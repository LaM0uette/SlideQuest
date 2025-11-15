using System.Globalization;
using GameConfig;
using GridGenerator;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using SlideQuest.Components;
using SlideQuest.Endpoints;
using SlideQuest.Hubs;
using SlideQuest.Client.Services;
using SlideQuest.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Localization: force default culture to fr-FR on server (affects prerendering)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] supportedCultures = [ new("fr-FR") ];
    options.DefaultRequestCulture = new RequestCulture("fr-FR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSignalR();

// Register HttpClient on the server to satisfy DI for prerendered components
builder.Services.AddHttpClient();


// Register a fake hub client on the server to satisfy DI during prerendering
builder.Services.AddSingleton<IGameHubClient, FakeGameHubClient>();

builder.Services.AddSingleton<IGridGenerator, FakeGridGenerator>();
builder.Services.AddSingleton<IGameConfig, FakeGameConfig>();

// Batcher pour regrouper les directions et émettre toutes les 3s
builder.Services.AddSingleton<DirectionBatcher>();


// Swagger/OpenAPI to test the endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SlideQuest.Client._Imports).Assembly);

// Map SignalR hub and the single API endpoint
app.MapHub<GameHub>("/hubs/game");
app.MapDirectionEndpoints();

app.Run();