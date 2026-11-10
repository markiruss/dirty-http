using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DirtyHttp.Tests.Support;

/// <summary>
/// Two connected loopback sockets. <see cref="Server"/> is handed to the code under test;
/// the test drives the conversation through <see cref="Client"/>.
/// </summary>
internal sealed class SocketPair : IDisposable
{
    private SocketPair(TcpClient client, TcpClient server)
    {
        Client = client;
        Server = server;
    }

    public TcpClient Client { get; }
    public TcpClient Server { get; }

    public NetworkStream ClientStream => Client.GetStream();
    public NetworkStream ServerStream => Server.GetStream();

    public static async Task<SocketPair> CreateAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var client = new TcpClient();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task connect = client.ConnectAsync(IPAddress.Loopback, port, ct).AsTask();
            TcpClient server = await listener.AcceptTcpClientAsync(ct);
            await connect;
            return new SocketPair(client, server);
        }
        finally
        {
            listener.Stop();
        }
    }

    public Task SendAsync(string text, CancellationToken ct) => SendAsync(Encoding.UTF8.GetBytes(text), ct);

    public async Task SendAsync(byte[] data, CancellationToken ct) => await ClientStream.WriteAsync(data, ct);

    /// <summary>
    /// Reads from the client side until <paramref name="isDone"/> is satisfied, the server closes the
    /// connection, or <paramref name="timeout"/> passes.
    /// </summary>
    public async Task<ReceiveResult> ReceiveAsync(Func<string, bool> isDone, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var received = new MemoryStream();
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                int n = await ClientStream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                {
                    return new ReceiveResult(Text(received), ServerClosed: true, TimedOut: false);
                }

                received.Write(buffer, 0, n);
                if (isDone(Text(received)))
                {
                    return new ReceiveResult(Text(received), ServerClosed: false, TimedOut: false);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ReceiveResult(Text(received), ServerClosed: false, TimedOut: true);
        }
        catch (IOException)
        {
            // Connection reset counts as the server closing on us.
            return new ReceiveResult(Text(received), ServerClosed: true, TimedOut: false);
        }
    }

    /// <summary>Reads until the server closes the connection (or the timeout passes).</summary>
    public Task<ReceiveResult> ReceiveUntilClosedAsync(TimeSpan timeout, CancellationToken ct) =>
        ReceiveAsync(_ => false, timeout, ct);

    private static string Text(MemoryStream ms) => Encoding.Latin1.GetString(ms.GetBuffer(), 0, (int)ms.Length);

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}

internal sealed record ReceiveResult(string Text, bool ServerClosed, bool TimedOut);
