using Zonar.Api.Data;

namespace Zonar.Api.Tests;

public class DatabaseConnectionTests
{
    [Theory]
    [InlineData("Data Source=zonar.db", DatabaseProvider.Sqlite)]
    [InlineData("", DatabaseProvider.Sqlite)]
    [InlineData("postgresql://u:p@host/db", DatabaseProvider.PostgreSql)]
    [InlineData("postgres://u:p@host/db", DatabaseProvider.PostgreSql)]
    [InlineData("Host=localhost;Database=zonar", DatabaseProvider.PostgreSql)]
    public void Provider_is_detected_from_the_connection_string(string cs, DatabaseProvider expected)
        => Assert.Equal(expected, DatabaseConnection.DetectProvider(cs));

    [Fact]
    public void Neon_style_uri_is_converted_to_npgsql_keys()
    {
        var result = DatabaseConnection.NormalisePostgres(
            "postgresql://owner:secret@ep-cool-sun.ap-southeast-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");

        Assert.Contains("Host=ep-cool-sun.ap-southeast-1.aws.neon.tech;", result);
        Assert.Contains("Port=5432;", result);
        Assert.Contains("Database=neondb;", result);
        Assert.Contains("Username=owner;", result);
        Assert.Contains("Password=secret;", result);
        Assert.Contains("SSL Mode=require;", result);
        Assert.Contains("Channel Binding=require;", result);
    }

    [Fact]
    public void Non_default_port_is_kept()
        => Assert.Contains("Port=6543;", DatabaseConnection.NormalisePostgres("postgres://u:p@db.example.com:6543/mydb"));

    [Fact]
    public void Percent_encoded_password_is_decoded()
        => Assert.Contains("Password=p@ss:word;", DatabaseConnection.NormalisePostgres("postgres://user:p%40ss%3Aword@host/db"));

    [Fact]
    public void Tls_is_required_even_when_the_uri_omits_it()
        => Assert.Contains("SSL Mode=Require;", DatabaseConnection.NormalisePostgres("postgres://u:p@host/db"));

    [Fact]
    public void Key_value_connection_strings_pass_through_unchanged()
    {
        const string cs = "Host=localhost;Database=zonar;Username=postgres;Password=x";
        Assert.Equal(cs, DatabaseConnection.NormalisePostgres(cs));
    }
}
