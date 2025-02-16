using DirtyHttp.Options;
using DirtyHttp.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;

namespace DirtyHttp;

public class Server : BackgroundService
{
    TcpClientHandler _tcpHandler;
    DirtyHttpOptions _options;
    IServiceProvider _serviceProvider;

    public const string SUPPORTED_HTTP_VERSION = "HTTP/1.1";

    public Server(TcpClientHandler tcpHandler, DirtyHttpOptions options, IServiceProvider serviceProvider)
    {
        _tcpHandler = tcpHandler;
        _options = options;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IPAddress localAddr = IPAddress.Parse("127.0.0.1");

        var tcpListener = new TcpListener(localAddr, _options.Port);

        tcpListener.Start();

        while (true)
        {
            // This is a blocking call
            TcpClient client = await tcpListener.AcceptTcpClientAsync();
            
            // New thread per client
            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var tcpHandler = scope.ServiceProvider.GetRequiredService<TcpClientHandler>();
                await _tcpHandler.HandleClient(client, stoppingToken);
            }, stoppingToken);
        }
    }
}
