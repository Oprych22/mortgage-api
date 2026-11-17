using System.Text.Json;
using Microsoft.AspNetCore.HttpLogging;
using Mortgage.Api.Applications;
using Mortgage.Api.Assessments;
using Mortgage.Api.Auth;
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
builder.Services.AddOptions<AffordabilityOptions>()
    .BindConfiguration(AffordabilityOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddHostedService<AssessmentWorker>();
// Method, path, status and duration only. Bodies are never logged: they hold the applicant's finances.
builder.Services.AddHttpLogging(options => options.LoggingFields =
    HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath |
    HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration);
builder.Services.AddHealthChecks().AddDbContextCheck<MortgageDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Trade-off: EnsureCreated builds the schema on first run so a clone needs nothing set up.
    // It never updates an existing database; migrations are the next step.
    scope.ServiceProvider.GetRequiredService<MortgageDbContext>().Database.EnsureCreated();
}

app.UseHttpLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
// Order matters: authentication reads the token and sets the user, then authorization decides using that user.
app.UseAuthentication();
app.UseAuthorization();

// On app, not the /api group, so it stays anonymous when authentication is added to /api.
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment()) app.MapDevTokenEndpoints();

var api = app.MapGroup("/api");
api.RequireAuthorization();
api.MapApplicationEndpoints();
api.MapAssessmentEndpoints();

app.Run();

public partial class Program;
