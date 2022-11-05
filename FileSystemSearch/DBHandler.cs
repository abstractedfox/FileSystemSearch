using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel.Design.Serialization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;


//This class is for performing actions on the database.

//This class should be stateless; the database should be passed as an argument

//Disposal of the database object should be done in the calling function to prevent conflicts

//Let's avoid complicating things with performing database operations asynchronously
//since we aren't sure how that will go over with SQLite


namespace FileSystemSearch
{
    public enum ResultCode
    {
        NULLCODE,
        SUCCESS,
        FAIL,
        ITEM_NOT_FOUND,
        DUPLICATE_FOUND
    }

    //A class for queueing database actions.
    //This is meant to enable some aspects of large file additions to work asynchronously while
    //keeping actual database writes synchronous
    class DBQueue
    {
        List<Task> dbTasks;
        List<DataItem> dataItemQueue, dataItemNext;
        DBClass db;
        bool finished;

        int itemsAdded, debugCounter;

        const bool debug = true, debugLocks = false;

        public DBQueue(DBClass dbReference)
        {
            dbTasks = new List<Task>();
            dataItemQueue = new List<DataItem>();
            dataItemNext = new List<DataItem>();
            db = dbReference;
            finished = false;
            _DebugReadout();

            itemsAdded = 0;

            debugCounter = 0;
        }

        public async void RunQueue()
        {
            await Task.Run(() => {
                bool superdebug = false;
                int interval = 0;
                while (true)
                {
                    interval++;
                    if (interval % 100000000 == 0) Console.WriteLine("loopy");

                    //Run continuously until the caller says it's done sending data.
                    if (finished == true && dataItemQueue.Count == 0 && dataItemNext.Count == 0) break;
                    if (dataItemQueue.Count == 0 && dataItemNext.Count == 0)
                    {
                        //if (superdebug) _DebugOutAsync("dataItemQueue and dataItemNext are both empty. Continuing.");
                        continue;
                    }

                    if (superdebug) _DebugOutAsync("Continue");

                    //This arrangement is intended to prevent a situation where locking dataItemQueue could defeat the purpose
                    //of this class by making it perform effectively synchronously.
                    if (dataItemNext.Count > 0 && dataItemQueue.Count == 0)
                    {
                        lock (dataItemNext)
                        {
                            lock (dataItemQueue)
                            {
                                if (debugLocks) _DebugOutAsync("RunQueue dataItemNext lock");
                                foreach (DataItem item in dataItemNext)
                                {
                                    dataItemQueue.Add(item);
                                }
                                //if (debug) _DebugOutAsync("Clearing dataItemNext of " + dataItemNext.Count + " items.");
                                dataItemNext.Clear();
                            }
                        }
                        if (debugLocks) _DebugOutAsync("RunQueue dataItemNext unlock");
                    }

                    if (true)
                    {
                        if (dataItemQueue.Count > 0)
                        {
                            lock (dataItemQueue)
                            {
                                if (debugLocks) _DebugOutAsync("RunQueue dataItemQueue lock");
                                lock (db)
                                {
                                    foreach (DataItem item in dataItemQueue)
                                    {
                                        var asdf = db.Add(item);
                                        itemsAdded++;
                                    }
                                }
                                dataItemQueue.Clear();
                            }
                            if (debugLocks) _DebugOutAsync("RunQueue dataItemQueue unlock");
                        }
                    }
                    
                }

                if (debug) Console.WriteLine("DBQueue complete!!!");
                db.SaveChanges();
            });
        }

        public async void AddToQueue(DataItem item)
        {
            //boilerplate debugging
            if (item == null) Console.WriteLine("FuCK");

            await Task.Run(async () =>
            {
                int a = debugCounter++;
                lock (dataItemNext)
                {
                    if (debugCounter == 50 && false)
                    {
                        Console.WriteLine("schfifty");
                        debugCounter++;
                        while (true) ;
                    }
                    if (debugLocks) _DebugOutAsync("AddToQueue dataItemNext lock" + a);
                    dataItemNext.Add(item);
                }
                if (debugLocks) _DebugOutAsync("AddToQueue dataItemNext unlock" + a);
            });
        }

        public async void SetComplete()
        {
            _DebugOutAsync("SetComplete hit!");
            finished = true;
        }

        private async void _DebugReadout()
        {
            while (!finished)
            {
                await Task.Delay((1000));
                Console.WriteLine("Queue! Total items: " + itemsAdded);
                Console.WriteLine("Queue! dataItemQueue count: " + dataItemQueue.Count);
                Console.WriteLine("Queue! dataItemNext count: " + dataItemNext.Count);
            }
        }

