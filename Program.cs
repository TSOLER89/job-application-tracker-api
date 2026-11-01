using System.Data;
using System.Text.Json;
using System.Threading.RateLimiting;
using job_application_tracker_api.Auth;
using job_application_tracker_api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddErrorDescriber<SwedishIdentityErrors>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "JobTrack.Csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var keys = builder.Services.AddDataProtection().PersistKeysToFileSystem(
    new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
if (OperatingSystem.IsWindows()) keys.ProtectKeysWithDpapi();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = (context, _) => new ValueTask(Results.Problem(statusCode: 429,
        title: "För många försök. Vänta en minut och försök igen.").ExecuteAsync(context.HttpContext));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("session", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "Opaque session token",
        Description = "Logga in med X-Auth-Mode: bearer och klistra in accessToken här."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("session", document)] = []
    });
    options.OperationFilter<AnonymousAuthOperationFilter>();
});
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("ReactApp", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Local maintenance commands never expose ownership assignment through an HTTP endpoint.
if (builder.Configuration.GetValue<bool>("backup-legacy"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await using var connection = database.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT Id, Company, Position, Location, DateApplied, Status, Notes, ImageUrl FROM JobApplications ORDER BY Id";
    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<Dictionary<string, object?>>();
    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object?>();
        for (var index = 0; index < reader.FieldCount; index++)
            row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
        rows.Add(row);
    }
    var directory = Path.Combine(app.Environment.ContentRootPath, "bin", "Backups");
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, $"applications-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
    await System.IO.File.WriteAllTextAsync(file, JsonSerializer.Serialize(rows));
    Console.WriteLine($"Backed up {rows.Count} applications to {file}");
    return;
}
if (builder.Configuration["assign-legacy-to"] is { Length: > 0 } ownerEmail)
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var owner = await users.FindByEmailAsync(ownerEmail.Trim());
    if (owner is null) throw new InvalidOperationException("Create the owner's account first. No records were assigned.");
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var count = await database.JobApplications.Where(application => application.UserId == null)
        .ExecuteUpdateAsync(setters => setters.SetProperty(application => application.UserId, owner.Id));
    Console.WriteLine($"Assigned {count} preserved applications to the selected local account.");
    return;
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest ? badRequest.StatusCode : 500
});
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else app.UseHttpsRedirection();

// Uploads are served by the authenticated ImagesController, not public static-file middleware.
app.UseRouting();
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var request = context.Request;
    var unsafeMethod = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);
    var bearer = request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    var nativeLogin = (request.Path == "/api/auth/login" || request.Path == "/api/auth/register") && request.Headers["X-Auth-Mode"] == "bearer";
    if (unsafeMethod && !bearer && !nativeLogin)
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            await Results.Problem(statusCode: 400, title: "Säkerhetskontrollen misslyckades. Ladda om sidan och försök igen.").ExecuteAsync(context);
            return;
        }
    }
    await next(context);
});
app.MapControllers();
app.Run();
