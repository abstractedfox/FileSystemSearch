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

        if (false) //View DB
        {
            IQueryable viewFiles = from DataItem in db.DataItems
                                   select DataItem;

            foreach (DataItem file in viewFiles)
            {
                Console.WriteLine("File: " + file.CaseInsensitiveFilename);
            }
        }

        if (false) //Generate patterns
        {
            if (false) //Delete existing patterns first
            {
                IQueryable deleteThese = from PatternList in db.PatternLists
                                         select PatternList;

                foreach (PatternList list in deleteThese)
                {
                    DBHandler.RemoveItem(db, list);
                }
            }

            Console.WriteLine("Generating patterns");
            DBHandler.GeneratePatterns(db);
            Console.WriteLine("Patterns generated");
        }

        if (true) //Show patterns
        {
            IQueryable patternoutcome = from PatternList in db.PatternLists
                                        select PatternList;

            Console.WriteLine("Patterns: ");

            foreach (PatternList list in patternoutcome)
            {
                Console.Write("[" + list.pattern + "] ");
            }
            Console.WriteLine();
        }

        if (false) //Populate patterns
        {
            Console.WriteLine("Populating patterns");
            DBHandler.PopulatePatternLists(db);
            Console.WriteLine("Done populating patterns");

        }

        if (true) //Print contents of a pattern list
        {
            //Get the ID for the list corresponding to "a"
            IQueryable testlist = from PatternList in db.PatternLists
                                  where PatternList.pattern == "a"
                                  select PatternList;

            long id = 0;
            foreach (PatternList list in testlist)
            {
                id = list.Id;
                break;
            }

            IQueryable resolvelist = from DataItemPatternList in db.DataItemPatternLists
                                      where DataItemPatternList.PatternListId == id
                                      select DataItemPatternList;

            foreach (DataItemPatternList list in resolvelist)
            {
                IQueryable getData = from DataItem in db.DataItems
                                     where DataItem.Id == list.DataItemId
                                     select DataItem;

                foreach (DataItem atLast in getData)
                {
                    Console.WriteLine(atLast.CaseInsensitiveFilename);
                }

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