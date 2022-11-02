using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

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


        //Search by scaling pattern lists first
        public static async Task SmartSearch(DBClass db, string query, ResultReturn receiveData, int taskLimit)
        {
            List<PatternList> lists = _FindMatchingPatternLists(db, query);
            List<Task> tasks = new List<Task>();

            if (lists.Count == 0) return;

            


            foreach (PatternList list in lists)
            {
                while (_GetRemainingTasks(tasks) >= taskLimit) ; //Block if the task limit is reached
                lock (tasks)
                {
                    tasks.Add(PatternListSearch(db, query, receiveData, list));
                }

            }
        }



        //*********************Private methods

        //Find pattern lists that match a query
        private static List<PatternList> _FindMatchingPatternLists(DBClass db, string query)
        {
            IQueryable patternLists = from PatternList in db.PatternLists
                                      where query.Contains(PatternList.pattern)
                                      select PatternList;

            List<PatternList> results = new List<PatternList>();

            foreach (PatternList list in patternLists)
            {
                results.Add(list);
            }

            return results;
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
