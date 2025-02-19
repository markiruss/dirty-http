using DirtyHttp.DependancyInjection;
using DirtyHttp.Http;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace DirtyHttpServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder();
            host.ConfigureServices((services) => services.AddDirtyHttpServer(new()
            {
                Port = 8080
            }));
           
            var app = host.Build();

            app.Use(async (context, next) =>
            {
                var timestamp = Stopwatch.GetTimestamp();
                await next(context);
                var elapsed = Stopwatch.GetElapsedTime(timestamp);
                Console.WriteLine($"Elapsed: {elapsed}");
            });

            app.MapRoute(HttpMethods.POST, "/hello", (context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.Headers["content-type"] = "application/json";
                context.Response.Body = "{ \"message\": \"Hello World\" }";
                return Task.CompletedTask;
            });

            await app.RunAsync();
        }
    }
}
