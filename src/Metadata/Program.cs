using Microsoft.EntityFrameworkCore;
using Metadata.Data;
using Carter;
using Scalar.AspNetCore;
using System.Text.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCarter();

// Enable OpenAPI generation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); 

// Set up the database context (as per your original setup)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TokenMetadataDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline for Scalar Playground and API Reference
if (app.Environment.IsDevelopment())
{

    app.UseSwagger(options =>
    {
        options.RouteTemplate = "/openapi/{documentName}.json"; 
    });

    // Map Scalar API Reference with custom options
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Custom API Playground") 
               .WithSidebar(true) 
               .WithOpenApiRoutePattern("/openapi/{documentName}.json"); 
    });
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
