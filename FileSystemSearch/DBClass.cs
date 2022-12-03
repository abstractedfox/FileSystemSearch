
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace FileSystemSearch
{
    internal class DBClass : DbContext
    {
        public DbSet<DataItem> DataItems { get; set; }
        public DbSet<PatternList> PatternLists { get; set; }
        public DbSet<DataItemPatternList> DataItemPatternLists { get; set; }

        public string DbPath { get; } //local path to the database

        public DBClass()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "FileSystemSearchDatabase.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite($"Data Source={DbPath}");
    }

    public class DataItem
    {
        [Key]
        public long Id { get; set; }

        public string? CaseInsensitiveFilename { get; set; }
        public string? FullPath { get; set; }

        public bool IsFolder { get; set; }

        //Set this flag to true once an entry has been duplicate-checked and sorted to pattern lists
        public bool HasBeenDuplicateChecked { get; set; }

        //Set if discrepancies are found (ie file does not exist)
        public bool HasDiscrepancies { get; set; }

        public bool MarkForDeletion { get; set; }

        public DataItem()
        {
            IsFolder = false;
            HasBeenDuplicateChecked = false;
            MarkForDeletion = false;
            HasDiscrepancies = false;
        }

        //asdf: this may not be permanent, we just need to see if adding finalizers will address that memory issue when indexing
        ~DataItem()
        {
            CaseInsensitiveFilename = null;
            FullPath = null;
        }
    }


    public class PatternList
    {
        [Key]
        public long Id { get; set; }

        public string? pattern { get; set; }

        public PatternList()
        {
        }
    }

    //association table
    //Yes it's necessary to do it this way; these can be deleted without having constraint issues with the DataItems table
    public class DataItemPatternList
    {
        [Key]
        public long Id { get; set; }

        public long DataItemId { get; set; }
        public long PatternListId { get; set; }

        public virtual DataItem DataItem { get; set; }
        public virtual PatternList PatternList { get; set; }


    }
}