        private async void _DebugOutAsync(string output)
        {
            await Task.Run(() =>
            {
                Console.WriteLine(output);
            });
        }
    }

    internal class DBHandler
    {
        //Add the contents of a single folder to the database.
        //Pass 'true' to arg3 to recursively perform this for subdirectories
        public static async Task<ResultCode> AddFolder(DBClass db, System.IO.DirectoryInfo folder, bool recursive)
        {
            System.IO.FileInfo[] files = null;
            

            if (recursive)
            {
                if (await _AddFolderRecursiveContainer(db, folder) == ResultCode.SUCCESS)
                {
                    return ResultCode.SUCCESS;
                }
                else return ResultCode.FAIL;
            }
                        //using (db)
            {
                try
                {
                    files = folder.GetFiles("*");
                    if (files == null) return ResultCode.SUCCESS;
                }
                catch (UnauthorizedAccessException e)
                {
                    _UnauthorizedFileHandler(e);
                }
                catch (Exception e)
                {
                    _MiscExceptionHandler(e);
                }

                if (files != null)
                {
                    foreach (System.IO.FileInfo file in files)
                    {
                        DataItem item = new DataItem { FullPath = file.FullName, CaseInsensitiveFilename = file.Name.ToLower() };
                        if (await AddItem(db, item) == ResultCode.DUPLICATE_FOUND)
                        {
                            _DebugOut("AddFolder: Duplicate file found: " + item.FullPath);
                        }

                    }
                }

                db.SaveChanges();

                return ResultCode.SUCCESS;

            }
        }


        //Pass a path to a single file to add it to the database
        public static void AddItem(DBClass db, string path)
        {

        }



        //Pass a DataItem and it will add it to the database. Duplicate check will be performed
        public static async Task<ResultCode> AddItem(DBClass db, DataItem item)
        {
            if (await _IsDuplicate(db, item) == false)
            {
                db.Add(item);
                return ResultCode.SUCCESS;
            }
            else
            {
                //_DebugOut("Duplicate found: " + item.CaseInsensitiveFilename);
                return ResultCode.DUPLICATE_FOUND;
            }
        }

        
        //Remove a list
        public static ResultCode RemoveItem(DBClass db, PatternList list)
        {
            IQueryable listToDelete = from PatternList in db.PatternLists
                              where PatternList == list
                              select PatternList;



            foreach (PatternList deleteThis in listToDelete)
            {

                IQueryable dataItemPatternListsToDelete = from DataItemPatternList in db.DataItemPatternLists
                                                          where DataItemPatternList.PatternList == list
                                                          select DataItemPatternList;

                foreach (DataItemPatternList associationToDelete in dataItemPatternListsToDelete)
                {
                    db.Remove(associationToDelete);
                }

                try
                {
                    Console.WriteLine("Deleting pattern list: " + deleteThis.pattern);
                    
                    db.Remove(deleteThis);
                }
                catch (Exception e)
                {
                    _DebugOut("Exception thrown when attempting to delete a list: " + e);
                }
            }


            db.SaveChanges();

            return ResultCode.SUCCESS;
        }


        //Remove an item
        public static ResultCode RemoveItem(DBClass db, DataItem item)
        {
            IQueryable validateItem = from DataItem in db.DataItems
                              where DataItem == item
                              select DataItem;

            int foundItems = 0;

            foreach (DataItem test in validateItem)
            {
                foundItems++;
            }

            if (foundItems == 0) return ResultCode.ITEM_NOT_FOUND;

            IQueryable associations = from DataItemPatternList in db.DataItemPatternLists
                                      where DataItemPatternList.DataItem == item
                                      select DataItemPatternList;

            foreach (DataItemPatternList associationToDelete in associations)
            {
                db.Remove(associationToDelete);
            }


            db.Remove(item);

            db.SaveChanges();

            return ResultCode.SUCCESS;
        }


        //Remove items matching a query
        public static void RemoveItems(DBClass db, IQueryable query)
        {
            foreach (DataItem item in query)
            {
                try
                {
                    db.Remove(item);
                }
                catch (Exception e)
                {
                    _MiscExceptionHandler(e);
                }
            }
        }


        //Generate pattern lists based on common patterns. This will only create lists, it doesn't populate them
        public static void GeneratePatterns(DBClass db)
        {
            //Just to get this off the ground, this will currently
            //only generate a pattern for each individual character found in filenames

            IQueryable allFiles = from DataItem in db.DataItems
                                  select DataItem;

            foreach(DataItem item in allFiles)
            {
                for (int character = 0; character < item.CaseInsensitiveFilename.Count(); character++)
                {
                    ResultCode result = _CreatePatternList(db, item.CaseInsensitiveFilename[character].ToString());
                    //if (result == ResultCode.DUPLICATE_FOUND) _DebugOut("Dupey!!!!");
                }
            }

            db.SaveChanges();
        }


