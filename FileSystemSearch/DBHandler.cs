using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystemSearch
{
    internal class DBHandler
    {
        private const string _databaseName = "database.sqlite";

        //Initialize a new database
        public static bool InitDatabase(string path)
        {
            const string debugName = "DBHandler::InitDatabase():";
            string newDBFullPath = path + _databaseName;

            using (SqliteConnection db = new SqliteConnection($"Filename={newDBFullPath}"))
            {
                try
                {
                    db.Open();

                    string initMainTable = "CREATE TABLE " +
                        "MainTable (Primary_Key INTEGER PRIMARY KEY," +
                        "Case_Insensitive_Name TEXT," +
                        "Full_Path TEXT," +
                        "Pattern_Keys_Dict TEXT)";

                    string initPatternTable = "CREATE TABLE " +
                        "PatternLists (Primary_Key INTEGER PRIMARY KEY," +
                        "Size INTEGER," +
                        "Main_Table_Keys_Dict TEXT)";

                    SqliteCommand createMainTable = new SqliteCommand(initMainTable, db);
                    SqliteCommand createPatternTable = new SqliteCommand(initPatternTable, db);

                    createMainTable.ExecuteReader();
                    createPatternTable.ExecuteReader();
                }
                catch(Exception e)
                {
                    debugOut(debugName + "Exception :" + e);
                    return false;
                }

                VerifyTables(db);
            }

            return true;
        }

        public static bool VerifyTables(SqliteConnection db)
        {
            String checkTablesCommand = "SELECT name FROM sqlite_master WHERE type='table';";

            SqliteCommand command = new SqliteCommand(checkTablesCommand, db);


            SqliteDataReader checkTables = command.ExecuteReader();

            
            Console.WriteLine("Blorg:" + checkTables.GetName(0));

            

            return true;
        }

        private static void debugOut(string debugText)
        {
            Console.WriteLine(debugText);
        }

    }
}
