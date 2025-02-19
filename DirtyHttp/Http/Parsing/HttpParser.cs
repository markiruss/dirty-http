using Microsoft.Extensions.Options;
using System.Diagnostics;
using System;
using System.Text;

namespace DirtyHttp.Http.Parsing;

internal class HttpParser
{
    private DirtyHttpRequest _currRequest = new();
    ParsingStatus _status = ParsingStatus.FirstLine;
    int? _contentLength = null;
        
    private static byte[] CrLfBytes = [(byte)'\r', (byte)'\n'];
    private const byte ColonByte = (byte)':';
    private const byte SpaceByte = (byte)' ';
    private static readonly (byte[], HttpMethods)[] HttpMethodBytes =
    [
        (Encoding.UTF8.GetBytes("GET"), HttpMethods.GET),
        (Encoding.UTF8.GetBytes("HEAD"), HttpMethods.HEAD),
        (Encoding.UTF8.GetBytes("OPTIONS"), HttpMethods.OPTIONS),
        (Encoding.UTF8.GetBytes("TRACE"), HttpMethods.TRACE),
        (Encoding.UTF8.GetBytes("PUT"), HttpMethods.PUT),
        (Encoding.UTF8.GetBytes("DELETE"), HttpMethods.DELETE),
        (Encoding.UTF8.GetBytes("POST"), HttpMethods.POST),
        (Encoding.UTF8.GetBytes("PATCH"), HttpMethods.PATCH),
        (Encoding.UTF8.GetBytes("CONNECT"), HttpMethods.CONNECT),
    ];

    public void Clear()
    {
        _currRequest = new();
        _status = ParsingStatus.FirstLine;
        _contentLength = null;
    }

    public ParseResponse ParseChunk(Span<byte> buffer)
    {
        if (_status == ParsingStatus.FirstLine)
        {
            int indexOfLineEnd = buffer.IndexOf(CrLfBytes);
            if (indexOfLineEnd == -1)
            {
                // We don't have a full line
                return NeedMoreData(buffer);
            }

            // We have a line
            Span<byte> line = buffer.Slice(0, indexOfLineEnd);
            if (line.Count(SpaceByte) != 2)
            {
                // Not 2 counts of space
                return ErrorOut();
            }

            // We have 3 sections
            int indexOfFirstSpace = line.IndexOf(SpaceByte);
            if (TryGetMethod(line.Slice(0, indexOfFirstSpace), out HttpMethods method))
            {
                _currRequest.Method = method;
            }
            else
            {
                // Not a proper method
                return ErrorOut();
            }

            // There is only 2 so it is the last index
            int indexOfSecondSpace = line.LastIndexOf(SpaceByte);

            _currRequest.Path = Encoding.UTF8.GetString(line.Slice(indexOfFirstSpace + 1, indexOfSecondSpace - indexOfFirstSpace - 1));

            _currRequest.HttpVersion = Encoding.UTF8.GetString(line.Slice(indexOfSecondSpace + 1));

            // Advance the buffer
            buffer = buffer.Slice(indexOfLineEnd + 2);
            _status = ParsingStatus.Headers;
        }

        if (_status == ParsingStatus.Headers)
        {
            while (true)
            {
                if (buffer.StartsWith(CrLfBytes))
                {
                    // End of Headers
                    _status = ParsingStatus.Body;
                    // Advance the buffer
                    buffer = buffer.Slice(2);
                    break;
                }

                int indexOfLineEnd = buffer.IndexOf(CrLfBytes);
                if (indexOfLineEnd == -1)
                {
                    // Not a whole line
                    return NeedMoreData(buffer);
                }

                // We have a full line
                Span<byte> line = buffer.Slice(0, indexOfLineEnd);
                
                int indexOfColon = line.IndexOf(ColonByte);
                if (indexOfColon == -1)
                {
                    // Not 1 colon
                    return ErrorOut();
                }

                Span<byte> headerKey = line.Slice(0, indexOfColon);

                Span<byte> headerValue = line.Slice(indexOfColon + 1);
                if (headerValue[0] == SpaceByte)
                {
                    // There is an optional space that should be ignored in each header
                    headerValue = headerValue.Slice(1);
                }

                _currRequest.Headers.Add(Encoding.UTF8.GetString(headerKey), Encoding.UTF8.GetString(headerValue));

                // Advance the buffer
                buffer = buffer.Slice(indexOfLineEnd + 2);
            }
        }

        if (_status == ParsingStatus.Body)
        {
            if (!_contentLength.HasValue)
            {
                // Look for it
                if (_currRequest.Headers.ContainsKey("content-length"))
                {
                    _contentLength = int.Parse(_currRequest.Headers["content-length"]);
                }
                else
                {
                    _status = ParsingStatus.Complete;
                }
            }

            if (_contentLength.HasValue)
            {
                // We need to look for a body
                int existingByteCount = Encoding.UTF8.GetByteCount(_currRequest.Body);
                if (existingByteCount >= _contentLength.Value)
                {
                    _status = ParsingStatus.Complete;
                }
                else
                {
                    int neededBytes = _contentLength.Value - existingByteCount;
                    int bytesTotake = Math.Min(neededBytes, buffer.Length);
                    _currRequest.Body += Encoding.UTF8.GetString(buffer.Slice(0, bytesTotake));

                    // Advance the buffer
                    buffer = buffer.Slice(bytesTotake);

                    if (bytesTotake == neededBytes)
                    {
                        _status = ParsingStatus.Complete;
                    }
                }
            }
        }

        return new ParseResponse
        {
            Request = _currRequest,
            Status = _status,
            LeftOver = buffer
        };
    }

    private ParseResponse NeedMoreData(Span<byte> leftOver)
    {
        return new ParseResponse
        {
            Status = _status,
            Request = _currRequest,
            LeftOver = leftOver
        };
    }

    private ParseResponse ErrorOut()
    {
        _status = ParsingStatus.Error;
        return new ParseResponse
        {
            Status = _status,
            Request = _currRequest
        };
    }

    private bool TryGetMethod(Span<byte> methodBytes, out HttpMethods method)
    {
        for (int i = 0; i < HttpMethodBytes.Length; i++)
        {
            if (methodBytes.SequenceEqual(HttpMethodBytes[i].Item1))
            {
                method = HttpMethodBytes[i].Item2;
                return true;
            }
        }
        method = default;
        return false;
    }
}
