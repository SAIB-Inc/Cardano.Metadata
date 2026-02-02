using FastEndpoints;
using FastEndpoints.Swagger;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using COMP.Data.Data;
using COMP.API.Handlers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<MetadataDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<MetadataHandler>();
builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "COMP API";
        s.Version = "v1";
    };
});

WebApplication app = builder.Build();

app.MapOpenApi();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.Prefix = "v";
    c.Versioning.DefaultVersion = 1;
    c.Versioning.PrependToRoute = true;
});

app.MapScalarApiReference(options =>
{
    options.Title = "COMP API Documentation";
    options.Theme = ScalarTheme.Default;
    options.ShowSidebar = true;
});

app.Run();