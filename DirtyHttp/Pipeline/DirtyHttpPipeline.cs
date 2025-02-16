using DirtyHttp.Http;

namespace DirtyHttp.Pipeline;

public class DirtyHttpPipeline
{
    private IDirtyHttpMiddleware? _initialMiddleware;
    
    public async Task<DirtyHttpContext> InvokePipelineAsync(DirtyHttpContext context)
    {
        if(_initialMiddleware != null)
        {
            await _initialMiddleware.InvokeAsync(context);
        }
        return context;
    }

    public void AddMiddleware(IDirtyHttpMiddleware middleware)
    {
        if(_initialMiddleware == null)
        {
            _initialMiddleware = middleware;
            return;
        }

        IDirtyHttpMiddleware currentMiddleware = _initialMiddleware;
        while(currentMiddleware.Next != null)
        {
            currentMiddleware = currentMiddleware.Next;
        }
        currentMiddleware.Next = middleware;
    }
}
