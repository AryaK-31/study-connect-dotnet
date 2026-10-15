using StudyConnect.Core.Validation;

namespace StudyConnect.Infrastructure.Services;

/// <summary>Profile management.</summary>
public sealed class ProfileService : IProfileService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(IDbContextFactory<StudyConnectDbContext> dbFactory, ILogger<ProfileService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Users
            .AsNoTracking()
            .Include(u => u.Interests)
            .Include(u => u.Availability)
            .Include(u => u.Enrollments).ThenInclude(e => e.Subject)
            .Include(u => u.Enrollments).ThenInclude(e => e.LabSlot)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<OperationResult> UpdateProfileAsync(int userId, ProfileUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        // ----- Validation -----
        string name = (update.Name ?? string.Empty).Trim();
        if (name.Length < 2 || name.Length > 100)
        {
            return OperationResult.Failure("Name must be between 2 and 100 characters.");
        }

        HashSet<string> interests = update.Interests
            .Select(i => i.NormalizeTag())
            .Where(i => i.Length > 0)
            .ToHashSet();

        if (interests.Count == 0)
        {
            return OperationResult.Failure("Please select at least one interest.");
        }

        if (interests.Count > InputValidators.MaxInterests)
        {
            return OperationResult.Failure($"Please choose at most {InputValidators.MaxInterests} interests.");
        }

        if (interests.Any(i => i.Length > InputValidators.MaxInterestLength))
        {
            return OperationResult.Failure($"Interests must be {InputValidators.MaxInterestLength} characters or fewer.");
        }

        HashSet<AvailabilitySlot> availability = update.Availability.ToHashSet();
        if (availability.Count == 0)
        {
            return OperationResult.Failure("Please select at least one time you are available.");
        }

        if (!InputValidators.IsValidPhoneOrEmpty(update.ContactNumber))
        {
            return OperationResult.Failure("Please enter a valid contact number.");
        }

        // ----- Persistence -----
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        User? user = await db.Users
            .Include(u => u.Interests)
            .Include(u => u.Availability)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return OperationResult.Failure("Account not found.");
        }

        user.Name = name;
        user.ContactNumber = string.IsNullOrWhiteSpace(update.ContactNumber) ? null : update.ContactNumber.Trim();
        user.Degree = string.IsNullOrWhiteSpace(update.Degree) ? null : update.Degree.Trim().Truncate(120);
        user.Bio = string.IsNullOrWhiteSpace(update.Bio) ? null : update.Bio.Trim().Truncate(500);
        user.ProfileCompleted = true;

        // Apply only the differences so unique indexes are never violated.
        foreach (UserInterest removed in user.Interests.Where(i => !interests.Contains(i.Name)).ToList())
        {
            db.UserInterests.Remove(removed);
        }

        HashSet<string> existingInterests = user.Interests.Select(i => i.Name).ToHashSet();
        foreach (string added in interests.Where(i => !existingInterests.Contains(i)))
        {
            user.Interests.Add(new UserInterest { Name = added });
        }

        foreach (UserAvailability removed in user.Availability.Where(a => !availability.Contains(a.ToSlot())).ToList())
        {
            db.UserAvailabilities.Remove(removed);
        }

        HashSet<AvailabilitySlot> existingSlots = user.Availability.Select(a => a.ToSlot()).ToHashSet();
        foreach (AvailabilitySlot slot in availability.Where(s => !existingSlots.Contains(s)))
        {
            user.Availability.Add(new UserAvailability { Day = slot.Day, Block = slot.Block });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Profile updated for user {UserId}", userId);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAccountAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Failure("Account not found.");
        }

        // Teams led by this user: hand leadership to the longest-standing member, or delete empty teams.
        List<Team> ledTeams = await db.Teams
            .Include(t => t.Members)
            .Where(t => t.LeaderId == userId)
            .ToListAsync(cancellationToken);

        foreach (Team team in ledTeams)
        {
            TeamMember? successor = team.Members
                .Where(m => m.UserId != userId)
                .OrderBy(m => m.CreatedAtUtc)
                .FirstOrDefault();

            if (successor is null)
            {
                db.Teams.Remove(team);
            }
            else
            {
                team.LeaderId = successor.UserId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Everything else (sessions created, memberships, messages, connections ...) is removed by cascade delete.
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted their account", userId);
        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<string>> GetPopularInterestsAsync(int take = 16, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<string> names = await db.UserInterests
            .GroupBy(i => i.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Name)
            .Take(take)
            .Select(g => g.Name)
            .ToListAsync(cancellationToken);

        return names.Select(n => n.ToTitleCase()).ToList();
    }
}
