using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace FileSystemSearch
{
    internal class DataSearch
    {
        public delegate void ResultReturn(DataItem result);


        //Performs a raw sequential search of the database based on the query. Slow.
        public static async Task QuerySearch(DBClass db, string query, ResultReturn receiveData)
        {
            const bool verbose = false;
            const string debugName = "DataSearch.QuerySearch():";

            if (verbose)
            {
                _DebugOut(debugName + "Searching for: " + query);
            }


            IQueryable querytime = from DataItem in db.DataItems
                                   select DataItem;


            await Task.Run(() =>
            {

                foreach (DataItem item in querytime)
                {
                    try
                    {
                        if (item.CaseInsensitiveFilename.Contains(query))
                        {
                            receiveData(item);
                        }
                    }
                    catch (NullReferenceException e)
                    {
                        _NullReferenceExceptionHandler();
                    }
                }
            });
            

            if (verbose)
            {
                _DebugOut(debugName + "Done");
            }
        }


        //Search a single pattern list for a query.
        //This may no longer be necessary (deletion candidate)
        /*
        public static async Task PatternListSearch(DBClass db, string query, ResultReturn receiveData, PatternList list)
        {
            const string debugName = "DataSearch.PatternListSearch:";
            const bool verbose = true;

            IQueryable listsInDB = from PatternList in db.PatternLists
                                   where PatternList.Id == list.Id
                                   select PatternList;

            int verifyCount = 0;
            foreach (PatternList aList in listsInDB)
            {
                verifyCount++;
                if (aList != list)
                {
                    _DebugOut(debugName + "Passed list did not match the equivalent ID in the index.");
                    return;
                }
            }
            if (verifyCount == 0)
            {
                _DebugOut(debugName + "Passed list did not match a list in the index.");
                return;
            }
            if (verifyCount > 1)
            {
                _DebugOut(debugName + "Too many matching lists were discovered.");
                return;
            }

            if (verbose)
            {
                _DebugOut(debugName + "Searching pattern list: " + list.pattern);
            }

            IQueryable listContents = from DataItemPatternList in db.DataItemPatternLists
                                      where DataItemPatternList.PatternList == list
                                      select DataItemPatternList.DataItemId;

            //Yes, for some reason, you can access the IDs from this table but not the actual DataItems

            List<DataItem> dataItemsToSearch = new List<DataItem>();

            foreach (long anId in listContents)
            {
                try
                {
                    DataItem item = db.DataItems.First(id => id.Id == anId);
                    if (item.CaseInsensitiveFilename.Contains(query))
                    {
                        receiveData(item);
                    }
                }
                catch (NullReferenceException)
                {
                    _NullReferenceExceptionHandler();
                }
            }


        }
        */

        //Search 'setToSearch' for 'query'. receiveData will be called with any results found
        public static async Task<Task> DataItemListSearchAsync(DBClass db, string query, ResultReturn receiveData, List<DataItem> setToSearch)
        {
            //This should return a task so the caller can keep track of how many concurrent search tasks are running
            return Task.Run(() =>
            {
                foreach (DataItem item in setToSearch)
                {
                    if (item.CaseInsensitiveFilename.Contains(query))
                    {
                        receiveData(item);
                    }
                }
            });
        }

        //Search by scaling pattern lists first
        //Currently in flux, do not use
        public static async Task SmartSearch(DBClass db, string query, ResultReturn receiveData, int taskLimit)
        {
            const bool debug = true;
            const string debugName = "DataSearch.SmartSearch:";


            if (debug)
            {
                _DebugOut(debugName + "Start");
            }

            
            List<Task> tasks = new List<Task>();

            /*
            foreach (DataItem item in items)
            {
                while (_GetRemainingTasks(tasks) >= taskLimit) ; //Block if the task limit is reached
                lock (tasks)
                {
                    tasks.Add(DataItemListSearchAsync(db, query, receiveData, items));
                }

            }
            */
        }

        //Testing the speed of well-structured queries
        public static async Task<Task> GoodQueryingTest(DBClass db, string query, ResultReturn receiveData)
        {
            const bool debug = true;
            const string debugName = "DataSearch.GoodQueryingTest:";

            if (debug) _DebugOut(debugName + "Called");

            /*
            IQueryable queryResults = from DataItemPatternList in db.DataItemPatternLists
                                      where query.Contains(DataItemPatternList.PatternList.pattern)
                                      select DataItemPatternList.DataItem;
            */

            IQueryable queryResults = from DataItemPatternList in db.DataItemPatternLists
                                      where DataItemPatternList.DataItem.CaseInsensitiveFilename.Contains(query.ToLower())
                                      select DataItemPatternList.DataItem;


            await Task.Run(() =>
            {
                Console.WriteLine("squagiel!");
                foreach (DataItem item in queryResults)
                {
                    receiveData(item);
                }
                Console.WriteLine("brangie.");
            });

            return new Task(() => { Console.WriteLine("tasky"); } );

        }

        //Possibly temporary pattern, returns all DataItems associated with a pattern list
        public static List<DataItem> GetPatternListContents(DBClass db, PatternList list)
        {
            IQueryable listContents = from DataItemPatternList in db.DataItemPatternLists
                                      where DataItemPatternList.PatternList.pattern == list.pattern
                                      select DataItemPatternList.DataItem;

            List<DataItem> results = new List<DataItem>();
            
            foreach (DataItem item in listContents)
            {
                results.Add(item);
            }

            return results;
        }

        public static async Task<Task> SearchListOfDataItems(DBClass db, string query, List<DataItem> itemsToSearch, ResultReturn receiveData)
        {
            return new Task(() =>
            {
                foreach (DataItem item in itemsToSearch)
                {
                    try
                    {
                        if (item.CaseInsensitiveFilename.Contains(query)) receiveData(item);
                    }
                    catch (NullReferenceException e)
                    {
                        _NullReferenceExceptionHandler();
                    }
                }
            });
        }


        //Returns the pattern lists that are relevant to a query.
        public static List<PatternList> FindMatchingPatternLists(DBClass db, string query)
        {
            List<PatternList> foundLists = new List<PatternList>();

            IQueryable relevantLists = from PatternList in db.PatternLists
                                       where query.Contains(PatternList.pattern)
                                       select PatternList;

            lock (db)
            {
                foreach (PatternList list in relevantLists) foundLists.Add(list);
            }

            return foundLists;
        }


        //*********************Private methods

        //Perform a search by looking for relvant pattern lists
        private static async Task<Task> _GetPatternListContents(DBClass db, string query, ResultReturn receiveData)
        {
            IQueryable queryResults = from DataItemPatternList in db.DataItemPatternLists
                                      where query.Contains(DataItemPatternList.PatternList.pattern)
                                      select DataItemPatternList.DataItem;

            //List<DataItem> results = new List<DataItem>();

            return new Task(() => {
                foreach (DataItem item in queryResults)
                {
                    receiveData(item);
                }
            });

        }

        private static void _DebugOut(string debugText)
        {
            Console.WriteLine(debugText);
        }


        private static void _NullReferenceExceptionHandler()
        {
            _DebugOut("Null reference!!");
        }


        //Find out how many tasks in a List<Task> have not completed
        private static int _GetRemainingTasks(List<Task> tasks)
        {
            int remainingTasks = 0;

            if (tasks.Count == 0) return -1;

            foreach (Task aTask in tasks)
            {
                if (!aTask.IsCompleted)
                {
                    remainingTasks++;
                }
            }

            return remainingTasks;
        }
    }
}
