using Microsoft.EntityFrameworkCore;
using Metadata.Data;
using Carter;
using Scalar.AspNetCore;
using Metadata.Workers;
using Metadata.Services; // Contains GitHubService and IGithub
using System.Net.Http.Headers;
using System.Reflection;
using Metadata.Interface.GIthub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<TokenMetadataDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IGithub, GitHubService>();

builder.Services.AddHostedService<GithubWorker>();

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
