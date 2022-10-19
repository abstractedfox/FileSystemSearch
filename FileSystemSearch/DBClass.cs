using System;
using System.Collections.Generic;
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
        int Id { get; set; }
        string CaseInsensitiveFilename { get; set; }
        string FullPath { get; set; }
        //Dictionary<int, int> PositionInPatternList = new Dictionary<int, int>();
        //dictionary pattern is: PatternListID:PrimaryKey of this item in that list

        List<int> PatternListsContainingThis = new List<int>();
    }

    public class PatternList
    {
        int Id { get; set; }
        int Size { get; set; }
        string pattern { get; set; }
        List<int> ForeignKeyInMainList = new List<int>();
    }
}
