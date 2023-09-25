using System.Net;
using Common;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using InMemoryCacheLib;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SimplegRPCCacheService;
using SimplegRPCCacheService.Services;



var builder = Host.CreateDefaultBuilder(args);
var isServer =
    bool.TryParse(Environment.GetEnvironmentVariable("IsServer") ?? string.Empty, out var boolValue) && boolValue;

//builder.ConfigureLogging(logging =>
//{
//    logging.SetMinimumLevel(LogLevel.Debug);
//});

builder.ConfigureWebHostDefaults(webHostBuilder =>
{
    if (isServer)
    {
        webHostBuilder
            .UseUrls()
            .UseKestrel(options =>
            {
                options.ConfigureEndpointDefaults(c => { c.Protocols = HttpProtocols.Http1AndHttp2; });
                options.ListenAnyIP(8080, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });

            });
        webHostBuilder.UseStartup<StartupWithServer>();
    }
    else
    {
        webHostBuilder.UseStartup<Startup>();
    }
});

var app = builder.Build();

Console.WriteLine($"I'm {Dns.GetHostName()}");
#if DEBUG
if (!isServer)
{
    Console.WriteLine("Delay client starting in debug mode");
    Thread.Sleep(5000);
}
#endif
app.Run();