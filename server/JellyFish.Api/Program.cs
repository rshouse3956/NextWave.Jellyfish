using FluentValidation;
using JellyFish.Api.Api.Endpoints;
using JellyFish.Api.Common.Behaviors;
using JellyFish.Api.Infrastructure.Persistence;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using MediatR;
using Microsoft.Extensions.Options;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using JellyFish.Api.Infrastructure.Services.PatternAssistant;
using Microsoft.Extensions.AI;
using OllamaSharp;


var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Options
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));


// Schedule options
builder.Services.Configure<ScheduleSettings>(builder.Configuration.GetSection("Schedule"));

// Jellyfish Controller options
builder.Services.Configure<JellyfishControllerOptions>(builder.Configuration.GetSection("JellyfishController"));

// Pattern assistant (Ollama). Registered even when disabled; the handlers check PatternAssistant:Enabled.
builder.Services.Configure<PatternAssistantOptions>(builder.Configuration.GetSection(PatternAssistantOptions.SectionName));
builder.Services.AddHttpClient(PatternAssistantOptions.HttpClientName, (sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<PatternAssistantOptions>>().Value;
    http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds <= 0 ? 120 : o.TimeoutSeconds);
});
builder.Services.AddScoped<IChatClient>(sp =>
{
    var o = sp.GetRequiredService<IOptions<PatternAssistantOptions>>().Value;
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(PatternAssistantOptions.HttpClientName);
    return new OllamaApiClient(http, o.Model);
});

// MediatR + FluentValidation
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

// MongoDB EF Core DbContext
// Same settings (and database-name default) as IndexInitializer and the readiness check.
var mongo = builder.Configuration.GetSection(MongoSettings.SectionName).Get<MongoSettings>() ?? new MongoSettings();

builder.Services.AddDbContext<JellyfishDbContext>(options =>
    options.UseMongoDB(mongo.ConnectionString, mongo.DatabaseName));

// Hosted services
builder.Services.AddHostedService<IndexInitializer>();
builder.Services.AddHostedService<HolidaySeeder>();
builder.Services.AddHostedService<JellyFish.Api.Infrastructure.Services.Scheduling.SchedulePollingService>();

// UoW
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Repositories

// Services
builder.Services.AddScoped<IJellyfishControllerClient, JellyfishControllerClient>();

builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddScoped<IPatternRepository, PatternRepository>();
builder.Services.AddScoped<IPatternDetailRepository, PatternDetailRepository>();
builder.Services.AddScoped<IHolidayRepository, HolidayRepository>();
builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IActiveZoneRepository, ActiveZoneRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Domain services
builder.Services.AddSingleton<ISolarTimesService, SolarTimesService>();
builder.Services.AddSingleton<IScheduleNextEventService, ScheduleNextEventService>();

// Audit logging
builder.Services.AddScoped<JellyFish.Api.Infrastructure.Services.Auditing.IAuditLogService, JellyFish.Api.Infrastructure.Services.Auditing.AuditLogService>();

// Notifications / SSE broadcaster
builder.Services.AddSingleton<JellyFish.Api.Infrastructure.Notifications.IStateChangeBroadcaster, JellyFish.Api.Infrastructure.Notifications.StateChangeBroadcaster>();


// Manual override for controller patterns (highest priority)
builder.Services.AddSingleton<IManualOverrideService, ManualOverrideService>();

var app = builder.Build();

var swaggerEnabled = builder.Configuration.GetValue<bool?>("Swagger:Enabled") ?? false;
if (app.Environment.IsDevelopment() || swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints
app.MapHealthEndpoints();
app.MapHealthzEndpoints();
app.MapFolderEndpoints();
app.MapPatternEndpoints();
app.MapHolidayEndpoints();
app.MapEventsEndpoints();

app.MapScheduleEndpoints();
app.MapZoneEndpoints();
app.MapSolarTimesEndpoints();
app.MapActiveZoneEndpoints();

app.MapJellyfishControllerEndpoints();
app.MapAuditLogEndpoints();
app.MapPatternAssistantEndpoints();


app.UseDefaultFiles();
app.UseStaticFiles();

// Fallback to the Vue SPA for non-API, non-file routes
app.MapFallbackToFile("/index.html");

app.Run();
