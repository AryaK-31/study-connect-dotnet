using System.ComponentModel.DataAnnotations;
using StudyConnect.Core.Enums;

namespace StudyConnect.Web.Models;

// ---------------------------------------------------------------------------
// Form models bound by Blazor <EditForm>. DataAnnotations attributes give instant
// client-facing validation messages; the services validate again on the server side.
// ---------------------------------------------------------------------------

public sealed class LoginInput
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public sealed class SignupInput
{
    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^\+?[0-9 ()\-]{8,20}$", ErrorMessage = "Enter a valid phone number (8-20 digits).")]
    public string? ContactNumber { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ForgotPasswordInput
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordInput
{
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordInput
{
    [Required(ErrorMessage = "Enter your current password.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm the new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ProfileFormModel
{
    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [RegularExpression(@"^\+?[0-9 ()\-]{8,20}$", ErrorMessage = "Enter a valid phone number (8-20 digits).")]
    public string? ContactNumber { get; set; }

    [StringLength(120)]
    public string? Degree { get; set; }

    [StringLength(500, ErrorMessage = "Bio must be 500 characters or fewer.")]
    public string? Bio { get; set; }
}

public sealed class SessionFormModel
{
    [Required(ErrorMessage = "Give the session a title.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "What will you study?")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Topic must be between 2 and 150 characters.")]
    public string Topic { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; } = DateTime.Today.AddDays(1).AddHours(14);

    [Range(15, 480, ErrorMessage = "Duration must be between 15 and 480 minutes.")]
    public int DurationMinutes { get; set; } = 60;

    [Required(ErrorMessage = "Where will you meet?")]
    [StringLength(150)]
    public string Location { get; set; } = "Online";

    public SessionMode Mode { get; set; } = SessionMode.Online;

    [Range(2, 100, ErrorMessage = "Capacity must be between 2 and 100.")]
    public int Capacity { get; set; } = 6;

    /// <summary>0 means "no specific subject".</summary>
    public int SubjectId { get; set; }
}

public sealed class EnrollmentFormModel
{
    public int SubjectId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please choose your lab.")]
    public int LabSlotId { get; set; }

    public TeamRole PreferredRole { get; set; } = TeamRole.FullStack;

    public SkillLevel Skill { get; set; } = SkillLevel.Intermediate;

    public TargetGrade Goal { get; set; } = TargetGrade.Distinction;

    public bool LookingForTeam { get; set; } = true;

    [StringLength(300, ErrorMessage = "Keep your pitch under 300 characters.")]
    public string? Pitch { get; set; }
}

public sealed class TeamFormModel
{
    [Required(ErrorMessage = "Give your team a name.")]
    [StringLength(60, MinimumLength = 3, ErrorMessage = "Team name must be between 3 and 60 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ProjectIdea { get; set; }

    [Range(2, 6)]
    public int MaxMembers { get; set; } = 3;

    public bool IsOpen { get; set; } = true;
}
