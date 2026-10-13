using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using StudyConnect.Infrastructure;
using StudyConnect.Infrastructure.Data;
using StudyConnect.Web.Components;
using StudyConnect.Web.Services;

// =============================================================================
// StudyConnect - ASP.NET Core Blazor Web App (.NET 9)
// Replaces the Node.js (Express + Apollo GraphQL + Socket.io) backend and the
// React front-end of the original project with a single C# full-stack application.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// ----- Blazor (interactive server rendering over SignalR) -----
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ----- Authentication: secure, HTTP-only cookie (replaces JWT in localStorage) -----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingUserAuthenticationStateProvider>();
builder.Services.AddScoped<UserValidationCookieEvents>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "StudyConnect.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.EventsType = typeof(UserValidationCookieEvents);
    });

builder.Services.AddAuthorization();

// ----- Data, domain and application services (see Infrastructure/DependencyInjection.cs) -----
builder.Services.AddStudyConnectInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

// ----- UI services -----
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

// Create the SQLite database and load the demo data (JSON files) on first run.
await DatabaseInitializer.InitializeAsync(app.Services, Path.Combine(app.Environment.ContentRootPath, "Data", "Seed"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();

app.Run();
