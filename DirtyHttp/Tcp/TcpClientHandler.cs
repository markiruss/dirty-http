using DirtyHttp.Exceptions;
using DirtyHttp.Http;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace DirtyHttp.Tcp;

public class TcpClientHandler
{
    readonly DirtyHttpRequestHandler _requestHandler;
    readonly TcpHttpReader _reader;
    readonly TcpHttpWriter _writer;
    readonly ILogger<TcpClientHandler> _logger;

    public TcpClientHandler(DirtyHttpRequestHandler requesthandler, TcpHttpReader reader, TcpHttpWriter writer, ILogger<TcpClientHandler> logger)
    {
        _requestHandler = requesthandler;
        _reader = reader;
        _writer = writer;
        _logger = logger;
    }

    public async Task HandleClient(TcpClient client, CancellationToken stoppingToken)
    {
        try
        {
            NetworkStream stream = client.GetStream();            

            while (true)
            {
                DirtyHttpRequest request = await _reader.ReadHttpMessageAsync(stream, stoppingToken);
                
                DirtyHttpResponse response = await _requestHandler.InvokeAsync(request);
                                
                await _writer.WriteHttpMessageAsync(response, stream, stoppingToken);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error in TcpClientHandler");
        }
        finally
        {
            client.Dispose();
        }
    }
}
