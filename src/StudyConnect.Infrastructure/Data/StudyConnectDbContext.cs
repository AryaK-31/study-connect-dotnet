using Microsoft.EntityFrameworkCore;
using StudyConnect.Core.Entities;

namespace StudyConnect.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context (SQLite).
/// </summary>
public class StudyConnectDbContext : DbContext
{
    public StudyConnectDbContext(DbContextOptions<StudyConnectDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserInterest> UserInterests => Set<UserInterest>();

    public DbSet<UserAvailability> UserAvailabilities => Set<UserAvailability>();

    public DbSet<Connection> Connections => Set<Connection>();

    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<SessionAttendee> SessionAttendees => Set<SessionAttendee>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<LabSlot> LabSlots => Set<LabSlot>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();

    public DbSet<TeamMessage> TeamMessages => Set<TeamMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureSocial(modelBuilder);
        ConfigureSessions(modelBuilder);
        ConfigureAcademic(modelBuilder);
        ConfigureTeams(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Name).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(200).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.ContactNumber).HasMaxLength(30);
            entity.Property(u => u.Degree).HasMaxLength(120);
            entity.Property(u => u.Bio).HasMaxLength(500);
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasMany(u => u.Interests)
                .WithOne(i => i.User)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Availability)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Enrollments)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserInterest>(entity =>
        {
            entity.Property(i => i.Name).HasMaxLength(60).IsRequired();
            entity.HasIndex(i => new { i.UserId, i.Name }).IsUnique();
        });

        modelBuilder.Entity<UserAvailability>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.Day, a.Block }).IsUnique();
        });
    }

    private static void ConfigureSocial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasOne(c => c.Requester)
                .WithMany()
                .HasForeignKey(c => c.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Addressee)
                .WithMany()
                .HasForeignKey(c => c.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.RequesterId, c.AddresseeId }).IsUnique();
        });

        modelBuilder.Entity<DirectMessage>(entity =>
        {
            entity.Property(m => m.Text).HasMaxLength(2000).IsRequired();
            entity.Property(m => m.ConversationKey).HasMaxLength(40).IsRequired();
            entity.HasIndex(m => m.ConversationKey);

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.Title).HasMaxLength(150).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(500);
            entity.Property(n => n.Link).HasMaxLength(200);
            entity.HasIndex(n => new { n.UserId, n.IsRead });

            entity.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudySession>(entity =>
        {
            entity.Property(s => s.Title).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Topic).HasMaxLength(150).IsRequired();
            entity.Property(s => s.Location).HasMaxLength(150);
            entity.HasIndex(s => s.StartsAt);

            entity.HasOne(s => s.Creator)
                .WithMany()
                .HasForeignKey(s => s.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Subject)
                .WithMany()
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(s => s.Attendees)
                .WithOne(a => a.StudySession)
                .HasForeignKey(a => a.StudySessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionAttendee>(entity =>
        {
            entity.HasIndex(a => new { a.StudySessionId, a.UserId }).IsUnique();

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAcademic(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.Property(s => s.Code).HasMaxLength(20).IsRequired();
            entity.Property(s => s.Name).HasMaxLength(150).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(1000);
            entity.HasIndex(s => s.Code).IsUnique();

            entity.HasMany(s => s.Labs)
                .WithOne(l => l.Subject)
                .HasForeignKey(l => l.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Enrollments)
                .WithOne(e => e.Subject)
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LabSlot>(entity =>
        {
            entity.Property(l => l.Code).HasMaxLength(20).IsRequired();
            entity.Property(l => l.Room).HasMaxLength(60);
            entity.Property(l => l.Tutor).HasMaxLength(100);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.Property(e => e.Pitch).HasMaxLength(300);
            entity.HasIndex(e => new { e.UserId, e.SubjectId }).IsUnique();

            entity.HasOne(e => e.LabSlot)
                .WithMany()
                .HasForeignKey(e => e.LabSlotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTeams(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(60).IsRequired();
            entity.Property(t => t.ProjectIdea).HasMaxLength(500);

            entity.HasOne(t => t.Subject)
                .WithMany()
                .HasForeignKey(t => t.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.LabSlot)
                .WithMany()
                .HasForeignKey(t => t.LabSlotId)
                .OnDelete(DeleteBehavior.Restrict);

            // Leadership is transferred in code before a leader's account is deleted.
            entity.HasOne(t => t.Leader)
                .WithMany()
                .HasForeignKey(t => t.LeaderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(t => t.Members)
                .WithOne(m => m.Team)
                .HasForeignKey(m => m.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.Invitations)
                .WithOne(i => i.Team)
                .HasForeignKey(i => i.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.Messages)
                .WithOne(m => m.Team)
                .HasForeignKey(m => m.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamInvitation>(entity =>
        {
            entity.Property(i => i.Message).HasMaxLength(300);

            entity.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMessage>(entity =>
        {
            entity.Property(m => m.Text).HasMaxLength(2000).IsRequired();

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
