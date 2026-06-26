using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TraderAlgoApi.Data;

/// <summary>
/// Allows EF Core CLI tools (dotnet ef migrations add/update) to create
/// MlflowDbContext without requiring the full application DI to resolve.
/// </summary>
public sealed class MlflowDbContextFactory : IDesignTimeDbContextFactory<MlflowDbContext>
{
    public MlflowDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<MlflowDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Mlflow") ??
            configuration.GetConnectionString("Supabase") ??
            throw new InvalidOperationException("No connection string found for 'Mlflow' or 'Supabase'.");

        var optionsBuilder = new DbContextOptionsBuilder<MlflowDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MlflowDbContext(optionsBuilder.Options);
    }
}
