﻿// <auto-generated />
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TeddySwapCardanoMetadataService.Data;

#nullable disable

namespace TeddySwapCardanoMetadataService.Migrations
{
    [DbContext(typeof(TokenMetadataDbContext))]
    [Migration("20230211031431_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TeddySwapCardanoMetadataService.Models.SyncState", b =>
                {
                    b.Property<string>("Sha")
                        .HasColumnType("text");

                    b.Property<string>("Date")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Sha");

                    b.ToTable("SyncState");
                });

            modelBuilder.Entity("TeddySwapCardanoMetadataService.Models.TokenMetadata", b =>
                {
                    b.Property<string>("Subject")
                        .HasColumnType("text");

                    b.Property<JsonElement>("Data")
                        .HasColumnType("jsonb");

                    b.HasKey("Subject");

                    b.ToTable("TokenMetadata");
                });
#pragma warning restore 612, 618
        }
    }
}
