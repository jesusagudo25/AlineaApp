using Npgsql;

namespace Alinea.Api.Infrastructure;

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public bool ApplyMigrationsOnStartup { get; set; }

    public static bool IsValid(DatabaseOptions options)
    {
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
            return !string.IsNullOrWhiteSpace(connection.Host)
                && !string.IsNullOrWhiteSpace(connection.Database)
                && !string.IsNullOrWhiteSpace(connection.Username)
                && !string.IsNullOrWhiteSpace(connection.Password)
                && !connection.IncludeErrorDetail && !connection.LogParameters;
        }
        catch (ArgumentException) { return false; }
    }
}

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];

    public static bool IsValid(CorsOptions options) => options.AllowedOrigins.All(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.GetLeftPart(UriPartial.Authority) == origin
        && string.IsNullOrEmpty(uri.UserInfo));
}
