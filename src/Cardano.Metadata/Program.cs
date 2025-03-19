using Carter;
using Cardano.Metadata.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Cardano.Metadata.Modules.Handlers;
using System.Net.Http.Headers;
using System.Reflection;
using Cardano.Metadata.Workers.Handlers;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCarter();

var connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<MetadataDbContext>(options =>
    options.UseNpgsql(connection));

builder.Services.AddSingleton<MetadataHandler>();
builder.Services.AddScoped<SyncHandler>();
builder.Services.AddScoped<DatabaseHandler>();

builder.Services.AddHostedService<Github>();

builder.Services.AddHttpClient("GithubApi", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    ProductInfoHeaderValue productValue = new("CardanoTokenMetadataService", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version");
    ProductInfoHeaderValue commentValue = new("(+https://github.com/SAIB-Inc/Cardano.Metadata)");
    client.DefaultRequestHeaders.UserAgent.Add(productValue);
    client.DefaultRequestHeaders.UserAgent.Add(commentValue);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration["GithubPAT"]);
});

builder.Services.AddHttpClient("GithubRaw", client =>
{
    client.BaseAddress = new Uri("https://raw.githubusercontent.com/");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
