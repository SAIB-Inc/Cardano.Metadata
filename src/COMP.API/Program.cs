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
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();

WebApplication app = builder.Build();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen(options =>
    {
        options.Path = "/openapi/{documentName}.json";
    });
    app.MapScalarApiReference(options =>
    {
        options.Title = "COMP API Documentation";
        options.Theme = ScalarTheme.Default;
        options.ShowSidebar = true;
    });
}

app.Run();