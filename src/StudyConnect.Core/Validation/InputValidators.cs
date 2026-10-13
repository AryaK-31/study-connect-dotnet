using System.Text.RegularExpressions;

namespace StudyConnect.Core.Validation;

/// <summary>Validation helpers shared by the services and the UI.</summary>
public static class InputValidators
{
    public const int MaxInterests = 15;
    public const int MaxInterestLength = 40;
    public const int MaxMessageLength = 2000;

    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9 ()\-]{8,20}$", RegexOptions.Compiled);

    /// <summary>Same e-mail rule as the original Node.js backend.</summary>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 200 && EmailRegex.IsMatch(email.Trim());

    /// <summary>Phone numbers are optional, but when supplied they must look like a phone number.</summary>
    public static bool IsValidPhoneOrEmpty(string? phone) =>
        string.IsNullOrWhiteSpace(phone) || PhoneRegex.IsMatch(phone.Trim());

    /// <summary>Returns the e-mail in the canonical form stored in the database.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
