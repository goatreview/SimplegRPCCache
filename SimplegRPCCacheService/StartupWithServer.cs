using Common;
using InMemoryCacheLib;
using SimplegRPCCacheService.Server;
using SimplegRPCCacheService.Services;

namespace SimplegRPCCacheService
{
    public class StartupWithServer 
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc(configureOptions =>
            {
                configureOptions.EnableDetailedErrors = true;
                configureOptions.MaxReceiveMessageSize = 32 * 1024 * 1024; // 32 MB
                configureOptions.MaxSendMessageSize = 32 * 1024 * 1024; // 32 MB
            });
            services.AddSingleton<CacherService>();
            services.AddSingleton<IKeyValueStore, InMemoryKeyValueStore>();
            services.AddHostedService<TestServerBackgrounder>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<CacherService>();
            });
        }
    }
}
