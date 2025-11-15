using System.Globalization;
using GameConfig;
using GridGenerator;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SlideQuest.Client.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

// Force culture to fr-FR on the client for formatting, dates, numbers
CultureInfo fr = new("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = fr;
CultureInfo.DefaultThreadCurrentUICulture = fr;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// SignalR hub client DI (concrete registered for interface)
builder.Services.AddSingleton<IGameHubService, GameHubService>();

builder.Services.AddSingleton<IGridGenerator, GridGenerator.GridGenerator>();
builder.Services.AddSingleton<IGameConfig, GameConfig.GameConfig>();

await builder.Build().RunAsync();