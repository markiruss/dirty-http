using DirtyHttp.Http;

namespace DirtyHttp.Pipeline;

public interface IDirtyHttpMiddleware
{
    IDirtyHttpMiddleware? Next { get; set; }
    public Task InvokeAsync(DirtyHttpContext context);
}
