﻿// <auto-generated />
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
                        .HasColumnType("TEXT");

                    b.Property<string>("FullPath")
                        .HasColumnType("TEXT");

                    b.Property<bool>("HasBeenDuplicateChecked")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("HasDiscrepancies")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsFolder")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("MarkForDeletion")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("DataItems");
                });

            modelBuilder.Entity("FileSystemSearch.DataItemPatternList", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<long>("DataItemId")
                        .HasColumnType("INTEGER");

                    b.Property<long>("PatternListId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("DataItemId");

                    b.HasIndex("PatternListId");

                    b.ToTable("DataItemPatternLists");
                });

            modelBuilder.Entity("FileSystemSearch.PatternList", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("pattern")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("PatternLists");
                });

            modelBuilder.Entity("FileSystemSearch.DataItemPatternList", b =>
                {
                    b.HasOne("FileSystemSearch.DataItem", "DataItem")
                        .WithMany()
                        .HasForeignKey("DataItemId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FileSystemSearch.PatternList", "PatternList")
                        .WithMany()
                        .HasForeignKey("PatternListId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DataItem");

                    b.Navigation("PatternList");
                });
#pragma warning restore 612, 618
        }
    }
}
