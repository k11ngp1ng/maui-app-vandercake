using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VanderCake.Infrastructure.Persistence;

public static class DatabaseSettings
{
    public static string DefaultConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VanderCake", "vandercake.db"),
        ForeignKeys = true
    }.ToString();

    public static void PrepareDirectory(string connectionString)
    {
        var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(source) || source == ":memory:") return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(source))!);
    }
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VanderCakeDbContext>
{
    public VanderCakeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__VanderCake")
            ?? DatabaseSettings.DefaultConnectionString;
        DatabaseSettings.PrepareDirectory(connection);
        return new(new DbContextOptionsBuilder<VanderCakeDbContext>().UseSqlite(connection).Options);
    }
}
