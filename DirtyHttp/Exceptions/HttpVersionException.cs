namespace DirtyHttp.Exceptions;

internal class HttpVersionException : ApplicationException
{
    public string HttpVersion;
    public HttpVersionException(string version)
        : base($"Unsuportted HTTP version: {version}")
    {
        HttpVersion = version;
    }
}
