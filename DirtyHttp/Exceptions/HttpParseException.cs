namespace DirtyHttp.Exceptions;

internal class HttpParseException : ApplicationException
{
    public HttpParseException(string message)
        : base(message)
    {

    }
}
