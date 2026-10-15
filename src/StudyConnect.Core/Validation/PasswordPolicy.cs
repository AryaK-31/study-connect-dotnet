namespace StudyConnect.Core.Validation;

/// <summary>Rules a password must satisfy.</summary>
public sealed record PasswordRequirements(
    int MinLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireSpecialCharacter)
{
    public static PasswordRequirements Default { get; } = new(8, true, true, true, true);
}

/// <summary>Result of validating a password.</summary>
/// <param name="IsValid">True when every enabled rule passes.</param>
/// <param name="Errors">Messages for the rules that failed.</param>
/// <param name="Strength">Number of rules satisfied (0-5), used for the strength meter.</param>
public sealed record PasswordValidationResult(bool IsValid, IReadOnlyList<string> Errors, int Strength)
{
    public string StrengthLabel => Strength switch
    {
        <= 1 => "Very weak",
        2 => "Weak",
        3 => "Fair",
        4 => "Good",
        _ => "Strong"
    };
}

/// <summary>Validates passwords against a set of requirements.</summary>
public interface IPasswordPolicy
{
    PasswordRequirements Requirements { get; }

    PasswordValidationResult Validate(string? password);
}

/// <summary>Default password policy.</summary>
public sealed class PasswordPolicy : IPasswordPolicy
{
    public const string SpecialCharacters = "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?~`";

    private readonly List<(bool Enabled, Func<string, bool> Passes, string Message)> _rules;

    /// <summary>Creates a policy with <see cref="PasswordRequirements.Default"/>.</summary>
    public PasswordPolicy()
        : this(PasswordRequirements.Default)
    {
    }

    /// <summary>Creates a policy with custom requirements.</summary>
    public PasswordPolicy(PasswordRequirements requirements)
    {
        Requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));

        _rules = new List<(bool Enabled, Func<string, bool> Passes, string Message)>
        {
            (true, p => p.Length >= requirements.MinLength, $"Password must be at least {requirements.MinLength} characters long"),
            (requirements.RequireUppercase, p => p.Any(char.IsUpper), "Password must contain at least one uppercase letter"),
            (requirements.RequireLowercase, p => p.Any(char.IsLower), "Password must contain at least one lowercase letter"),
            (requirements.RequireDigit, p => p.Any(char.IsDigit), "Password must contain at least one number"),
            (requirements.RequireSpecialCharacter, p => p.Any(c => SpecialCharacters.Contains(c)), "Password must contain at least one special character (!@#$%^&*...)")
        };
    }

    public PasswordRequirements Requirements { get; }

    public PasswordValidationResult Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordValidationResult(false, new[] { "Password is required" }, 0);
        }

        string value = password;

        List<string> errors = _rules
            .Where(rule => rule.Enabled && !rule.Passes(value))
            .Select(rule => rule.Message)
            .ToList();

        int strength = _rules.Count(rule => rule.Passes(value));

        return new PasswordValidationResult(errors.Count == 0, errors, strength);
    }
}