        //Populate pattern lists with all relevant DataItems
        public static ResultCode PopulatePatternLists(DBClass db)
        {
            const bool verbose = true;
            const string debugName = "PopulatePatternLists:";

            IQueryable allLists = from PatternList in db.PatternLists
                                  select PatternList;

            foreach (PatternList list in allLists)
            {
                if (verbose)
                {
                    _DebugOut(debugName + "Populating pattern list: " + list.pattern);
                }

                IQueryable matches = from DataItem in db.DataItems
                                     where (DataItem.CaseInsensitiveFilename.Contains(list.pattern)) == true
                                     select DataItem;

                foreach (DataItem match in matches)
                {
                    /*
                    list.DataItems.Add(match);
                    if (verbose)
                    {
                        _DebugOut(debugName + "Match add confirm:" + list.DataItems.Last<DataItem>().CaseInsensitiveFilename);
                        list.Size++;
                    }
                    */

                    DataItemPatternList association = new DataItemPatternList();
                    association.DataItem = match;
                    association.PatternList = list;

                    db.DataItemPatternLists.Add(association);
                }
            }

            db.SaveChanges();
            return ResultCode.SUCCESS;
        }


        //Combine two pattern lists into one. Args 2 and 3 are keys to both lists
        public static void CombinePatternLists(DBClass db, long listPKey1, long listPKey2)
        {

        }


        //Find and combine lists whose members are >similarity% in common
        public static void SimplifyLists(DBClass db, int similarity)
        {

        }
        
        public static void test(DBClass db)
        {
            _PrintAllFiles(db);
        }

        //**********************Private functions below

        //Call this to perform a recursive folder add
        private static async Task<ResultCode> _AddFolderRecursiveContainer(DBClass db, System.IO.DirectoryInfo rootFolder)
        {
            const bool debug = false;
            const string debugName = "_AddFolderRecursiveContainer:";

            DBQueue queue = new DBQueue(db);

            List<System.IO.DirectoryInfo> folders = new List<System.IO.DirectoryInfo>();

            await _AddFolderRecursive(db, rootFolder, folders, queue);

            queue.RunQueue();

            while (folders.Count > 0)
            {
                if (debug)
                {
                    _DebugOut(debugName + "Calling _AddFolderRecursive with :" + folders.Last<DirectoryInfo>().FullName);
                }
                await _AddFolderRecursive(db, folders.Last<DirectoryInfo>(), folders, queue);
            }

            if (debug)
            {
                _DebugOutAsync(debugName + "Complete!");
            }

            queue.SetComplete();

            return ResultCode.SUCCESS;
        }


        //arg1 is db, arg2 is the folder whose files to add, arg3 is a reference to a list which will receive any additional
        //folders found. This function is not to be called directly; _AddFolderRecursiveContainer should be called with the
        //root folder and it will call this as long as more folders are found.
        //True recursion is avoided due to potential stack overflow if there are many directories
        private static async Task<ResultCode> _AddFolderRecursive(DBClass db, System.IO.DirectoryInfo folder, 
            List<System.IO.DirectoryInfo> nextFolder, DBQueue queue)
        {
            const bool debug = false, verbose = false, debugLocks = false;
            const string debugName = "_AddFolderRecursive:";
            
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] folders = null;

            if (verbose)
            {
                _DebugOutAsync(debugName + "Adding: " + folder.FullName);
            }

            lock (nextFolder)
            {
                if (debugLocks) _DebugOutAsync(debugName + "Locked: nextFolder");
                if (nextFolder.Contains(folder)) nextFolder.Remove(folder);
            }
            if (debugLocks) _DebugOutAsync(debugName + "Unlocked: nextFolder");

