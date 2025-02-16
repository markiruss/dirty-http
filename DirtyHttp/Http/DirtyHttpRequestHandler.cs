using DirtyHttp.Pipeline;

namespace DirtyHttp.Http;

public class DirtyHttpRequestHandler
{
    private DirtyHttpPipeline _pipeline;

    public DirtyHttpRequestHandler(DirtyHttpPipeline pipeline)
    {
        _pipeline = pipeline;
    }
    public async Task<DirtyHttpResponse> InvokeAsync(DirtyHttpRequest request)
    {
        try
        {
            var response = new DirtyHttpResponse { StatusCode = 404 };
            var context = new DirtyHttpContext(request, response);

            context = await _pipeline.InvokePipelineAsync(context);
            return context.Response;
        }
        catch
        {
            return new DirtyHttpResponse { StatusCode = 500 };
        }
    }
}
