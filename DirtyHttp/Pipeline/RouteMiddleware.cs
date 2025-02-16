using DirtyHttp.Http;

namespace DirtyHttp.Pipeline;

internal class RouteMiddleware : IDirtyHttpMiddleware
{
    public RouteMiddleware(HttpMethods method, string route, Func<DirtyHttpContext, Task> work)
    {
        _method = method;
        _route = route;
        _work = work;
    }

    public IDirtyHttpMiddleware? Next { get; set; }

    readonly Func<DirtyHttpContext, Task> _work;
    readonly HttpMethods _method;
    readonly string _route;
   

    public async Task InvokeAsync(DirtyHttpContext context)
    {
        if(_method == context.Request.Method && string.Equals(_route, context.Request.Path, StringComparison.OrdinalIgnoreCase))
        {
           await _work(context);
        }

        if (Next != null)
        {
            await Next.InvokeAsync(context);
        }        
    }
}
