using System.Text.Json.Serialization;
using Clients.Api;
using Clients.Api.Clients;
using Clients.Api.Clients.Risk;
using Clients.Api.Extensions;
using OpenTelemetry.Trace;
using Microsoft.EntityFrameworkCore;
using RiskEvaluator;
using OpenTelemetry.Resources;
using System.Reflection;
using Npgsql;
using StackExchange.Redis;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<ClientsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ClientsDb")));

IConnectionMultiplexer connectionMultiplexer = ConnectionMultiplexer.Connect(
   builder.Configuration.GetConnectionString("ClientsCache")!);
builder.Services.AddSingleton(connectionMultiplexer);
builder.Services.AddStackExchangeRedisCache(options =>
    options.ConnectionMultiplexerFactory = () => Task.FromResult(connectionMultiplexer));

builder.Services.AddSingleton<IRiskValidator, RiskValidator>();

builder.Services.AddGrpcClient<Evaluator.EvaluatorClient>(options =>
{
    options.Address = new Uri(builder.Configuration["RiskEvaluator:Url"]!);
});

builder.AddRabbitMq();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.AddHealthChecksConfiguration();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Clients.Api",
    serviceNamespace: "Dometrain.Courses.OpenTelemetry",
    serviceVersion: Assembly.GetExecutingAssembly().GetName().Version!.ToString()
     )
    .AddAttributes(new[]
        {
            new KeyValuePair<string, object>("Service.Version",
            Assembly.GetExecutingAssembly().GetName().Version!.ToString())
        })
    )
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation()
    .AddNpgsql()
    .AddRedisInstrumentation()
    .AddConsoleExporter());

var app = builder.Build();

// For demo purposes, ensure the database is created
// On a production application, this should not be a responsibility of the application
EnsureDbCreated(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/healthz");
app.MapGet("/", () => "Clients.API");
app.MapClients();

app.Run();


static void EnsureDbCreated(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var context = scopedServices.GetRequiredService<ClientsDbContext>();
    context.Database.EnsureCreated();
}