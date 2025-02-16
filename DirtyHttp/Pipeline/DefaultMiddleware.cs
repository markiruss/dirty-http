using DirtyHttp.Http;

namespace DirtyHttp.Pipeline;

internal class DefaultMiddleware : IDirtyHttpMiddleware
{
    public DefaultMiddleware(Func<DirtyHttpContext, Func<DirtyHttpContext, Task>, Task> work)
    {
        _work = work;
    }

    public IDirtyHttpMiddleware? Next { get; set; }

    private readonly Func<DirtyHttpContext, Func<DirtyHttpContext, Task>, Task> _work;

    private async Task DoNext(DirtyHttpContext context)
    {
        if(Next != null)
        {
            await Next.InvokeAsync(context);
        }
    }

    public async Task InvokeAsync(DirtyHttpContext context)
    {
        await _work(context, DoNext);       
    }
}
