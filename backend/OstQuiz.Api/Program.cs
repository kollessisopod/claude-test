using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Data;
using OstQuiz.Api.Features.Admin;
using OstQuiz.Api.Features.Games;
using OstQuiz.Api.Features.Puzzles;
using OstQuiz.Api.Services.Audio;
using OstQuiz.Api.Services.Rawg;
using OstQuiz.Api.Services.Security;
using OstQuiz.Api.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration / options ---
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<RawgOptions>(builder.Configuration.GetSection(RawgOptions.SectionName));
builder.Services.Configure<StepTokenOptions>(builder.Configuration.GetSection(StepTokenOptions.SectionName));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RawgOptions>>().Value);

// --- Database ---
var connStr = builder.Configuration.GetConnectionString("Postgres")
              ?? "Host=localhost;Database=ostquiz;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr));

// --- Object storage (MinIO via AWS SDK, path-style) ---
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
    var cfg = new AmazonS3Config
    {
        ServiceURL = o.ServiceUrl,
        ForcePathStyle = o.ForcePathStyle,
        AuthenticationRegion = "us-east-1",
    };
    return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), cfg);
});
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

// --- RAWG ---
builder.Services.AddHttpClient<RawgClient>();
builder.Services.AddScoped<RawgImportService>();

// --- Audio clip generation ---
builder.Services.AddSingleton<IAudioClipper, FfmpegAudioClipper>();
builder.Services.AddScoped<PuzzleClipService>();

// --- Feature services ---
builder.Services.AddSingleton<StepTokenService>();
builder.Services.AddScoped<PuzzleService>();

// --- CORS for the SPA frontends ---
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// --- Migrate + seed on startup (dev convenience) ---
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var db = sp.GetRequiredService<AppDbContext>();

    await WaitForDatabaseAsync(db, logger);
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        var storage = sp.GetRequiredService<IObjectStorage>();
        var clipSvc = sp.GetRequiredService<PuzzleClipService>();
        await DbSeeder.SeedAsync(db, storage, clipSvc, logger);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapGameEndpoints();
app.MapPuzzleEndpoints();
app.MapAdminEndpoints();

app.Run();

static async Task WaitForDatabaseAsync(AppDbContext db, ILogger logger)
{
    const int maxAttempts = 30;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            if (await db.Database.CanConnectAsync()) return;
        }
        catch
        {
            // swallow until the database is reachable
        }
        logger.LogInformation("Waiting for database... ({Attempt}/{Max})", attempt, maxAttempts);
        await Task.Delay(2000);
    }
}
