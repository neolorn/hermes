using System;
using System.Text;
using System.Threading.Tasks;

using Bakery;

using Hermes;
using Hermes.Conversations;
using Hermes.Core;
using Hermes.Development;
using Hermes.Testing;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// A neighbourhood bakery's site: its customers' orders and the week's offer go out through the
// library, with the engine's groups and the staff's conversations mapped under /notifications,
// and the customers' support conversations under /help, each with its hub at /live; customers
// and staff attach files, kept in memory and scanned by a fixed scanner from Hermes.Testing,
// which a deployment replaces with its object storage and its scanner. Run in Development, it keeps
// every message in the development inbox instead of sending it, readable at /dev/inbox/; its
// keys are the fixed ones of Hermes.Testing, which a deployment replaces with its own secret
// source, as it registers the providers it sends through.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string connection = builder.Configuration.GetConnectionString("Bakery")
    ?? throw new InvalidOperationException("ConnectionStrings:Bakery names no database.");

FixedSecretSource secrets = new();
secrets.SetSecret("connection", new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] });
builder.Services.AddSingleton<ISecretSource>(secrets);
builder.AddHermesDevelopmentInbox();
builder.Services.AddHermes(builder.Configuration, Notices.Declare);
builder.Services.AddHermesConversations();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IRecipientIdentity, CustomerIdentity>();
builder.Services.AddSingleton<Customers>();
builder.Services.AddSingleton<IConversationMembership, StaffMembership>();
builder.Services.AddSingleton<IAttachmentStore, InMemoryAttachmentStore>();
builder.Services.AddSingleton<IAttachmentScanner>(new FixedAttachmentScanner());

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
app.MapHermesInApp("/notifications");
app.MapHermesStaffConversations("/notifications").RequireAuthorization(ShopEndpoints.StaffPolicy);
app.MapHermesPublicConversations("/help");
app.MapHermesDevelopmentInbox("/dev/inbox");

await app.RunAsync().ConfigureAwait(false);

static Task Answer(HttpResponse response, int status)
{
    response.StatusCode = status;
    return Task.CompletedTask;
}