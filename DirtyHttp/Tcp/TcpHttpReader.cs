using DirtyHttp.Exceptions;
using DirtyHttp.Http;
using System.Net.Sockets;
using System.Text;

namespace DirtyHttp.Tcp;

public class TcpHttpReader
{
    readonly byte[] _byteBuffer = new byte[2048];
    readonly char[] _charBuffer = new char[2048];
    readonly StringBuilder _sb = new();

    public async Task<DirtyHttpRequest> ReadHttpMessageAsync(NetworkStream stream, CancellationToken stoppingToken)
    {
        // TODO: This is terrible, there is a better way to do it, sort it out. One glaring issue is that if a blank line
        // is never received it will loop for ever

        var currRequest = new DirtyHttpRequest();
        bool requestComplete = false;
        bool headersComplete = false;
        int indexOfBlankLine = 0;
        int bytesRead;
        int charsRead;

        while (!requestComplete)
        {
            bytesRead = await stream.ReadAsync(_byteBuffer, stoppingToken);
            charsRead = Encoding.UTF8.GetChars(_byteBuffer.AsSpan(0, bytesRead), _charBuffer);
            _sb.Append(_charBuffer.AsSpan(0, charsRead));

            while (stream.DataAvailable)
            {
                bytesRead = await stream.ReadAsync(_byteBuffer, stoppingToken);
                charsRead = Encoding.UTF8.GetChars(_byteBuffer.AsSpan(0, bytesRead), _charBuffer);
                _sb.Append(_charBuffer.AsSpan(0, charsRead));
            }
            string fullRequestReceived = _sb.ToString();
            if (!headersComplete)
            {
                indexOfBlankLine = fullRequestReceived.IndexOf("\r\n\r\n");
                if (indexOfBlankLine > -1)
                {
                    // Headers are complete
                    ReadOnlySpan<char> headers = fullRequestReceived.AsSpan(0, indexOfBlankLine);
                    int i = 0;
                    foreach (ReadOnlySpan<char> line in headers.EnumerateLines())
                    {
                        if (i == 0)
                        {
                            // FirstLine
                            int y = 0;
                            foreach (Range section in line.Split(' '))
                            {
                                if (y == 0)
                                {
                                    currRequest.Method = Enum.Parse<HttpMethods>(line[section]);
                                }
                                else if (y == 1)
                                {
                                    currRequest.Path = line[section].ToString();
                                }
                                else if (y == 2)
                                {
                                    currRequest.HttpVersion = line[section].ToString();
                                    if (currRequest.HttpVersion != Server.SUPPORTED_HTTP_VERSION)
                                    {
                                        throw new HttpVersionException(currRequest.HttpVersion);
                                    }
                                }
                                y++;
                            }
                            if (y > 3)
                            {
                                throw new HttpParseException("Error on first line");
                            }
                        }
                        else
                        {
                            // Header
                            int indexOfColon = line.IndexOf(":", StringComparison.Ordinal);
                            ReadOnlySpan<char> key = line.Slice(0, indexOfColon).Trim();
                            ReadOnlySpan<char> value = line.Slice(indexOfColon + 1).Trim();
                            currRequest.Headers.Add(key.ToString(), value.ToString());
                        }
                        i++;
                    }
                    headersComplete = true;
                }
            }
            if (headersComplete)
            {
                if (currRequest.Headers.ContainsKey("content-length"))
                {
                    int contentLength = int.Parse(currRequest.Headers["content-length"]);
                    ReadOnlySpan<char> body = fullRequestReceived.AsSpan(indexOfBlankLine + 4);
                    int bodyReceived = Encoding.UTF8.GetByteCount(body);
                    if (bodyReceived >= contentLength)
                    {
                        bytesRead = Encoding.UTF8.GetBytes(body, _byteBuffer);
                        charsRead = Encoding.UTF8.GetChars(_byteBuffer.AsSpan(0, contentLength), _charBuffer);
                        currRequest.Body = _charBuffer.AsSpan(0, charsRead).ToString();
                        charsRead = Encoding.UTF8.GetChars(_byteBuffer.AsSpan(contentLength, bytesRead - contentLength), _charBuffer);
                        ResetSb(_charBuffer.AsSpan(0, charsRead));
                        requestComplete = true;
                    }
                }
                else
                {
                    ResetSb(fullRequestReceived.AsSpan(indexOfBlankLine + 4));
                    requestComplete = true;
                }
            }
        }
        if (currRequest.HttpVersion != Server.SUPPORTED_HTTP_VERSION)
        {
            throw new HttpVersionException(currRequest.HttpVersion);
        }

        return currRequest;
    }

    private void ResetSb(ReadOnlySpan<char> leftOver)
    {
        _sb.Clear();
        _sb.Append(leftOver);
    }
}
