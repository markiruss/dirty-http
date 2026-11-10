using DirtyHttp.Exceptions;
using DirtyHttp.Tcp;
using DirtyHttp.Tests.Support;

namespace DirtyHttp.Tests.Tcp;

public class TcpHttpReaderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static CancellationTokenSource TimeoutToken()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(Timeout);
        return cts;
    }

    // ---- Baseline (passes today) -----------------------------------------------------------------

    [Fact]
    public async Task Reads_a_single_request()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        var reader = new TcpHttpReader();

        await sockets.SendAsync("GET /hello HTTP/1.1\r\nHost: x\r\n\r\n", ct);

        using var timeout = TimeoutToken();
        var result = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);

        Assert.Equal(TcpReadStatus.Success, result.Status);
        Assert.Equal("/hello", result.HttpRequest?.Path);
    }

    [Fact]
    public async Task Reads_a_request_that_arrives_in_several_writes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        sockets.Client.NoDelay = true;
        var reader = new TcpHttpReader();

        using var timeout = TimeoutToken();
        var read = reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);

        foreach (var part in new[] { "GET /hel", "lo HTTP/1.1\r\nHo", "st: x\r\n", "Content-Length: 3\r\n\r\nab", "c" })
        {
            await sockets.SendAsync(part, ct);
            await Task.Delay(20, ct);
        }

        var result = await read;
        Assert.Equal(TcpReadStatus.Success, result.Status);
        Assert.Equal("/hello", result.HttpRequest?.Path);
        Assert.Equal("abc", result.HttpRequest?.Body);
    }

    [Fact]
    public async Task Reads_two_requests_sent_one_after_the_other()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        var reader = new TcpHttpReader();
        using var timeout = TimeoutToken();

        await sockets.SendAsync("GET /one HTTP/1.1\r\n\r\n", ct);
        var first = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);

        await sockets.SendAsync("GET /two HTTP/1.1\r\n\r\n", ct);
        var second = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);

        Assert.Equal("/one", first.HttpRequest?.Path);
        Assert.Equal("/two", second.HttpRequest?.Path);
    }

    [Fact]
    public async Task Client_closing_returns_SocketClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        var reader = new TcpHttpReader();

        sockets.Client.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);

        using var timeout = TimeoutToken();
        var result = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);

        Assert.Equal(TcpReadStatus.SocketClosed, result.Status);
    }

    // ---- Known bugs (fail today) -----------------------------------------------------------------

    [Fact]
    public async Task Second_pipelined_request_is_read_from_the_buffer_without_waiting_for_more_data()
    {
        // Both requests arrive in one write. Today the second read blocks on the socket even though
        // the whole second request is already sitting in the buffer, so it times out.
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        var reader = new TcpHttpReader();

        await sockets.SendAsync("GET /one HTTP/1.1\r\n\r\nGET /two HTTP/1.1\r\n\r\n", ct);

        using var timeout = TimeoutToken();
        var first = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);
        Assert.Equal("/one", first.HttpRequest?.Path);

        var second = await reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token);
        Assert.Equal(TcpReadStatus.Success, second.Status);
        Assert.Equal("/two", second.HttpRequest?.Path);
    }

    [Fact]
    public async Task Header_line_longer_than_the_buffer_is_a_parse_error_not_a_closed_socket()
    {
        // Today the buffer fills up, ReadAsync is asked for 0 bytes, returns 0, and that is
        // mistaken for the client hanging up.
        var ct = TestContext.Current.CancellationToken;
        using var sockets = await SocketPair.CreateAsync(ct);
        var reader = new TcpHttpReader();

        await sockets.SendAsync("GET / HTTP/1.1\r\nX-Big: " + new string('a', 3000) + "\r\n\r\n", ct);

        using var timeout = TimeoutToken();
        await Assert.ThrowsAsync<HttpParseException>(
            () => reader.ReadHttpMessageAsync(sockets.ServerStream, timeout.Token));
    }
}
