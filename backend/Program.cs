using ExpenseManagement.Api.Background;
using ExpenseManagement.Api.Middleware;
using ExpenseManagement.Api.Security;
using ExpenseManagement.Api.Services;
using ExpenseManagement.Api.Storage;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Data + domain services. DataStore is a singleton (it *is* the database here);
// the background queue must also be a singleton so producers and the worker share it.
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<PolicyEngine>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ReceiptStorage>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity: 100));
builder.Services.AddHostedService<QueuedHostedService>();

builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<ReportingService>();

// Custom bearer-token authentication (no third-party packages).
builder.Services
    .AddAuthentication(TokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
        TokenAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// CORS for the Vite dev server.
const string CorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---- Seed demo data ----
using (var scope = app.Services.CreateScope())
{
    DataSeeder.Seed(scope.ServiceProvider.GetRequiredService<DataStore>());
}

// ---- Pipeline ----
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
