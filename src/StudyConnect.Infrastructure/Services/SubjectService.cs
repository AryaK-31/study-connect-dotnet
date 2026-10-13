namespace StudyConnect.Infrastructure.Services;

/// <summary>Subjects, lab classes and the student's enrolments.</summary>
public sealed class SubjectService : ISubjectService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;

    public SubjectService(IDbContextFactory<StudyConnectDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<Subject> subjects = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Labs)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        // Sort the labs in memory (Monday first, then by start time).
        foreach (Subject subject in subjects)
        {
            subject.Labs = subject.Labs
                .OrderBy(l => l.Day == DayOfWeek.Sunday ? 7 : (int)l.Day)
                .ThenBy(l => l.StartTime)
                .ToList();
        }

        return subjects;
    }

    public async Task<IReadOnlyList<Enrollment>> GetEnrollmentsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Subject)
            .Include(e => e.LabSlot)
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Subject.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<Enrollment>> SaveEnrollmentAsync(int userId, EnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Pitch is { Length: > 300 })
        {
            return Result<Enrollment>.Failure("Your pitch must be 300 characters or fewer.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        LabSlot? lab = await db.LabSlots.FirstOrDefaultAsync(l => l.Id == request.LabSlotId && l.SubjectId == request.SubjectId, cancellationToken);
        if (lab is null)
        {
            return Result<Enrollment>.Failure("Please choose a lab that belongs to this subject.");
        }

        Enrollment? enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SubjectId == request.SubjectId, cancellationToken);

        bool inTeam = await db.TeamMembers.AnyAsync(m => m.UserId == userId && m.Team.SubjectId == request.SubjectId, cancellationToken);

        if (enrollment is not null && enrollment.LabSlotId != request.LabSlotId && inTeam)
        {
            return Result<Enrollment>.Failure("You are in a team for this subject. Leave the team before changing labs (teams must be from the same lab).");
        }

        if (enrollment is null)
        {
            enrollment = new Enrollment { UserId = userId, SubjectId = request.SubjectId };
            db.Enrollments.Add(enrollment);
        }

        enrollment.LabSlotId = request.LabSlotId;
        enrollment.PreferredRole = request.PreferredRole;
        enrollment.Skill = request.Skill;
        enrollment.Goal = request.Goal;
        enrollment.LookingForTeam = request.LookingForTeam && !inTeam;
        enrollment.Pitch = string.IsNullOrWhiteSpace(request.Pitch) ? null : request.Pitch.Trim();

        await db.SaveChangesAsync(cancellationToken);
        return Result<Enrollment>.Success(enrollment);
    }

    public async Task<OperationResult> UnenrollAsync(int userId, int subjectId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Enrollment? enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.SubjectId == subjectId, cancellationToken);
        if (enrollment is null)
        {
            return OperationResult.Failure("You are not enrolled in this subject.");
        }

        if (await db.TeamMembers.AnyAsync(m => m.UserId == userId && m.Team.SubjectId == subjectId, cancellationToken))
        {
            return OperationResult.Failure("Leave your team for this subject before unenrolling.");
        }

        // Withdraw any pending invitations / join requests for this subject.
        List<TeamInvitation> pending = await db.TeamInvitations
            .Where(i => i.UserId == userId && i.Team.SubjectId == subjectId && i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        pending.ForEach(invitation =>
        {
            invitation.Status = InvitationStatus.Cancelled;
            invitation.RespondedAtUtc = DateTime.UtcNow;
        });

        db.Enrollments.Remove(enrollment);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }
}
