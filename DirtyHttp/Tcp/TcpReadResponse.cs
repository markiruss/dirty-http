using DirtyHttp.Http;

namespace DirtyHttp.Tcp;

public class TcpReadResponse
{
    public TcpReadStatus Status { get; set; }
    public DirtyHttpRequest? HttpRequest { get; set; }
}

public enum TcpReadStatus
{
    Success,
    SocketClosed
}