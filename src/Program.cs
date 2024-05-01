using Microsoft.EntityFrameworkCore;
using Cardano.Metadata.Data;
using Cardano.Metadata.Workers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = JsonSerializer.Deserialize<string[]>(builder.Configuration.GetValue<string>("AllowedOrigins") ?? "[]");
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "Main",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins ?? [])
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Add services to the container.
builder.Services.AddDbContextFactory<TokenMetadataDbContext>(options =>
{
    options.EnableSensitiveDataLogging(true);
    options.UseNpgsql(builder.Configuration.GetConnectionString("TokenMetadataService"));
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<GithubWorker>();
builder.Services.AddHttpClient();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<TokenMetadataDbContext>();
dbContext.Database.Migrate();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();
app.UseCors("Main");
app.Run();
