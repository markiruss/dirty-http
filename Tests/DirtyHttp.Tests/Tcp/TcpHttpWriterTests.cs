using System.Text.RegularExpressions;
using DirtyHttp.Http;
using DirtyHttp.Tcp;
using DirtyHttp.Tests.Support;

namespace DirtyHttp.Tests.Tcp;

public class TcpHttpWriterTests
{
    private static async Task<string> WriteAndCaptureAsync(DirtyHttpResponse response)
    {
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);

        await new TcpHttpWriter().WriteHttpMessageAsync(response, sockets.ServerStream, ct);
        sockets.Server.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);

        var received = await sockets.ReceiveUntilClosedAsync(TimeSpan.FromSeconds(2), ct);
        return received.Text;
    }

    // ---- Baseline (passes today) -----------------------------------------------------------------

    [Fact]
    public async Task Writes_status_headers_content_length_and_body()
    {
        var response = new DirtyHttpResponse { StatusCode = 200, Body = "héllo" };
        response.Headers["Content-Type"] = "text/plain";

        string text = await WriteAndCaptureAsync(response);

        Assert.StartsWith("HTTP/1.1 200", text);
        Assert.Matches(new Regex(@"\r\ncontent-type:\s*text/plain\r\n", RegexOptions.IgnoreCase), text);
        Assert.Matches(new Regex(@"\r\ncontent-length:\s*6\r\n", RegexOptions.IgnoreCase), text); // é is 2 bytes
        Assert.EndsWith("\r\n\r\nhÃ©llo", text); // raw UTF-8 bytes seen as Latin-1
    }

    // ---- Known bugs (fail today) -----------------------------------------------------------------

    [Theory]
    [InlineData(200, "OK")]
    [InlineData(404, "Not Found")]
    [InlineData(500, "Internal Server Error")]
    public async Task Status_line_has_the_real_reason_phrase(int status, string reason)
    {
        string text = await WriteAndCaptureAsync(new DirtyHttpResponse { StatusCode = status });

        Assert.StartsWith($"HTTP/1.1 {status} {reason}\r\n", text);
    }

    [Fact]
    public async Task Content_length_set_by_the_handler_is_not_written_twice()
    {
        var response = new DirtyHttpResponse { StatusCode = 200, Body = "hello" };
        response.Headers["Content-Length"] = "5";

        string text = await WriteAndCaptureAsync(response);

        int count = Regex.Matches(text, "content-length", RegexOptions.IgnoreCase).Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Header_value_containing_CRLF_cannot_inject_extra_headers()
    {
        // Either throw, or strip/escape the line break. What must not happen is the injected
        // header appearing as its own line.
        var response = new DirtyHttpResponse { StatusCode = 200 };
        response.Headers["X-Name"] = "mark\r\nSet-Cookie: evil=1";

        string text;
        try
        {
            text = await WriteAndCaptureAsync(response);
        }
        catch (ArgumentException)
        {
            return; // rejecting it is fine
        }

        Assert.DoesNotContain("\r\nSet-Cookie:", text);
    }
}
