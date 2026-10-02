using Microsoft.EntityFrameworkCore;
using Zonar.Api.Services;

namespace Zonar.Api.Data;

public static class DataServiceExtensions
{
    public static IServiceCollection AddZonarData(this IServiceCollection services, IConfiguration config)
    {
        // Resolved lazily so test hosts / environment variables can override it.
        services.AddDbContext<ZonarDbContext>((sp, o) => o.UseSqlite(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Zonar") ?? "Data Source=zonar.db"));
        services.AddScoped<IContributionStore, EfContributionStore>();
        return services;
    }

    /// <summary>Creates the SQLite schema on first run and optionally seeds demo data.</summary>
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
