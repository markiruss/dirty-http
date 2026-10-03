using DirtyHttp.Exceptions;
using DirtyHttp.Http;
using DirtyHttp.Http.Parsing;
using System.Net.Sockets;

namespace DirtyHttp.Tcp;

public class TcpHttpReader
{
    // The buffer size is important, it is basically the max header size. If a header line does not all
    // fit in this buffer then the parser will throw an error
    const int _bufferSize = 2048;
    readonly byte[] _Buffer = new byte[_bufferSize];
    int _bufferOffset = 0;
    readonly HttpParser _parser = new();

    public async Task<TcpReadResponse> ReadHttpMessageAsync(NetworkStream stream, CancellationToken stoppingToken)
    {
        _parser.Clear();
        while (true)
        {
            int bytesRead = await stream.ReadAsync(_Buffer, _bufferOffset, _Buffer.Length - _bufferOffset, stoppingToken);

            if (bytesRead == 0)
            {
                // Peer socket shutdown
                return new TcpReadResponse
                {
                    Status = TcpReadStatus.SocketClosed
                };
            }
            
            var httpResponse = _parser.ParseChunk(_Buffer.AsSpan(0, bytesRead + _bufferOffset));
            switch (httpResponse.Status)
            {
                case ParsingStatus.FirstLine:
                case ParsingStatus.Headers:
                case ParsingStatus.Body:
                    // Add the leftover to the buffer and let it loop again
                    httpResponse.LeftOver.CopyTo(_Buffer);                    
                    _bufferOffset = httpResponse.LeftOver.Length;
                    break;

                case ParsingStatus.Error:                    
                    throw new HttpParseException("Error Parsing Http");

                case ParsingStatus.Complete:
                    // Add the leftover to the buffer and return the response
                    httpResponse.LeftOver.CopyTo(_Buffer);
                    _bufferOffset = httpResponse.LeftOver.Length;
                    return new TcpReadResponse
                    {
                        HttpRequest = httpResponse.Request,
                        Status = TcpReadStatus.Success
                    };
            }
        }
    }
}
