using Loggy.ApiService;
using Loggy.ApiService.Controllers.Classes;
using Loggy.ApiService.Controllers.Interfaces;
using Loggy.ApiService.Services.Classes;
using Loggy.ApiService.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddControllers();
// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//Dependency Injection for Aspire client
builder.Services.AddScoped<IEventProcessingService, LogEventProcessingService>();

builder.Services.Configure<Options>(builder.Configuration.GetSection("MyService"));
builder.Services.AddHttpClient("gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240); // Must be 2x AttemptTimeout
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapDefaultEndpoints();
app.MapControllers();
app.Run();


