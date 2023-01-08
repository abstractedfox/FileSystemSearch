
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystemSearch
{

    //This class is for processing CLI functionality. Instantiate one CLI object at a time or there will be sqlite conflicts.
    internal class CLI
    {
        private Object _dbLockObject = new Object();
        private List<DataItem> _results;
        private bool _exit;

        const string userHelpText = "To perform a search, simply type any query. To select a result, enter the number of that result into the prompt.\n" +
            "To perform an advanced search, put each desired string in a pair of quotation marks. To search the full path, place a : at the beginning of a string.\n" +
            "Example: \"iidx\" \".jpg\" \":\\memes\\\" (search for jpgs with 'iidx' in the name in any path containing 'memes')\n" +
            "Results will appear as they are discovered, and entry selections can be made at any time, even if the search is still in progress.\n" +
            "Commands:\n" +
            "\"/buildindex\" Add to the existing index from a chosen directory. Prompts for a root directory after calling.\n" +
            "\"/rebuildindex\" Rebuild the index starting at a chosen directory. Erases the existing index first.\n" +
            "\"/help\" View this help text.\n" +
            "\"/exit\" Exit the search utility.";

        public CLI()
        {
            _results = new List<DataItem>();
            _exit = false;

            Initialize();
        }

        
        //Enter a loop which will continuously wait for user input.
        public async void CLILoopEntry()
        {
            while (!_exit) 
            {
                //note: make this a switch statement
                //better note: replace this with an actual parser
                _userOutput("Enter a query or command! Enter \"/help\" for help.");

                string? input = Console.ReadLine();

                if (input == null)
                {
                    _userOutput("Invalid input");
                    continue;
                }

                if (input == "/help")
                {
                    _userOutput(userHelpText);
                    continue;
                }

                if (input == "/exit")
                {
                    Environment.Exit(0);
                }

                if (input == "/buildindex")
                {
                    string indexstart = "";

                    _userOutput("Enter a root folder to begin the index. Enter \"/cancel\" to cancel.");

                    input = Console.ReadLine();
                    if (input == "/cancel")
                    {
                        continue;
                    }
                    await BuildIndex(input, false);

                    continue;
                }

                if (input == "/rebuildindex")
                {
                    string indexstart = "";

                    _userOutput("This will purge the entire index. Enter a root folder to rebuild the index. Enter \"/cancel\" to cancel.");

                    input = Console.ReadLine();
                    if (input == "/cancel")
                    {
                        continue;
                    }
                    await BuildIndex(input, true);

                    continue;
                }


                Search(input);

                SelectResultLoop();
            }
        }


        public async Task Search(string query)
        {
            _results.Clear();
            GC.Collect(); //Cleanup in case a significant number of results left a lot of memory in GC limbo

            if (query.Contains("\""))
            {
                DataSearch.ParsedQuerySearch(_dbLockObject, query, _ReceiveData);
            }
            else
            {
                Task searchTask = DataSearch.QuerySearch(_dbLockObject, query, _ReceiveData);
            }

            return;
        }


        //Enable the user to continuously select results from the search
        public void SelectResultLoop()
        {
            string input = "";
            while (true) {
                _userOutput("Result selections are now possible. For a new search, enter \"/newsearch\".");
                input = Console.ReadLine();
                if (input == "/newsearch")
                {
                    return;
                }

                int resultNumber;

                try
                {
                    resultNumber = int.Parse(input);
                    if (resultNumber < 0 || resultNumber > _results.Count - 1)
                    {
                        _userOutput("Invalid result selection.");
                    }
                    SelectResult(resultNumber);
                }
                catch (FormatException)
                {
                    _userOutput("Please enter a valid selection, or enter \"/newsearch\" to start a new search.");
                }

            }
        }


        //Pass a position in the 'results' list. If it's a valid position, the file is opened in Explorer
        public void SelectResult(int resultNumber)
        {
            if (resultNumber < 0 || resultNumber > _results.Count - 1)
            {
                return;
            }

            if (!_results[resultNumber].IsFolder)
            {
                System.Diagnostics.Process.Start("explorer.exe", String.Format("/select,\"{0}\"", _results[resultNumber].FullPath));
            }
            else
            {
                System.Diagnostics.Process.Start("explorer.exe", String.Format("/n,\"{0}\"", _results[resultNumber].FullPath));
            }
        }


        //Build or rebuild the index from the passed path. If "rebuildFromScratch" == true, it empties the database first.
        public async Task BuildIndex(string path, bool rebuildFromScratch)
        {
            if (path == null)
            {
                _userOutput("Invalid entry");
                return;
            }
            DirectoryInfo rootFolder = new DirectoryInfo(path);
            if (!rootFolder.Exists)
            {
                _userOutput("Path " + path + "\nis not a valid directory.");
                return;
            }


            if (rebuildFromScratch)
            {
                _userOutput("Initializing index.");
                DBHandler.Clear(_dbLockObject);
                await DBHandler.AddFolder(_dbLockObject, rootFolder, true, true);
            }
            else
            {
                await DBHandler.AddFolder(_dbLockObject, rootFolder, true, false);
            }

            
            GC.Collect();

            _userOutput("Index complete.");
        }


        //**********Private methods

        //Initialize the database context
        private async void Initialize()
        {
                lock (_dbLockObject)
                {
                    using (DBClass db = new DBClass())
                    {
                        if (db == null)
                        {
                            _errorHandler("Database returned null.");
                        }
                        if (!(db.GetService<IDatabaseCreator>() as RelationalDatabaseCreator).Exists())
                        {
                            _errorHandler("Database does not exist.");
                        }

                        _userOutput("Initialized with " + db.DataItems.Count<DataItem>() + " indexed files.");
                    }
                }

                return;
        }


        //Callback for receiving data from queries
        private void _ReceiveData(DataItem data)
        {
            //Compensate for duplicates in the index.
            //if (!_AlreadyExists(results, data) && DBHandler.VerifyItem(data) == ResultCode.SUCCESS)
            if (DBHandler.VerifyItem(data) == ResultCode.SUCCESS)
            {
                _results.Add(data);
                _userOutput("[" + (_results.Count - 1) + "] " + data.FullPath);
            }
        }


        //Checks a passed list of DataItems for an example matching itemToCheck, ignoring the key
        private bool _AlreadyExists(List<DataItem> dataItems, DataItem itemToCheck)
        {
            foreach(DataItem item in dataItems)
            {
                if (item.FullPath.ToLower() == itemToCheck.FullPath.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        
        //Placeholder; housekeeping class will need this as a callback
        private void _ReceiveDataResultCode(DataItem data, ResultCode code)
        {

        }


        private static void _errorHandler(string error)
        {
            _userOutput("Error: " + error);
        }


        private static void _userOutput(string debugText)
        {
            Console.WriteLine(debugText);
        }
    }
}
