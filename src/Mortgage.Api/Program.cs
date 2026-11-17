using System.Text.Json;
using Mortgage.Api.Applications;
using Mortgage.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // The built-in validation names errors after the C# property (MonthlyExpenses) but the domain uses
    // the JSON name (depositAmount). Lower-casing the first letter gives clients one format for both.
    if (context.ProblemDetails is HttpValidationProblemDetails validation)
    {
        var errors = validation.Errors.ToDictionary(
            e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value);
        validation.Errors.Clear();
        foreach (var (key, messages) in errors) validation.Errors[key] = messages;
    }
});
builder.Services.AddValidation();
// In Development a bad request body would otherwise throw and the exception page would turn it into a 500.
// Malformed JSON is the client's mistake, so it must be a 400 in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Mortgage")!);
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Trade-off: EnsureCreated builds the schema on first run so a clone needs nothing set up.
    // It never updates an existing database; migrations are the next step.
    scope.ServiceProvider.GetRequiredService<MortgageDbContext>().Database.EnsureCreated();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

var api = app.MapGroup("/api");
api.MapApplicationEndpoints();

app.Run();

public partial class Program;
