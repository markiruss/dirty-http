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

            app.MapRoute(HttpMethods.GET, "/hello", (context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.Body = "Hello World!";
                return Task.CompletedTask;
            });

            await app.RunAsync();
        }
    }
}
