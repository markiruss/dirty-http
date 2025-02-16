namespace DirtyHttp.Http;

public class DirtyHttpContext
{
    public DirtyHttpContext(DirtyHttpRequest request, DirtyHttpResponse response)
    {
        Request = request;
        Response = response;
    }

    public DirtyHttpRequest Request { get; set; }
    public DirtyHttpResponse Response { get; set; }
}
