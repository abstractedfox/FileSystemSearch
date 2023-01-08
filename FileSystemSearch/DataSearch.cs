
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

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


        //Search the contents of 'setToSearch' for 'query'. receiveData will be called with any results found
        public static async Task<Task> SearchDataItemList(DBClass db, string query, ResultReturn receiveData, List<DataItem> setToSearch)
        {
            //This should return a task so the caller can keep track of how many concurrent search tasks are running
            return Task.Run(() =>
            {
                foreach (DataItem item in setToSearch)
                {
                    if (item != null && item.CaseInsensitiveFilename.Contains(query))
                    {
                        receiveData(item);
                    }
                }
            });
        }




        //Exactly what it sounds like
        public static async Task<Task> QuerySearch(Object dbLockObject, string query, ResultReturn receiveData)
        {
            
            const bool debug = true;
            const string debugName = "DataSearch.DirectQuerySearch:";

            if (debug) _DebugOut(debugName + "Called");

            return Task.Run(() =>
            {
                lock (dbLockObject)
                {
                    using (DBClass db = new DBClass())
                    {
                        IQueryable queryResults = from DataItem in db.DataItems
                                                  where DataItem.CaseInsensitiveFilename.Contains(query.ToLower())
                                                  select DataItem;
                        foreach (DataItem item in queryResults)
                        {
                            receiveData(item);
                        }
                    }
                }

                if (debug) _DebugOut(debugName + "Complete");
            });
        }

        public static ResultCode ParsedQuerySearch(Object dbLockObject, string input, ResultReturn receiveData)
        {
            //Input should be formatted as queries contained in pairs of quotes
            List<string> queries = new List<string>();

            if (input == null || input == "") return ResultCode.FAIL;

            int firstQuote = input.IndexOf("\"");
            int lastQuote = input.IndexOf("\"", firstQuote + 1);
            if (firstQuote == -1 || lastQuote == -1) return ResultCode.FAIL;

            while (firstQuote != -1)
            {
                queries.Add(input.Substring(firstQuote + 1, lastQuote - 1 - firstQuote));
                firstQuote = input.IndexOf("\"", lastQuote + 1);
                lastQuote = input.IndexOf("\"", firstQuote + 1);
            }

            using (DBClass searchInstance = new DBClass())
            {
                //If the query starts with a ':', search the full path and not just the filename
                IQueryable queryResults;
                if (queries[0][0] != ':')
                {
                    queryResults = from DataItem in searchInstance.DataItems
                                   where DataItem.CaseInsensitiveFilename.Contains(queries[0].ToLower())
                                   select DataItem;
                }
                else
                {
                    queryResults = from DataItem in searchInstance.DataItems
                                   where DataItem.FullPath.ToLower().Contains(queries[0].Substring(1).ToLower())
                                   select DataItem;
                }

                bool mismatch = false;
                foreach (DataItem item in queryResults)
                {
                    for (int i = 0; i < queries.Count; i++)
                    {
                        //If the query starts with a ':', search the full path and not just the filename
                        if (!queries[i].Contains(":"))
                        {
                            if (!item.CaseInsensitiveFilename.Contains(queries[i].ToLower()))
                            {
                                mismatch = true;
                                break;
                            }
                        }
                        else if (queries[i][0] == ':')
                        {
                            if (!item.FullPath.ToLower().Contains(queries[i].Substring(1).ToLower()))
                            {
                                mismatch = true;
                                break;
                            }
                        }
                    }
                    if (!mismatch) receiveData(item);
                    mismatch = false;
                }
            }
            return ResultCode.SUCCESS;
        }



        //*********************Private methods


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
