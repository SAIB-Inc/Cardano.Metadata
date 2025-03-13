
using Carter;
using Comp.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCarter();

var connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<MetadataDbContext>(options =>
    options.UseNpgsql(connection));


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.UseHttpsRedirection();
app.MapCarter();

app.Run();
