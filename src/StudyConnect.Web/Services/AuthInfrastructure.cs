using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Entities;

namespace StudyConnect.Web.Services;

/// <summary>Builds the claims principal stored in the authentication cookie.</summary>
public static class UserPrincipalFactory
{
    public static ClaimsPrincipal Create(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Reads the user id claim (null when not signed in).</summary>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        string? value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : (int?)null;
    }
}

/// <summary>
/// Cookie events: on every request make sure the account in the cookie still exists
/// (e.g. after "Delete my account"), otherwise sign the browser out.
/// Demonstrates polymorphism by overriding a framework virtual method.
/// </summary>
public sealed class UserValidationCookieEvents : CookieAuthenticationEvents
{
    private readonly IAuthService _authService;

    public UserValidationCookieEvents(IAuthService authService)
    {
        _authService = authService;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        int? userId = context.Principal?.GetUserId();
        if (userId is null || !await _authService.UserExistsAsync(userId.Value))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

/// <summary>
/// Re-checks every few minutes that the user of a live Blazor circuit still exists
/// (overrides <see cref="RevalidatingServerAuthenticationStateProvider"/>).
/// </summary>
public sealed class RevalidatingUserAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RevalidatingUserAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        int? userId = authenticationState.User.GetUserId();
        if (userId is null)
        {
            return false;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IAuthService authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.UserExistsAsync(userId.Value, cancellationToken);
    }
}

/// <summary>Minimal API endpoints for the account area.</summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Logout must be a POST so that a link or image on another site cannot sign the user out.
        // The auth cookie is SameSite=Lax, so cross-site POSTs never carry it.
        endpoints.MapPost("/account/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/account/login?loggedOut=true");
        });

        return endpoints;
    }
}
