using ErrorPulseApi.Services;

namespace ErrorPulseApi;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReferenceDataProvider, ReferenceDataProvider>();
        services.AddScoped<ICsvGenerationService, CsvGenerationService>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => { });
    }
}