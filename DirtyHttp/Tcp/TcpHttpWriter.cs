using DirtyHttp.Http;
using System.Net.Sockets;
using System.Text;

namespace DirtyHttp.Tcp;

public class TcpHttpWriter
{
    public async Task WriteHttpMessageAsync(DirtyHttpResponse response, NetworkStream stream, CancellationToken stoppingToken)
    {
        response.HttpVersion = Server.SUPPORTED_HTTP_VERSION;
        await stream.WriteAsync(Encoding.UTF8.GetBytes(SerializeHttp(response)), stoppingToken);
    }

    private string SerializeHttp(DirtyHttpResponse response)
    {
        string newLine = "\r\n";
        var sb = new StringBuilder();
        sb.Append(response.HttpVersion);
        sb.Append(" ");
        sb.Append(response.StatusCode);
        sb.Append(" ");
        sb.Append("status-message"); // Todo: real status message
        sb.Append(newLine);
        foreach (var header in response.Headers)
        {
            sb.Append(header.Key);
            sb.Append(':');
            sb.Append(header.Value);
            sb.Append(newLine);
        }
        
        int bodySize = 0;
        if (!string.IsNullOrEmpty(response.Body))
        {
            bodySize = Encoding.UTF8.GetByteCount(response.Body);
        }
        sb.Append("content-length:");
        sb.Append(bodySize);
        sb.Append(newLine);

        sb.Append(newLine);
        sb.Append(response.Body);

        var test = sb.ToString();
        return test;
    }
}
