using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Matching;
using StudyConnect.Core.Security;
using StudyConnect.Core.Validation;
using StudyConnect.Infrastructure.Data;
using StudyConnect.Infrastructure.Email;
using StudyConnect.Infrastructure.Realtime;
using StudyConnect.Infrastructure.Services;

namespace StudyConnect.Infrastructure;

/// <summary>
/// Extension method that registers every infrastructure service with the DI container,
/// so the web project only needs a single line: <c>builder.Services.AddStudyConnectInfrastructure(...)</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStudyConnectInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        // ----- Database (SQLite file under App_Data) -----
        string connectionString = BuildSqliteConnectionString(
            configuration.GetConnectionString("StudyConnect") ?? "Data Source=App_Data/studyconnect.db",
            contentRootPath);

        // A factory gives each operation its own short-lived DbContext, which is the
        // recommended pattern for Blazor Server (DbContext is not thread-safe).
        services.AddDbContextFactory<StudyConnectDbContext>(options => options.UseSqlite(connectionString));

        // ----- Stateless domain services (singletons) -----
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>(_ => new Pbkdf2PasswordHasher());
        services.AddSingleton<IPasswordPolicy>(_ => new PasswordPolicy());
        services.AddSingleton<ICompatibilityEngine>(_ => new CompatibilityEngine());
        services.AddSingleton<ITeamBuilder>(provider => new TeamBuilder(provider.GetRequiredService<ICompatibilityEngine>()));
        services.AddSingleton<IRealtimeNotifier, InMemoryRealtimeNotifier>();

        // ----- E-mail: real SMTP when configured, otherwise write e-mails to an outbox folder -----
        EmailOptions emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        if (!Path.IsPathRooted(emailOptions.OutboxDirectory))
        {
            emailOptions.OutboxDirectory = Path.Combine(contentRootPath, emailOptions.OutboxDirectory);
        }

        services.AddSingleton(emailOptions);
        if (emailOptions.IsSmtpConfigured)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, FileOutboxEmailSender>();
        }

        // ----- Application services (one instance per Blazor circuit / HTTP request) -----
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IStudySessionService, StudySessionService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IInsightsService, InsightsService>();

        return services;
    }

    /// <summary>Turns a relative SQLite path into an absolute one under the content root and creates its folder.</summary>
    public static string BuildSqliteConnectionString(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        string dataSource = builder.DataSource;

        if (!string.IsNullOrWhiteSpace(dataSource)
            && dataSource != ":memory:"
            && !Path.IsPathRooted(dataSource))
        {
            builder.DataSource = Path.Combine(contentRootPath, dataSource);
        }

        string? directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return builder.ToString();
    }
}
