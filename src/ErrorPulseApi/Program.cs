using ErrorPulseApi;
using ErrorPulseApi.Configuration;
using ErrorPulseApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var startup = new Startup();
startup.ConfigureServices(builder.Services);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders(); 
builder.Logging.AddConsole();
builder.Logging.AddDebug(); 

builder.Services
    .AddOptions<GenerationOptions>()
    .Bind(builder.Configuration.GetSection("Generation"))
    .ValidateOnStart();

builder.Services
    .AddOptions<DataFoldersOptions>()
    .Bind(builder.Configuration.GetSection("DataFolders"))
    .ValidateOnStart();

var app = builder.Build();
startup.Configure(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();