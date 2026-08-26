using Bluba.Prediction.UI.Components;
using Bluba.Prediction.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// El cliente vive en el servidor, así que las llamadas a BLUBA Predict API no
// pasan por el navegador y no dependen de que la API tenga CORS habilitado.
builder.Services.AddHttpClient<BlubaApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BlubaApi:BaseAddress"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("BlubaApi:TimeoutSeconds", 10));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
