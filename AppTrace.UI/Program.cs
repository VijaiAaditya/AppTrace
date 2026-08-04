using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AppTrace.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Points to the AppTrace.Query.API base address. Override via wwwroot/appsettings.json
// ("QueryApiBaseAddress") per-environment when deploying.
var queryApiBaseAddress = builder.Configuration["QueryApiBaseAddress"] ?? "http://localhost:5000";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(queryApiBaseAddress) });

await builder.Build().RunAsync();
