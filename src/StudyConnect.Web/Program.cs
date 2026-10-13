using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using StudyConnect.Infrastructure;
using StudyConnect.Infrastructure.Data;
using StudyConnect.Web.Components;
using StudyConnect.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

builder.Services.AddStudyConnectInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

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
