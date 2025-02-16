using DirtyHttp.Http;
using DirtyHttp.Options;
using DirtyHttp.Pipeline;
using DirtyHttp.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace DirtyHttp.DependancyInjection;

public static class DirtyHttpServiceCollectionExtensions
{
    public static IServiceCollection AddDirtyHttpServer(this IServiceCollection services, DirtyHttpOptions options)
    {
        return services
             .AddSingleton(options)
             .AddHostedService<Server>()             
             .AddSingleton<DirtyHttpPipeline>()             
             .AddSingleton<DirtyHttpRequestHandler>()
             .AddScoped<TcpClientHandler>()
             .AddScoped<TcpHttpReader>()
             .AddScoped<TcpHttpWriter>();
    }
}
