using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"mortgage-test-{Guid.NewGuid():N}.db");
    private readonly Action<MortgageDbContext>? _seedBeforeStart;

    public ApiFactory() { }

    // Optional: rows to put in the database file before the host starts, to act like data left by an earlier run.
    public ApiFactory(Action<MortgageDbContext> seedBeforeStart) => _seedBeforeStart = seedBeforeStart;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        SeedDatabase();
        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the connection string while building,
        // before ConfigureAppConfiguration callbacks apply, so only a host setting reaches it.
        builder.UseSetting("ConnectionStrings:Mortgage", $"Data Source={_databasePath}");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Assessments:WorkerDelaySeconds"] = "0",
        }));
    }

    private void SeedDatabase()
    {
        if (_seedBeforeStart is null) return;
        using var db = new MortgageDbContext(new DbContextOptionsBuilder<MortgageDbContext>()
            .UseSqlite($"Data Source={_databasePath}").Options);
        db.Database.EnsureCreated();
        _seedBeforeStart(db);
        db.SaveChanges();
    }

    // The only way tests get a client, so adding authentication later changes this one method.
    public HttpClient CreateApiClient() => CreateClient();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        // Pooled connections keep the file locked until they are cleared.
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm", _databasePath + "-journal" })
            if (File.Exists(path)) File.Delete(path);
    }
}
