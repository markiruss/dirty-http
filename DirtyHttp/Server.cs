using DirtyHttp.Options;
using DirtyHttp.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace DirtyHttp;

public class Server : BackgroundService
{
    TcpClientHandler _tcpHandler;
    DirtyHttpOptions _options;
    IServiceProvider _serviceProvider;
    ILogger<Server> _logger;

    public const string SUPPORTED_HTTP_VERSION = "HTTP/1.1";

    public Server(TcpClientHandler tcpHandler, DirtyHttpOptions options, IServiceProvider serviceProvider, ILogger<Server> logger)
    {
        _tcpHandler = tcpHandler;
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, _options.Port);

        tcpListener.Start();
        _logger.LogInformation("Listening on port {port}", _options.Port);

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
