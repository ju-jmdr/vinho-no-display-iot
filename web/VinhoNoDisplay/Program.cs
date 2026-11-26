using System.Globalization;
using Microsoft.AspNetCore.Localization;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
ConfiguracaoFiware.Iniciar(builder.Configuration, builder.Environment.ContentRootPath);
ConfiguracaoSupervisao.Iniciar(builder.Configuration);
builder.Services.AddHostedService<SupervisorService>();   // analisa as médias e manda o comando de alerta

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");

// Cultura fixa en-US: números com ponto, igual ao JSON do FIWARE
var cultura = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { cultura },
    SupportedUICultures = new[] { cultura }
});

app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.Run();
