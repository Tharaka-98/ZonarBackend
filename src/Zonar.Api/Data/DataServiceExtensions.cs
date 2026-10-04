using Microsoft.EntityFrameworkCore;
using Zonar.Api.Services;

namespace Zonar.Api.Data;

public static class DataServiceExtensions
{
    public static IServiceCollection AddZonarData(this IServiceCollection services, IConfiguration config)
    {
        // Resolved lazily so test hosts / environment variables can override it.
        // The provider is chosen from the connection string: SQLite for local development,
        // PostgreSQL (Neon, Supabase, Render, ...) in production. No code change between them.
        services.AddDbContext<ZonarDbContext>((sp, o) =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Zonar")
                     ?? DatabaseConnection.Default;

            if (DatabaseConnection.DetectProvider(cs) == DatabaseProvider.PostgreSql)
                o.UseNpgsql(DatabaseConnection.NormalisePostgres(cs));
            else
                o.UseSqlite(cs);
        });
        services.AddScoped<IContributionStore, EfContributionStore>();
        return services;
    }

    /// <summary>Creates the schema on first run and optionally seeds demo data.</summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZonarDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (app.Configuration.GetValue("Seed:DemoData", true))
        {
            var store = scope.ServiceProvider.GetRequiredService<IContributionStore>();
            if (!await store.AnyContributionsAsync(default))
                await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ContributionService>());
        }
    }
}
