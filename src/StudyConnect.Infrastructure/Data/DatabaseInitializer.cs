using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudyConnect.Core.Security;

namespace StudyConnect.Infrastructure.Data;

/// <summary>Creates the SQLite database on first run and fills it with demo data.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string seedDirectory, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider provider = scope.ServiceProvider;

        ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("StudyConnect.Database");
        IConfiguration configuration = provider.GetRequiredService<IConfiguration>();
        IDbContextFactory<StudyConnectDbContext> factory = provider.GetRequiredService<IDbContextFactory<StudyConnectDbContext>>();

        await using StudyConnectDbContext db = await factory.CreateDbContextAsync(cancellationToken);

        if (configuration.GetValue<bool>("Database:ResetOnStartup"))
        {
            logger.LogWarning("Database:ResetOnStartup is enabled - deleting the existing database.");
            await db.Database.EnsureDeletedAsync(cancellationToken);
        }

        bool created = await db.Database.EnsureCreatedAsync(cancellationToken);
        if (created)
        {
            logger.LogInformation("Created a new SQLite database.");
        }

        bool seedDemoData = configuration.GetValue("Database:SeedDemoData", true);
        if (seedDemoData && !await db.Users.AnyAsync(cancellationToken))
        {
            var seeder = new DatabaseSeeder(db, provider.GetRequiredService<IPasswordHasher>(), seedDirectory, logger);
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
