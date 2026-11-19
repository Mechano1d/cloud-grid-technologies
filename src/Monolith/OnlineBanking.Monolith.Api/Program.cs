using OnlineBanking.BuildingBlocks.Modules;
using OnlineBanking.BuildingBlocks.Time;
using OnlineBanking.Modules.Accounts;
using OnlineBanking.Modules.Clients;
using OnlineBanking.Modules.Transactions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

IModule[] modules =
[
    new ClientsModule(),
    new AccountsModule(),
    new TransactionsModule()
];

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", (IDateTimeProvider clock) => Results.Ok(new
{
    status = "Healthy",
    service = "online-banking-monolith",
    timestampUtc = clock.UtcNow
}))
.WithName("GetHealth")
.WithTags("System");

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
