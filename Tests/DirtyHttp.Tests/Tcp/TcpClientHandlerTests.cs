using DirtyHttp.Http;
using DirtyHttp.Pipeline;
using DirtyHttp.Tcp;
using DirtyHttp.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DirtyHttp.Tests.Tcp;

/// <summary>
/// End-to-end over a real socket: bytes in, TcpClientHandler (reader + pipeline + writer), bytes out.
/// </summary>
public class TcpClientHandlerTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _serverStop = new();
    private SocketPair _sockets = null!;
    private Task _serverTask = Task.CompletedTask;

    public async ValueTask InitializeAsync()
    {
        _sockets = await SocketPair.CreateAsync(TestContext.Current.CancellationToken);

        var pipeline = new DirtyHttpPipeline();
        pipeline.AddMiddleware(new RouteMiddleware(HttpMethods.GET, "/hello", context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Body = "hi";
            return Task.CompletedTask;
        }));

        var handler = new TcpClientHandler(
            new DirtyHttpRequestHandler(pipeline),
            new TcpHttpReader(),
            new TcpHttpWriter(),
            NullLogger<TcpClientHandler>.Instance);

        _serverTask = handler.HandleClient(_sockets.Server, _serverStop.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _serverStop.Cancel();
        _sockets.Dispose();
        await _serverTask; // HandleClient swallows its own exceptions
        _serverStop.Dispose();
    }

    private static int CountResponses(string text) => text.Split("HTTP/1.1 ").Length - 1;

    // ---- Baseline (passes today) -----------------------------------------------------------------

    [Fact]
    public async Task Responds_to_a_request_and_keeps_the_connection_open()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sockets.SendAsync("GET /hello HTTP/1.1\r\nHost: x\r\n\r\n", ct);
        var first = await _sockets.ReceiveAsync(t => t.EndsWith("hi"), Timeout, ct);

        await _sockets.SendAsync("GET /hello HTTP/1.1\r\nHost: x\r\n\r\n", ct);
        var second = await _sockets.ReceiveAsync(t => t.EndsWith("hi"), Timeout, ct);

        Assert.StartsWith("HTTP/1.1 200", first.Text);
        Assert.StartsWith("HTTP/1.1 200", second.Text);
    }

    // ---- Known bugs (fail today) -----------------------------------------------------------------

    [Fact]
    public async Task Pipelined_requests_each_get_a_response()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sockets.SendAsync("GET /hello HTTP/1.1\r\n\r\nGET /hello HTTP/1.1\r\n\r\n", ct);
        var received = await _sockets.ReceiveAsync(t => CountResponses(t) >= 2 && t.EndsWith("hi"), Timeout, ct);

        Assert.False(received.TimedOut, $"Timed out. Received so far:\n{received.Text}");
        Assert.Equal(2, CountResponses(received.Text));
    }

    [Theory]
    [InlineData("BLAH\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nNoColonHere\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nHost : x\r\n\r\n")]
    [InlineData("POST / HTTP/1.1\r\nContent-Length: abc\r\n\r\n")]
    public async Task Malformed_request_gets_a_400_response_then_the_connection_is_closed(string raw)
    {
        // Today the connection is just dropped with no response at all.
        var ct = TestContext.Current.CancellationToken;

        await _sockets.SendAsync(raw, ct);
        var received = await _sockets.ReceiveUntilClosedAsync(Timeout, ct);

        Assert.StartsWith("HTTP/1.1 400", received.Text);
        Assert.True(received.ServerClosed, "Server should close the connection after a 400");
    }

    [Fact]
    public async Task Connection_close_header_closes_the_connection_after_the_response()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sockets.SendAsync("GET /hello HTTP/1.1\r\nConnection: close\r\n\r\n", ct);
        var received = await _sockets.ReceiveUntilClosedAsync(Timeout, ct);

        Assert.StartsWith("HTTP/1.1 200", received.Text);
        Assert.True(received.ServerClosed, "Server ignored 'Connection: close' and kept the socket open");
    }

    [Fact]
    public async Task Http_1_0_request_without_keep_alive_closes_the_connection_after_the_response()
    {
        // HTTP/1.0 connections are close-by-default unless the client sends "Connection: keep-alive".
        var ct = TestContext.Current.CancellationToken;

        await _sockets.SendAsync("GET /hello HTTP/1.0\r\n\r\n", ct);
        var received = await _sockets.ReceiveUntilClosedAsync(Timeout, ct);

        Assert.Contains(" 200", received.Text);
        Assert.True(received.ServerClosed, "Server kept an HTTP/1.0 connection open without keep-alive");
    }
}
