using Microsoft.Extensions.DependencyInjection;

namespace ErrorPulseApi
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
            });
        }
    }
}

