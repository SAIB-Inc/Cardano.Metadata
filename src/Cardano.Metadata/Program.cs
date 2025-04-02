using Carter;
using Cardano.Metadata.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Cardano.Metadata.Modules.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCarter();


builder.Services.AddDbContextFactory<MetadataDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<MetadataHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
