using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Common;
using StudyConnect.Core.Entities;
using StudyConnect.Core.Models;
using StudyConnect.Core.Security;
using StudyConnect.Core.Validation;
using StudyConnect.Infrastructure.Data;

namespace StudyConnect.Infrastructure.Services;

/// <summary>
/// Registration, login and password reset. Cookie sign-in is handled in the web layer.
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);
    private const string GenericResetMessage = "If an account exists for that e-mail, a reset link has been sent.";

    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly IPasswordHasher _hasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDbContextFactory<StudyConnectDbContext> dbFactory,
        IPasswordHasher hasher,
        IPasswordPolicy passwordPolicy,
        IEmailSender emailSender,
        ILogger<AuthService> logger)
    {
        _dbFactory = dbFactory;
        _hasher = hasher;
        _passwordPolicy = passwordPolicy;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Result<User>> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = (request.Name ?? string.Empty).Trim();
        string email = InputValidators.NormalizeEmail(request.Email);

        if (name.Length < 2 || name.Length > 100)
        {
            return Result<User>.Failure("Please enter your full name (2-100 characters).");
        }

        if (!InputValidators.IsValidEmail(email))
        {
            return Result<User>.Failure("Invalid email format.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return Result<User>.Failure("Passwords do not match.");
        }

        PasswordValidationResult passwordCheck = _passwordPolicy.Validate(request.Password);
        if (!passwordCheck.IsValid)
        {
            return Result<User>.Failure(string.Join(". ", passwordCheck.Errors) + ".");
        }

        if (!InputValidators.IsValidPhoneOrEmpty(request.ContactNumber))
        {
            return Result<User>.Failure("Please enter a valid contact number.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result<User>.Failure("Email already registered.");
        }

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            ContactNumber = string.IsNullOrWhiteSpace(request.ContactNumber) ? null : request.ContactNumber.Trim(),
            LastSeenUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.Notifications.Add(new Notification
        {
            User = user,
            Type = NotificationType.System,
            Title = "Welcome to StudyConnect 👋",
            Body = "Complete your profile, then enrol in a subject to start finding team mates from your lab.",
            Link = "/profile"
        });

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("New user {UserId} registered", user.Id);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = InputValidators.NormalizeEmail(email);
        if (!InputValidators.IsValidEmail(normalizedEmail))
        {
            return Result<User>.Failure("Invalid email format.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Same message for unknown e-mail and wrong password so attackers cannot enumerate accounts.
        if (user is null || !_hasher.Verify(password ?? string.Empty, user.PasswordHash))
        {
            _logger.LogInformation("Failed login attempt for {Email}", normalizedEmail);
            return Result<User>.Failure("Invalid email or password.");
        }

        user.LastSeenUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result<User>.Success(user);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string resetPageUrl, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = InputValidators.NormalizeEmail(email);
        if (!InputValidators.IsValidEmail(normalizedEmail))
        {
            return new PasswordResetRequestResult(false, "Invalid email format.", null);
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // Do not reveal whether the e-mail exists.
            return new PasswordResetRequestResult(true, GenericResetMessage, null);
        }

        string token = SecureTokens.Create();
        user.PasswordResetTokenHash = SecureTokens.Hash(token);
        user.PasswordResetExpiresUtc = DateTime.UtcNow.Add(ResetTokenLifetime);
        await db.SaveChangesAsync(cancellationToken);

        string resetLink = $"{resetPageUrl}?token={Uri.EscapeDataString(token)}";

        try
        {
            await _emailSender.SendAsync(new EmailMessage(
                user.Email,
                "Password Reset Request - StudyConnect",
                $"<p>Hello {WebUtility.HtmlEncode(user.Name)},</p>" +
                "<p>You requested a password reset. Click the button below to choose a new password.</p>" +
                $"<p><a href=\"{resetLink}\" style=\"background:#4f46e5;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none\">Reset password</a></p>" +
                "<p>This link expires in 30 minutes. If you did not request it, you can ignore this e-mail.</p>",
                $"Reset your password: {resetLink}"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send password reset e-mail to {Email}", user.Email);
            return new PasswordResetRequestResult(false, "We could not send the reset e-mail. Please try again later.", null);
        }

        // In development (no SMTP server) show the link on screen so the feature can be demonstrated.
        string? developmentLink = _emailSender.DeliversExternally ? null : resetLink;
        return new PasswordResetRequestResult(true, GenericResetMessage, developmentLink);
    }

    public async Task<OperationResult> ResetPasswordAsync(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OperationResult.Failure("The reset link is invalid.");
        }

        if (newPassword != confirmPassword)
        {
            return OperationResult.Failure("Passwords do not match.");
        }

        PasswordValidationResult passwordCheck = _passwordPolicy.Validate(newPassword);
        if (!passwordCheck.IsValid)
        {
            return OperationResult.Failure(string.Join(". ", passwordCheck.Errors) + ".");
        }

        string tokenHash = SecureTokens.Hash(token.Trim());
        DateTime now = DateTime.UtcNow;

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        User? user = await db.Users.FirstOrDefaultAsync(
            u => u.PasswordResetTokenHash == tokenHash && u.PasswordResetExpiresUtc > now,
            cancellationToken);

        if (user is null)
        {
            return OperationResult.Failure("Invalid or expired reset link. Please request a new one.");
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset for user {UserId}", user.Id);

        try
        {
            await _emailSender.SendAsync(new EmailMessage(
                user.Email,
                "Password Reset Successful - StudyConnect",
                $"<p>Hello {WebUtility.HtmlEncode(user.Name)},</p><p>Your password has been reset successfully.</p>" +
                "<p>If you did not make this change, please contact support immediately.</p>"), cancellationToken);
        }
        catch (Exception ex)
        {
            // The password was changed - a failed confirmation e-mail should not undo that.
            _logger.LogWarning(ex, "Could not send password reset confirmation e-mail");
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (newPassword != confirmPassword)
        {
            return OperationResult.Failure("New passwords do not match.");
        }

        PasswordValidationResult passwordCheck = _passwordPolicy.Validate(newPassword);
        if (!passwordCheck.IsValid)
        {
            return OperationResult.Failure(string.Join(". ", passwordCheck.Errors) + ".");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Failure("Account not found.");
        }

        if (!_hasher.Verify(currentPassword ?? string.Empty, user.PasswordHash))
        {
            return OperationResult.Failure("Your current password is incorrect.");
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    public async Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
    }
}
