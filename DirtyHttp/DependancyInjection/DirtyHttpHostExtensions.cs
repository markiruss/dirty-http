using DirtyHttp.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DirtyHttp.Http;

namespace DirtyHttp.DependancyInjection;

public static class DirtyHttpHostExtensions
{
    public static IHost Use(this IHost host, IDirtyHttpMiddleware middleware)
    {
        DirtyHttpPipeline pipeline = host.Services.GetService<DirtyHttpPipeline>()
            ?? throw new ApplicationException("The DirtyHttp Server has not been configured correctly");

        pipeline.AddMiddleware(middleware);

        return host;
    }

    public static IHost Use(this IHost host, Func<DirtyHttpContext, Func<DirtyHttpContext, Task>, Task> work)
    {
        return host.Use(new DefaultMiddleware(work));
    }

    public static IHost MapRoute(this IHost host, HttpMethods method, string route, Func<DirtyHttpContext, Task> work)
    {
        return host.Use(new RouteMiddleware(method, route, work));
    }
}
