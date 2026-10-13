namespace StudyConnect.Web.Services;

/// <summary>Protects against open-redirect attacks when following a returnUrl.</summary>
public static class RedirectHelper
{
    public static string SafeLocalUrl(string? url, string fallback = "/")
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return fallback;
        }

        bool isLocal = url.StartsWith('/')
                       && !url.StartsWith("//", StringComparison.Ordinal)
                       && !url.StartsWith("/\\", StringComparison.Ordinal);

        // Never send the user back to an account page after logging in.
        bool isAccountPage = url.StartsWith("/account", StringComparison.OrdinalIgnoreCase);

        return isLocal && !isAccountPage ? url : fallback;
    }
}
