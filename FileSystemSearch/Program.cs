// See https://aka.ms/new-console-template for more information

using Microsoft.Data.Sqlite;
using System.Collections.Generic;


using FileSystemSearch;


Console.WriteLine("Hello, World!");

const string TestPath = "C:\\";

const string DBPath = "C:\\";

//DBHandler.InitDatabase(TestPath);

using (SqliteConnection db = new SqliteConnection($"Filename={DBPath}"))
{
    db.Open();
    DBHandler.VerifyTables(db);
}


    //Boilerplate db code below

    static void CreateTestDB()
    {
        using (SqliteConnection db = new SqliteConnection($"Filename={TestPath}"))
        {
            db.Open();

            string tableCommand = "CREATE TABLE IF NOT " +
                "EXISTS TestTable (Primary_Key INTEGER PRIMARY KEY, " +
                "Text_Entry NVARCHAR(2048) NULL)";

            SqliteCommand createTable = new SqliteCommand(tableCommand, db);

            createTable.ExecuteReader();
        }
    }