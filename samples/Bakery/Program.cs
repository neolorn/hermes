using System;
using System.Text;
using System.Threading.Tasks;

using Bakery;

using Hermes;
using Hermes.Core;
using Hermes.Testing;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// A neighbourhood bakery's site: its customers' orders and the week's offer go out through the
// library, sent here by the in-memory providers of Hermes.Testing, with every endpoint group
// mapped under /notifications.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string connection = builder.Configuration.GetConnectionString("Bakery")
    ?? throw new InvalidOperationException("ConnectionStrings:Bakery names no database.");

builder.Services.AddHermesTesting(secrets => secrets.SetSecret(
    "connection",
    new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] }));
builder.Services.AddHermes(builder.Configuration, Notices.Declare);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IRecipientIdentity, CustomerIdentity>();
builder.Services.AddSingleton<Customers>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    // An API answers 401 and 403; it redirects to no sign-in page.
    options.Events.OnRedirectToLogin = context => Answer(context.Response, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context => Answer(context.Response, StatusCodes.Status403Forbidden);
});
builder.Services.AddAuthorizationBuilder().AddPolicy(ShopEndpoints.StaffPolicy, policy => policy.RequireRole(ShopEndpoints.StaffPolicy));

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapShop();
app.MapHermesPreferences("/notifications");
app.MapHermesPushSubscriptions("/notifications");
app.MapHermesUnsubscribe("/notifications");
app.MapHermesSmsReports("/notifications");

await app.RunAsync().ConfigureAwait(false);

static Task Answer(HttpResponse response, int status)
{
    response.StatusCode = status;
    return Task.CompletedTask;
}