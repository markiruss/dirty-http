using System.Text;
using DirtyHttp.Http;
using DirtyHttp.Http.Parsing;
using static DirtyHttp.Tests.Support.ParserHarness;

namespace DirtyHttp.Tests.Parsing;

/// <summary>
/// Behaviour the parser already gets right. These should pass today and keep passing while you fix things.
/// </summary>
public class HttpParserBaselineTests
{
    [Fact]
    public void Parses_request_line_and_headers()
    {
        var result = ParseOnce("GET /hello HTTP/1.1\r\nHost: example.com\r\nAccept: */*\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(HttpMethods.GET, result.Request.Method);
        Assert.Equal("/hello", result.Request.Path);
        Assert.Equal("HTTP/1.1", result.Request.HttpVersion);
        Assert.Equal("example.com", result.Request.Headers["Host"]);
        Assert.Equal("*/*", result.Request.Headers["Accept"]);
        Assert.Empty(result.LeftOver);
    }

    [Fact]
    public void Header_names_are_case_insensitive()
    {
        var result = ParseOnce("GET / HTTP/1.1\r\nCONTENT-type: text/plain\r\n\r\n");

        Assert.Equal("text/plain", result.Request.Headers["content-type"]);
    }

    [Fact]
    public void Parses_body_using_content_length()
    {
        var result = ParseOnce("POST /echo HTTP/1.1\r\nContent-Length: 5\r\n\r\nhello");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(HttpMethods.POST, result.Request.Method);
        Assert.Equal("hello", result.Request.Body);
        Assert.Empty(result.LeftOver);
    }

    [Fact]
    public void Request_without_content_length_has_empty_body()
    {
        var result = ParseOnce("POST /echo HTTP/1.1\r\nHost: x\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(string.Empty, result.Request.Body);
    }

    [Fact]
    public void Http_1_0_is_accepted()
    {
        var result = ParseOnce("GET / HTTP/1.0\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("HTTP/1.0", result.Request.HttpVersion);
    }

    [Fact]
    public void Incomplete_request_line_asks_for_more_data_and_keeps_bytes()
    {
        var result = ParseOnce("GET /hel");

        Assert.Equal(ParsingStatus.FirstLine, result.Status);
        Assert.Equal("GET /hel", result.LeftOverText);
    }

    [Fact]
    public void Incomplete_header_line_asks_for_more_data_and_keeps_only_that_line()
    {
        var result = ParseOnce("GET / HTTP/1.1\r\nHost: x\r\nAcc");

        Assert.Equal(ParsingStatus.Headers, result.Status);
        Assert.Equal("Acc", result.LeftOverText);
    }

    [Fact]
    public void Second_pipelined_request_is_returned_as_leftover()
    {
        const string first = "GET /one HTTP/1.1\r\n\r\n";
        const string second = "GET /two HTTP/1.1\r\n\r\n";

        var result = ParseOnce(first + second);

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("/one", result.Request.Path);
        Assert.Equal(second, result.LeftOverText);
    }

    [Fact]
    public void Ascii_request_gives_same_result_wherever_the_network_splits_it()
    {
        const string next = "GET /next HTTP/1.1\r\n\r\n";
        byte[] raw = Bytes("POST /echo HTTP/1.1\r\nHost: x\r\nContent-Length: 11\r\n\r\nhello world" + next);

        for (int split = 1; split < raw.Length; split++)
        {
            var result = ParseSplitAt(raw, split);

            Assert.True(ParsingStatus.Complete == result.Status, $"split at {split}: status {result.Status}");
            Assert.True("hello world" == result.Request.Body, $"split at {split}: body '{result.Request.Body}'");
            Assert.True(next == result.LeftOverText, $"split at {split}: leftover '{result.LeftOverText}'");
        }
    }

    [Theory]
    [InlineData("FOO / HTTP/1.1\r\n\r\n")]
    [InlineData("get / HTTP/1.1\r\n\r\n")] // methods are case-sensitive
    [InlineData("GET /a b HTTP/1.1\r\n\r\n")] // too many spaces
    [InlineData("GET /\r\n\r\n")] // too few parts
    [InlineData("GET / HTTP/1.1\r\nNoColonHere\r\n\r\n")]
    public void Malformed_requests_are_rejected(string raw)
    {
        Assert.Equal(ParsingStatus.Error, ParseOnce(raw).Status);
    }
}

/// <summary>
/// Known bugs. Every test in here fails today; each one should go green as you fix the matching issue.
/// </summary>
public class HttpParserBugTests
{
    // ---- Body is counted as UTF-8 text instead of bytes -------------------------------------------

    [Fact]
    public void Body_split_in_the_middle_of_a_multibyte_character_is_read_correctly()
    {
        // "é" is 2 bytes in UTF-8 (C3 A9). Split the network read between them.
        const string next = "GET /next HTTP/1.1\r\n\r\n";
        byte[] head = Bytes("POST /echo HTTP/1.1\r\nContent-Length: 4\r\n\r\nab");
        byte[] e = Bytes("é");

        var result = ParseInChunks([.. head, e[0]], [e[1], .. Bytes("z" + next)]);

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("abéz".Length, result.Request.Body.Length);
        Assert.Equal("abéz", result.Request.Body);
        Assert.Equal(next, result.LeftOverText);
    }

    [Fact]
    public void Utf8_request_gives_same_result_wherever_the_network_splits_it()
    {
        const string body = "héllo wörld"; // 13 bytes, 11 chars
        const string next = "GET /next HTTP/1.1\r\n\r\n";
        int bodyBytes = Encoding.UTF8.GetByteCount(body);
        byte[] raw = Bytes($"POST /echo HTTP/1.1\r\nContent-Length: {bodyBytes}\r\n\r\n{body}{next}");

        var failures = new List<string>();
        for (int split = 1; split < raw.Length; split++)
        {
            var result = ParseSplitAt(raw, split);
            if (result.Status != ParsingStatus.Complete || result.Request.Body != body || result.LeftOverText != next)
            {
                failures.Add($"split at {split}: status={result.Status} body='{result.Request.Body}' leftover='{result.LeftOverText}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Binary_body_consumes_exactly_content_length_bytes()
    {
        // Not valid UTF-8. Whatever the body ends up as, the parser must take exactly 4 bytes
        // and leave the next request untouched.
        const string next = "GET /next HTTP/1.1\r\n\r\n";
        byte[] head = Bytes("POST /upload HTTP/1.1\r\nContent-Length: 4\r\n\r\n");
        byte[] body = [0xFF, 0xFE, 0x00, 0x80];

        var result = ParseInChunks([.. head, body[0]], [.. body[1..], .. Bytes(next)]);

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(next, result.LeftOverText);
    }

    // ---- Header values ---------------------------------------------------------------------------

    [Fact]
    public void Empty_header_value_is_allowed()
    {
        // Today: IndexOutOfRangeException from headerValue[0].
        var result = ParseOnce("GET / HTTP/1.1\r\nX-Empty:\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(string.Empty, result.Request.Headers["X-Empty"]);
    }

    [Theory]
    [InlineData("X-Test:value", "value")]
    [InlineData("X-Test:   value", "value")]
    [InlineData("X-Test:\tvalue", "value")]
    [InlineData("X-Test: value  ", "value")]
    [InlineData("X-Test: \t value \t ", "value")]
    [InlineData("X-Test: two words", "two words")]
    public void Optional_whitespace_around_header_value_is_trimmed(string headerLine, string expected)
    {
        var result = ParseOnce($"GET / HTTP/1.1\r\n{headerLine}\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal(expected, result.Request.Headers["X-Test"]);
    }

    [Fact]
    public void Repeated_header_values_are_combined()
    {
        // Today: ArgumentException from Dictionary.Add. RFC 9110 §5.3 allows combining with ", ".
        var result = ParseOnce("GET / HTTP/1.1\r\nAccept: text/html\r\naccept: application/json\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("text/html, application/json", result.Request.Headers["Accept"]);
    }

    [Theory]
    [InlineData("Host : example.com")]
    [InlineData("Host\t: example.com")]
    [InlineData(" Host: example.com")]
    public void Whitespace_in_or_before_header_name_is_rejected(string headerLine)
    {
        // RFC 9112 §5.1: no whitespace allowed between field name and colon; must answer 400.
        var result = ParseOnce($"GET / HTTP/1.1\r\n{headerLine}\r\n\r\n");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    [Fact]
    public void Empty_header_name_is_rejected()
    {
        var result = ParseOnce("GET / HTTP/1.1\r\n: value\r\n\r\n");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    // ---- Content-Length --------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData("5x")]
    [InlineData("")]
    [InlineData("99999999999999999999")]
    public void Invalid_content_length_is_rejected(string value)
    {
        // Today: FormatException/OverflowException escapes, or a negative length is accepted.
        var result = ParseOnce($"POST / HTTP/1.1\r\nContent-Length: {value}\r\n\r\nhello");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    [Fact]
    public void Conflicting_content_length_headers_are_rejected()
    {
        // Classic request-smuggling vector.
        var result = ParseOnce("POST / HTTP/1.1\r\nContent-Length: 3\r\nContent-Length: 5\r\n\r\nhello");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    // ---- Transfer-Encoding -----------------------------------------------------------------------

    [Fact]
    public void Chunked_body_is_not_silently_ignored()
    {
        // Either decode it, or reject it. What it must not do is report "complete, empty body" and
        // leave the chunk data to be parsed as the next request.
        var result = ParseOnce("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc\r\n0\r\n\r\n");

        bool rejected = result.Status == ParsingStatus.Error;
        bool decoded = result.Status == ParsingStatus.Complete && result.Request.Body == "abc" && result.LeftOver.Length == 0;
        Assert.True(rejected || decoded,
            $"status={result.Status} body='{result.Request.Body}' leftover='{result.LeftOverText}'");
    }

    [Fact]
    public void Transfer_encoding_together_with_content_length_is_rejected()
    {
        var result = ParseOnce(
            "POST / HTTP/1.1\r\nContent-Length: 5\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    // ---- Request line ----------------------------------------------------------------------------

    [Theory]
    [InlineData("BANANA")]
    [InlineData("HTTP/2.0")]
    [InlineData("http/1.1")]
    [InlineData("HTTP/1.1x")]
    [InlineData("")]
    public void Unsupported_http_version_is_rejected(string version)
    {
        var result = ParseOnce($"GET / {version}\r\n\r\n");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    [Fact]
    public void Empty_request_target_is_rejected()
    {
        var result = ParseOnce("GET  HTTP/1.1\r\n\r\n");

        Assert.Equal(ParsingStatus.Error, result.Status);
    }

    [Fact]
    public void Query_string_is_not_part_of_the_path()
    {
        // Today Path is "/hello?name=mark", so routes never match when there's a query string.
        // When you fix this, consider adding a Query property and asserting on it here too.
        var result = ParseOnce("GET /hello?name=mark HTTP/1.1\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("/hello", result.Request.Path);
    }

    [Fact]
    public void Leading_empty_line_before_request_line_is_ignored()
    {
        // RFC 9112 §2.2: a server SHOULD ignore at least one empty line received before the request-line.
        var result = ParseOnce("\r\nGET / HTTP/1.1\r\n\r\n");

        Assert.Equal(ParsingStatus.Complete, result.Status);
        Assert.Equal("/", result.Request.Path);
    }
}
