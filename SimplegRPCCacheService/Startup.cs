using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using SimplegRPCCacheService.Client;
using SimplegRPCCacheService.Services;

namespace SimplegRPCCacheService
{
    public class Startup 
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc(configureOptions =>
            {
                configureOptions.EnableDetailedErrors = true;
                configureOptions.MaxReceiveMessageSize = 32 * 1024 * 1024; // 32 MB
                configureOptions.MaxSendMessageSize = 32 * 1024 * 1024; // 32 MB
            });
            services.AddTransient<CacherClientProxy>();
            services.AddHostedService<TestClientBackgrounder>();
            //services.AddTransient<Cacher.CacherClient>(provider =>
            //    {
            //        var channel = GrpcChannel.ForAddress("http://simplegrpccacheservice:8080", new GrpcChannelOptions()
            //        {
            //            UnsafeUseInsecureChannelCallCredentials = true,
            //            MaxReceiveMessageSize = 32 * 1024 * 1024, // 32 MB
            //            MaxSendMessageSize = 32 * 1024 * 1024, // 32 MB
            //        });
            //        return new Cacher.CacherClient(channel);
            //    }
            //);
            services.AddGrpcClient<Cacher.CacherClient>(options =>
            {
                options.Address = new Uri("http://simplegrpccacheservice:8080");
            });

        }

        public void Configure(IApplicationBuilder app)
        {
            
        }
    }
}
