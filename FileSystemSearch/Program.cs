// See https://aka.ms/new-console-template for more information


using System;
using System.Linq;
using System.Collections.Generic;


using FileSystemSearch;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;


//This file is temporary and will probably be super boilerplatey

Console.WriteLine("Hello, World!");


string currentDir = System.IO.Directory.GetCurrentDirectory();
string localDirsFile = System.IO.Path.Join(currentDir, "localpaths.txt");
string localDirs = "";

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
string DbPath = System.IO.Path.Join(path, "database.db");

List<DataItem> results = new List<DataItem>();

//testSearch();

doStuff();

Console.WriteLine("doStuff is over");


while (true)
{
    //Console.WriteLine("asdf");
    await Task.Delay(6000);
}

async void testSearch()
{
    List<Task> tasks = new List<Task>();

    string query = "asdf";
    using (DBClass db = new DBClass())
    {
        tasks.Add(DataSearch.GoodQueryingTest(db, query, ReceiveData));
        while (true) ;

        Console.WriteLine("done");
    }
}





void ReceiveData(DataItem thing)
{
    lock (results)
    {
        if (!results.Contains(thing))
        {
            results.Add(thing);
            Console.WriteLine("Data received:" + thing.CaseInsensitiveFilename);
        }
    }
}

void ReceiveDataResultCode(DataItem thing, ResultCode code)
{
    lock (results)
    {
        if (!results.Contains(thing))
        {
            results.Add(thing);
            Console.WriteLine("Data received (" + code.ToString() + "):" + thing.CaseInsensitiveFilename);
        }
    }
}


async void doStuff()
{
    if (File.Exists(localDirsFile)){
        localDirs = System.IO.File.ReadAllText(localDirsFile);
    }

    if (true) //Create the db
    {
        using (DBClass db = new DBClass())
        {
            Task<ResultCode> jawn = DBHandler.AddFolder(db, new DirectoryInfo(localDirs), true);
            await jawn;
        }
    }

    Console.WriteLine("folder add is DONEZO");

    //Use the new housekeeping class!
    using (DBClass db = new DBClass()) {
        Housekeeping dostuff = new Housekeeping(db);
        await dostuff.StartHousekeeping(ReceiveDataResultCode);

        Console.WriteLine("WE DONE maybe");
        while (true) ;
    }

    if (false) //View DB
    {
        using (DBClass db = new DBClass())
        {
            IQueryable viewFiles = from DataItem in db.DataItems
                                   select DataItem;

            foreach (DataItem file in viewFiles)
            {
                Console.WriteLine("File: " + file.CaseInsensitiveFilename);
            }
        }
    }

    if (false) //Generate patterns
    {
        using (DBClass db = new DBClass())
        {
            if (false) //Delete existing patterns first
            {
                IQueryable deleteLists = from PatternList in db.PatternLists
                                         select PatternList;

                foreach (PatternList list in deleteLists)
                {
                    DBHandler.RemoveItem(db, list);
                }
                Console.WriteLine("Existing patterns deleted");
            }

            Console.WriteLine("Generating patterns");
            DBHandler.GeneratePatterns(db);
            Console.WriteLine("Patterns generated");
            db.SaveChanges();
        }
    }

    if (false) //Show patterns
    {
        using (DBClass db = new DBClass())
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
    }

    if (false) //Populate patterns
    {
        using (DBClass db = new DBClass()) {
            Console.WriteLine("Populating patterns");
            DBHandler.PopulatePatternLists(db);
            Console.WriteLine("Done populating patterns");
            db.SaveChanges();
        }

    }

    if (false) //Print contents of a pattern list
    {
        using (DBClass db = new DBClass())
        {
            //Get the ID for the list corresponding to "a"
            IQueryable testlist = from PatternList in db.PatternLists
                                  where PatternList.pattern == "a"
                                  select PatternList;

            IQueryable listy = from DataItemPatternList in db.DataItemPatternLists
                               where DataItemPatternList.PatternList.pattern == "a"
                               select DataItemPatternList;

            Console.WriteLine("List time:");

            long id = 0;
            foreach (DataItemPatternList list in listy)
            {
                Console.WriteLine("Thingy:" + list.DataItem.CaseInsensitiveFilename);
            }



            /*

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
            */
        }
            
    }

    if (false) //Test DataItem deletions
    {
        using (DBClass db = new DBClass())
        {
            foreach (DataItem item in db.DataItems)
            {
                DBHandler.RemoveItem(db, item);
            }

            Console.WriteLine("Printing any remaining dataitems:");

            foreach (DataItem item in db.DataItems)
            {
                Console.WriteLine(item.CaseInsensitiveFilename);
            }

            Console.WriteLine("Printing any remaining associations:");

            foreach (DataItemPatternList association in db.DataItemPatternLists)
            {
                Console.WriteLine(association.PatternList.pattern);
            }
            db.SaveChanges();
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
        //db.Add(new PatternList { pattern="a", Size = 0});

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
