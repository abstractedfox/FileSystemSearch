// See https://aka.ms/new-console-template for more information

using System;
using System.Linq;
using System.Collections.Generic;


using FileSystemSearch;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;


//This file isn't temporary and will probably be super boilerplatey

Console.WriteLine("Hello, World!");

string currentDir = System.IO.Directory.GetCurrentDirectory();
string localDirsFile = System.IO.Path.Join(currentDir, "localpaths.txt");
string localDirs = "";

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
string DbPath = System.IO.Path.Join(path, "database.db");

doStuff();

void doStuff()
{
    if (File.Exists(localDirsFile)){
        localDirs = System.IO.File.ReadAllText(localDirsFile);
    }

    using (DBClass db = new DBClass())
    {
        //DBHandler.AddFolder(db, new DirectoryInfo(localDirs), true);

        //DBHandler.GeneratePatterns(db);

        IQueryable patternoutcome = from PatternList in db.PatternLists
                                    select PatternList;

        Console.WriteLine("Patterns: ");

        foreach (PatternList list in patternoutcome)
        {
            Console.Write("[" + list.pattern + "] ");
        }
        Console.WriteLine();

        DBHandler.PopulatePatternLists(db);

        IQueryable testlist = from PatternList in db.PatternLists
                              where PatternList.pattern == "a"
                              select PatternList;

        foreach (PatternList pattern in testlist)
        {
            Console.WriteLine("Pattern info: " + pattern.pattern + " Count: " + pattern.DataItems.Count);
            Console.WriteLine("Pattern results: ");

            foreach (DataItem item in pattern.DataItems)
            {
                Console.WriteLine(item.CaseInsensitiveFilename);
            }
        }
    }
}






//testDB();

//ReadDB();

//testLists();

//boilerplate DB stuff below

void testDB()
{
    using (var db = new DBClass())
    {
        db.Add(new DataItem { FullPath="ASDF", CaseInsensitiveFilename="asdf"});
        db.Add(new PatternList { pattern="a", Size = 0});

        db.SaveChanges();

    }
}

void ReadDB()
{
    using (var db = new DBClass())
    {
        var query = from DataItem in db.DataItems
                    select DataItem;

        foreach (var item in query)
        {
            Console.WriteLine("jawn:" + item.CaseInsensitiveFilename);
        }
    }
}

//Let's try adding a DataItem to a list
void testLists()
{
    using (var db = new DBClass())
    {
        var itemQuery = from DataItem in db.DataItems
                    select DataItem;

        var listQuery = from PatternList in db.PatternLists
                        select PatternList;

        foreach (var item in itemQuery)
        {
            foreach (var list in listQuery)
            {
                Console.WriteLine("Listy: " + list.pattern);
                list.DataItems.Add(item);
            }
        }

        Console.WriteLine("jawns added. What's in the jawns?");

        foreach (var list in listQuery)
        {
            foreach (var item in list.DataItems)
            {
                item.FullPath = "HEHEHEHEEHEHEE";
                Console.WriteLine(item.CaseInsensitiveFilename);
            }
        }

        db.SaveChanges();
    }
}