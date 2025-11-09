using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

// Force culture to fr-FR on the client for formatting, dates, numbers
CultureInfo fr = new("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = fr;
CultureInfo.DefaultThreadCurrentUICulture = fr;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();