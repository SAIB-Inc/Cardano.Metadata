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
builder.Services.AddEndpointsApiExplorer();  // Required for OpenAPI in .NET 8+
builder.Services.AddSwaggerGen();  // Optional, if using Swagger for OpenAPI spec

// Set up the database context (as per your original setup)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TokenMetadataDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline for Scalar Playground and API Reference
if (app.Environment.IsDevelopment())
{
    // Expose the OpenAPI specification, if needed (this is based on .NET 8+ setup)
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "/openapi/{documentName}.json";  // Customize the route template
    });

    // Map Scalar API Reference with custom options
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Custom API Playground")  // Title of your API playground
               .WithSidebar(true)  // Optionally hide the sidebar
               .WithOpenApiRoutePattern("/openapi/{documentName}.json");  // Path to your OpenAPI spec
    });
}

app.UseHttpsRedirection();
app.MapCarter();  // Map your Carter endpoints

app.Run();
