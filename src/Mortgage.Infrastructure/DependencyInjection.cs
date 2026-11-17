using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mortgage.Infrastructure.Queue;

namespace Mortgage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Trade-off: SQLite so a clone runs with nothing to install; SQL Server or PostgreSQL is a change to this line and the connection string.
        services.AddDbContext<MortgageDbContext>(options => options.UseSqlite(connectionString));
        // One instance for the whole app: the endpoints write to it and the worker reads from it.
        services.AddSingleton<IAssessmentQueue, ChannelAssessmentQueue>();
        return services;
    }
}