            //using (db)
            {
                try
                {
                    files = folder.GetFiles("*");
                }
                catch (UnauthorizedAccessException e)
                {
                    _UnauthorizedFileHandler(e);
                }
                catch (Exception e)
                {
                    _MiscExceptionHandler(e);
                }

                try
                {
                    folders = folder.GetDirectories();
                }
                catch (UnauthorizedAccessException e)
                {
                    _UnauthorizedFileHandler(e);
                }
                catch (Exception e)
                {
                    _MiscExceptionHandler(e);
                }

                if (files == null && folders == null) return ResultCode.SUCCESS;

                if (files != null)
                {
                    //Console.WriteLine("Enter for!");
                    foreach (System.IO.FileInfo file in files)
                    {
                        DataItem item = new DataItem { FullPath = file.FullName, CaseInsensitiveFilename = file.Name.ToLower() };

                        //if (await _IsDuplicate(db, item) == false)
                        if (true)
                        {
                            //db.Add(item);
                            queue.AddToQueue(item);
                        }
                        else
                        {
                            _DebugOutAsync("Duplicate found: " + item.CaseInsensitiveFilename);
                        }
                    }
                    //Console.WriteLine("Exit for!");
                }

                if (folders != null)
                {
                    foreach (System.IO.DirectoryInfo foundfolder in folders)
                    {
                        DataItem item =
                            new DataItem { FullPath = foundfolder.FullName, CaseInsensitiveFilename = foundfolder.Name.ToLower() };

                        lock (folders)
                        {
                            if (debugLocks) _DebugOutAsync(debugName + "Locked: folders");
                            nextFolder.Add(foundfolder);
                        }
                        if (debugLocks) _DebugOutAsync(debugName + "Unlocked: folders");

                        if (debug)
                        {
                            _DebugOut(debugName + "Folder found! " + item.FullPath);
                        }
                    }
                }

                db.SaveChanges();

                return ResultCode.SUCCESS;
            }
        }


        //Create a new pattern list. Performs duplicate check.
        private static ResultCode _CreatePatternList(DBClass db, string newPattern)
        {
            const bool debug = true;
            const string debugName = "_CreatePatternList:";

            if (_IsDuplicate(db, newPattern))
            {
                return ResultCode.DUPLICATE_FOUND;
            }

            PatternList newList = new PatternList { pattern = newPattern };

            if (debug) _DebugOut(debugName + "New pattern list added: " + newList.pattern);

            db.Add(newList);

            db.SaveChanges(); 
            //this has to be done here or the next duplicate check will check the DB on disk and miss
            //a possible duplicate

            return ResultCode.SUCCESS;
        }


        //Pass a list key in arg2 and arg3. Return value is a logical AND of those lists in the form 
        //of a List<int> of keys
        private static List<int> _ANDLists(DBClass db, long listPKey1, long listPKey2)
        {
            return new List<int>();
        }


        //Check whether an identical DataItem is already in this database
        private async static Task<bool> _IsDuplicate(DBClass db, DataItem itemToCheck)
        {
            bool foundDupe = false;

            int keysFound = 0;

                
            IQueryable findDupe = from DataItem in db.DataItems
                                    where DataItem.FullPath == itemToCheck.FullPath
                                    select DataItem;

            //await Task.Run(() =>
            {
                lock (db)
                {
                    foreach (DataItem item in findDupe)
                    {
                        
                        foundDupe = true;
                        break;
                    }
                }
            }//);

            if (foundDupe) return true;
            return false;

        }


        //Check whether an identical pattern list is already in this database
        private static bool _IsDuplicate(DBClass db, string pattern)
        {

            //using (db) //this causes issues, maybe because the passed db is already in a using block
            //in a calling function?
            {
                int keysFound = 0;

                IQueryable findDupe = from PatternList in db.PatternLists
                                      where PatternList.pattern == pattern
                                      select PatternList;


                foreach (PatternList foundList in findDupe)
                {
                    keysFound++;
                    if (keysFound > 0)
                    {
                        return true;
                    }
                }

                return false;

            }
        }


        //Call when handling an unauthorized access exception from the file system
        private static void _UnauthorizedFileHandler(Exception e)
        {
            Console.WriteLine("Exceptioun!!!!! Unauthorized File ignored");
        }


        private static void _MiscExceptionHandler(Exception e)
        {
            Console.WriteLine("Exceptioun!!!!!" + e);
        }


        private static void _MiscError(string error)
        {
            Console.WriteLine("DBHandler._MiscError:");
        }


        private static void _DebugOut(string debugText)
        {
            Console.WriteLine("DBHandler._DebugOut: " + debugText);
        }

        private static async void _DebugOutAsync(string debugText)
        {
            await Task.Run(() =>
            {
                Console.WriteLine(debugText);
            });
        }

        //Prints everything in the DataItems table
        private static void _PrintAllFiles(DBClass db)
        {
            Console.WriteLine("Printing all files in the DB:");

            IQueryable items = from DataItem in db.DataItems
                               select DataItem;

            foreach (DataItem item in items)
            {
                Console.WriteLine(item.FullPath);
            }
        }


        
    }

}
