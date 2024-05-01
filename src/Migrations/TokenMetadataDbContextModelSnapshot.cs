﻿// <auto-generated />
using System;
using System.Text.Json;
using Cardano.Metadata.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cardano.Metadata.Migrations
{
    [DbContext(typeof(TokenMetadataDbContext))]
    partial class TokenMetadataDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Cardano.Metadata.Models.SyncState", b =>
                {
                    b.Property<string>("Sha")
                        .HasColumnType("text");

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Sha");

                    b.ToTable("SyncState");
                });

            modelBuilder.Entity("Cardano.Metadata.Models.TokenMetadata", b =>
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
