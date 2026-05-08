using Loggy.Web;
using Loggy.Web.ApiClients;
using Loggy.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
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


builder.Services.AddHttpClient<LogUploadApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
});

builder.Services.AddHttpClient<AnalysisApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
});


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
