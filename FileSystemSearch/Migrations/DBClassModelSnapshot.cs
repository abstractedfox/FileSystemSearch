﻿// <auto-generated />
using System;
using FileSystemSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FileSystemSearch.Migrations
{
    [DbContext(typeof(DBClass))]
    partial class DBClassModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.10");

            modelBuilder.Entity("FileSystemSearch.DataItem", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("CaseInsensitiveFilename")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FullPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long?>("PatternListId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PatternListId");

                    b.ToTable("DataItems");
                });

            modelBuilder.Entity("FileSystemSearch.PatternList", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Size")
                        .HasColumnType("INTEGER");

                    b.Property<string>("pattern")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("PatternLists");
                });

            modelBuilder.Entity("FileSystemSearch.DataItem", b =>
                {
                    b.HasOne("FileSystemSearch.PatternList", null)
                        .WithMany("DataItems")
                        .HasForeignKey("PatternListId");
                });

            modelBuilder.Entity("FileSystemSearch.PatternList", b =>
                {
                    b.Navigation("DataItems");
                });
#pragma warning restore 612, 618
        }
    }
}
