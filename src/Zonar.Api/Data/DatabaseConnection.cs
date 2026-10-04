using System.Text;

namespace Zonar.Api.Data;

/// <summary>Which database engine the connection string points at.</summary>
public enum DatabaseProvider
{
    Sqlite,
    PostgreSql
}

/// <summary>
/// Works out which database to use from the connection string alone, so the same build runs
/// on SQLite locally and PostgreSQL in production with no code change.
/// </summary>
public static class DatabaseConnection
{
    public const string Default = "Data Source=zonar.db";

    public static DatabaseProvider DetectProvider(string connectionString)
    {
        var cs = (connectionString ?? "").Trim();

        if (cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            cs.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            cs.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            return DatabaseProvider.PostgreSql;

        return DatabaseProvider.Sqlite;
    }

    /// <summary>
    /// Hosted providers (Neon, Supabase, Render, Heroku) hand out URI-style connection strings
    /// like postgresql://user:pass@host/db?sslmode=require. Npgsql expects key=value pairs,
    /// so convert the URI form; anything else is passed through unchanged.
    /// </summary>
    public static string NormalisePostgres(string connectionString)
    {
        var cs = (connectionString ?? "").Trim();

        if (!cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return cs;

        var uri = new Uri(cs);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new StringBuilder()
            .Append("Host=").Append(uri.Host).Append(';')
            .Append("Port=").Append(uri.IsDefaultPort ? 5432 : uri.Port).Append(';')
            .Append("Database=").Append(Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))).Append(';')
            .Append("Username=").Append(Uri.UnescapeDataString(userInfo[0])).Append(';');

        if (userInfo.Length > 1)
            builder.Append("Password=").Append(Uri.UnescapeDataString(userInfo[1])).Append(';');

        // Carry over query parameters (sslmode, channel_binding, options, ...)
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var hasSslMode = false;
        foreach (var key in query.AllKeys.Where(k => k is not null))
        {
            var value = query[key];
            if (string.IsNullOrEmpty(value)) continue;

            switch (key!.ToLowerInvariant())
            {
                case "sslmode":
                    hasSslMode = true;
                    builder.Append("SSL Mode=").Append(value).Append(';');
                    break;
                case "channel_binding":
                    builder.Append("Channel Binding=").Append(value).Append(';');
                    break;
                default:
                    builder.Append(key).Append('=').Append(value).Append(';');
                    break;
            }
        }

        // Managed Postgres (Neon, Supabase, Render) always requires TLS.
        if (!hasSslMode) builder.Append("SSL Mode=Require;");

        return builder.ToString();
    }
}
