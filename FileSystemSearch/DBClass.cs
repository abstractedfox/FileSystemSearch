using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
            DbPath = System.IO.Path.Join(path, "database.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite($"Data Source={DbPath}");
    }

    public class DataItem
    {
        [Key]
        public long Id { get; set; }

        public string CaseInsensitiveFilename { get; set; }
        public string FullPath { get; set; }

        public ICollection<PatternList> PatternLists { get; set; }

        public DataItem()
        {
            PatternLists = new List<PatternList>();
        }

    }

    public class PatternList
    {
        [Key]
        public long Id { get; set; }

        public int Size { get; set; }
        public string pattern { get; set; }

        public ICollection<DataItem> DataItems { get; set; }

        public PatternList()
        {
            this.DataItems = new List<DataItem>();
        }
    }

    //association table
    public class DataItemPatternList
    {
        public long DataItemId { get; set; }
        public long PatternListId { get; set; }

        public virtual DataItem DataItem { get; set; }
        public virtual PatternList PatternList { get; set; }


    }
}
