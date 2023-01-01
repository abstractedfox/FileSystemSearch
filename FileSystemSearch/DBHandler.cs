
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

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
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Diagnostics;
using FileSystemSearch.Migrations;


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
    class DBQueue : IDisposable
    {
        List<Task> dbTasks;
        List<DataItem> dataItemQueue, dataItemNext;
        Object dataItemQueueLock = new Object();
        Object dataItemNextLock = new Object();
        Object? dbLockObject;
        bool finished;
        private bool _disposed;

        int itemsAdded;

        const bool debug = false, debugLocks = false;

        //The caller can read this to determine if operations are still pending before disposing the db instance
        public bool operationsPending;

        public DBQueue(Object lockObject)
        {
            dbTasks = new List<Task>();
            dataItemQueue = new List<DataItem>();
            dataItemNext = new List<DataItem>();
            dbLockObject = lockObject;
            finished = false;
            operationsPending = false;
            if (debug) _DebugReadout();
            _disposed = false;

            itemsAdded = 0;
        }

        ~DBQueue(){
            if (_disposed) return;
            dbTasks.Clear();
            dataItemQueue.Clear();
            dataItemNext.Clear();
            dbLockObject = null;

            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            dbTasks.Clear();
            dataItemQueue.Clear();
            dataItemNext.Clear();
            dbLockObject = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public async void RunQueue()
        {
            const string debugName = "DBQueue.RunQueue():";
            await Task.Run(() => {
                operationsPending = true;

                bool debugQueueInfo = false;
                bool superdebug = false;
                while (true)
                {
                    //Run continuously until the caller says it's done sending data.
                    if (finished == true && dataItemQueue.Count == 0 && dataItemNext.Count == 0)
                    {
                        if (debug) _DebugOutAsync(debugName + "Break conditions met, exiting RunQueue loop.");
                        break;
                    }
                    if (dataItemQueue.Count == 0 && dataItemNext.Count == 0)
                    {
                        continue;
                    }

                    if (superdebug) _DebugOutAsync("RunQueue Continue");

                    //This arrangement is intended to prevent a situation where locking dataItemQueue could defeat the purpose
                    //of this class by making it perform effectively synchronously.
                    if (dataItemNext.Count > 0 && dataItemQueue.Count == 0)
                    {
                        lock (dataItemNextLock)
                        {
                            lock (dataItemQueueLock)
                            {
                                if (debugQueueInfo) _DebugOutAsync("Flushing " + dataItemNext.Count + " from dataItemNext");
                                if (debugLocks) _DebugOutAsync("RunQueue dataItemNext lock");
                                foreach (DataItem item in dataItemNext)
                                {
                                    dataItemQueue.Add(item);
                                }
                                //if (debug) _DebugOutAsync("Clearing dataItemNext of " + dataItemNext.Count + " items.");
                            }

                            dataItemNext.Clear();
                        }
                        if (debugLocks) _DebugOutAsync("RunQueue dataItemNext unlock");
                    }

                    if (dataItemQueue.Count > 0)
                    {
                        lock (dataItemQueueLock)
                        {
                            if (debugLocks) _DebugOutAsync("RunQueue dataItemQueue lock");
                            lock (dbLockObject)
                            {
                                using (DBClass addItemsInstance = new DBClass())
                                {
                                    foreach (DataItem item in dataItemQueue)
                                    {
                                        addItemsInstance.Add(item);
                                        itemsAdded++;
                                    }
                                    addItemsInstance.SaveChanges();
                                }
                            }
                            dataItemQueue.Clear();
                        }
                        if (debugLocks) _DebugOutAsync("RunQueue dataItemQueue unlock");
                    }
                }


                if (debug) Console.WriteLine("DBQueue complete!!! Items added: " + itemsAdded);
                lock (dbLockObject)
                {
                    //db.SaveChanges(); //No longer needed
                    operationsPending = false;
                }

                
            });
        }

        public async void AddToQueue(DataItem item)
        {
            //This isn't awaited because we don't want the caller loop to block waiting for this to return
            Task.Run(() =>
            {
                lock (dataItemNextLock)
                {
                    if (debugLocks) _DebugOutAsync("AddToQueue dataItemNext lock");
                    dataItemNext.Add(item);
                }
                if (debugLocks) _DebugOutAsync("AddToQueue dataItemNext unlock");
            });
        }

        public void SetComplete()
        {
            _DebugOutAsync("SetComplete hit! dataItemNext count: " + dataItemNext.Count + " dataItemQueue count: " + dataItemQueue.Count);
            finished = true;
        }

        private async void _DebugReadout()
        {
            while (!finished || operationsPending)
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


    //A class for sorting items into pattern lists and checking for duplicates.
    //This is a stateful class; it must be instantiated. This is done so the caller can cancel running operations without
    //waiting for them to finish
    /*
    class Housekeeping
    {
        const bool debug = true;

        Object db;
        int taskLimit;
        List<Task> runningTasks;
        bool cancelHousekeeping;
        public bool isHousekeeping;

        List<DataItem> dbCopy;

        public delegate void ResultReturn(DataItem result, ResultCode code);

        public Housekeeping(Object dbToUse)
        {
            runningTasks = new List<Task>();
            dbCopy = new List<DataItem>();
            db = dbToUse;
            taskLimit = 8;
            isHousekeeping = false;
            cancelHousekeeping = false;
        }

        public async Task StartHousekeeping(ResultReturn resultOut)
        {
            const bool debug = false;
            const string debugName = "Housekeeping.StartHousekeeping():";

            if (debug) _debugOut(debugName + "Start");

            if (isHousekeeping == true)
            {
                if (debug) _debugOut(debugName + "StartHousekeeping is already running.");
                return;
            }

            isHousekeeping = true;
            await Task.Run(() => {
                while (!cancelHousekeeping)
                {
                    IQueryable unprocessedFiles = from DataItem in db.DataItems
                                                  where DataItem.HasBeenDuplicateChecked == false
                                                  select DataItem;

                    List<DataItem> itemsToProcess = new List<DataItem>();

                    lock (db)
                    {
                        foreach (DataItem item in unprocessedFiles)
                        {
                            itemsToProcess.Add(item);
                        }
                    }

                    if (debug) _debugOut(debugName + "Unprocessed files: " + itemsToProcess.Count);


                    for (int i = 0; i < itemsToProcess.Count; i++)
                    {
                        while (GetPendingTasks() > taskLimit) ; //Block if the task limit is hit
                        _processFile(itemsToProcess[i], resultOut);
                    }

                    while (GetPendingTasks() > 0) ; //Block if any tasks are incomplete
                    
                    lock (db)
                    {
                        db.SaveChanges();
                    }

                    //Having this self-terminate may be less desirable if this is split off into a persistent process later
                    cancelHousekeeping = true;
                }
                isHousekeeping = false;
                if (debug) _debugOut(debugName + "Housekeeping has ended.");
            });
        }


        public void CancelHousekeeping()
        {
            const bool debug = true;
            if (debug) _debugOut("CancelHousekeeping called");
            cancelHousekeeping = true;
        }


        //Sets the local dbCopy list to a passed list. Remember that lists are a reference type.
        public void SetDBCopy(List<DataItem> dbCopySource)
        {
            dbCopy = dbCopySource; 
        }


        //Performs a dupe-check on the contents of dbCopy only. Marks any found duplicates for deletion.
        //Slow, but technically faster than doing it via database queries.
        public int DupeCheckFast()
        {
            int dupesFound = 0;
            int iterations = 0;
            if (dbCopy.Count > 0)
            {
                for (int outer = 0; outer < dbCopy.Count; outer++)
                {
                    _debugOut(iterations++.ToString());
                    if (dbCopy[outer].HasBeenDuplicateChecked == false)
                    {
                        for (int inner = outer + 1; inner < dbCopy.Count; inner++)
                        {
                            if (dbCopy[outer].FullPath == dbCopy[inner].FullPath && dbCopy[outer].Id != dbCopy[inner].Id)
                            {
                                lock (dbCopy)
                                {
                                    dbCopy[outer].MarkForDeletion = true;
                                    dupesFound++;
                                }
                            }
                            lock (dbCopy)
                            {
                                dbCopy[outer].HasBeenDuplicateChecked = true;
                            }
                        }
                    }
                }
            }

            return dupesFound;
        }

        //Check a single file for duplicates and add it to pattern lists. sourceList is the list of DataItems that is supplying
        //this function; if a duplicate is found, this function will remove it on its own.
        private async void _processFile(DataItem item, ResultReturn resultOut)
        {
            const bool debug = false;
            const string debugName = "Housekeeping._processFile():";
            runningTasks.Add(Task.Run(() => {
                if (debug) _debugOut(debugName + "Processing " + item.FullPath);

                
                if (_isDuplicate(item))
                {
                    if (debug) _debugOut(debugName + "Duplicate found: " + item.FullPath);
                    resultOut(item, ResultCode.DUPLICATE_FOUND);
                    lock (db)
                    {
                        DBHandler.RemoveItem(db, item);
                    }
                    return;
                }

                //We're going to leave all this out; the pattern list functionality may not be needed after all
                if (false)
                {

                    if (debug) _debugOut(debugName + "Checking for new pattern lists.");

                    //Check whether any new pattern lists need to be made
                    for (int i = 0; i < item.CaseInsensitiveFilename.Count(); i++)
                    {
                        if (DataSearch.FindMatchingPatternLists(db, item.CaseInsensitiveFilename[i].ToString()).Count() == 0)
                        {
                            lock (db)
                            {
                                ResultCode result = DBHandler.CreatePatternList(db, item.CaseInsensitiveFilename[i].ToString());
                            }
                        }
                    }

                    if (debug) _debugOut(debugName + "Building pattern list associations.");

                    //Create any necessary associations with pattern lists
                    List<PatternList> relevantLists = DataSearch.FindMatchingPatternLists(db, item.CaseInsensitiveFilename);

                    foreach (PatternList list in relevantLists)
                    {
                        DataItemPatternList association = new DataItemPatternList();
                        association.DataItem = item;
                        association.PatternList = list;
                        lock (db)
                        {
                            db.DataItemPatternLists.Add(association);
                        }
                    }
                }

                lock (db)
                {
                    item.HasBeenDuplicateChecked = true; //We're gonna have to make sure this is actually updating the db
                }

                if (debug) _debugOut(debugName + "Done");
            }));
        }

        public int GetPendingTasks()
        {
            int results = 0;

            for (int i = 0; i < runningTasks.Count; i++)
            {

                if (runningTasks[i].IsCompleted == false) results++;
                /*
                if (runningTasks.Count > 500)
                {
                    RemoveCompletedTasks();
                }
                
            }
            return results;
        }

        //Temporarily out of use as it caused concurrency issues. Also worth measuring if/when this is actually significant to performance
        public async void RemoveCompletedTasks()
        {
            const string debugName = "Housekeeping.RemoveCompletedTasks():";
            await Task.Run(() =>
            {
                _debugOut(debugName + "Flushing completed tasks");
                lock (runningTasks)
                {
                    for (int i = runningTasks.Count - 1; i > 0; i--) {
                        if (runningTasks[i].IsCompleted) runningTasks.RemoveAt(i);
                    }
                }
            });
        }

        private async void _debugOut(string debugText)
        {
            await Task.Run(() => { Console.WriteLine(debugText); });
        }

        private bool _isDuplicate(DataItem item)
        {
            IQueryable dupeCheck = from DataItem in db.DataItems
                                    where DataItem.FullPath == item.FullPath && DataItem.Id != item.Id
                                    select DataItem;

            bool foundDupe = false;

            lock (db)
            {
                foreach (DataItem possibledupe in dupeCheck)
                {
                    foundDupe = true;
                    break;
                }
            }

            return foundDupe;

            
        }
    }
    */
    internal class DBHandler
    {
        //Add the contents of a single folder to the database.
        //Pass 'true' to arg3 to recursively perform this for subdirectories.
        //Pass 'true' to setDuplicateFlag to automatically set all files as non-duplicates.
        //Recursive is permanently true now. Have fun!
        public static async Task<ResultCode> AddFolder(Object dbLockObject, System.IO.DirectoryInfo folder, bool recursive, bool setDuplicateFlag)
        {
            System.IO.FileInfo[] files = null;

            recursive = true;

            if (recursive)
            {
                if (await _AddFolderRecursiveContainer(dbLockObject, folder, setDuplicateFlag) == ResultCode.SUCCESS)
                {
                    return ResultCode.SUCCESS;
                }
                else return ResultCode.FAIL;
            }

            return ResultCode.FAIL;

        }


        //Pass a path to a single file to add it to the database. Currently empty as it's not super necessary right now
        public static void AddItem(Object db, string path)
        {

        }


        //Add a single DataItem. Mildly unnecessary but it's here if you want it
        public static async Task<ResultCode> AddItem(Object dbLockObject, DataItem item)
        {
            try
            {
                lock (dbLockObject)
                {
                    using (DBClass db = new DBClass())
                    {
                        db.Add(item);
                        db.SaveChanges();
                        return ResultCode.SUCCESS;
                    }
                }
            }
            catch
            {
                return ResultCode.FAIL;
            }
        }

        
        //Remove a list
        public static ResultCode RemoveItem(Object dbLockObject, PatternList list)
        {
            using (DBClass db = new DBClass())
            {
                lock (dbLockObject)
                {
                    IQueryable listToDelete = from PatternList in db.PatternLists
                                              where PatternList == list
                                              select PatternList;



                    foreach (PatternList deleteThis in listToDelete)
                    {

                        IQueryable dataItemPatternListsToDelete = from DataItemPatternList in db.DataItemPatternLists
                                                                  where DataItemPatternList.PatternList == list
                                                                  select DataItemPatternList;

                        foreach (DataItem associationToDelete in dataItemPatternListsToDelete)
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
                }

                return ResultCode.SUCCESS;
            }
        }


        //Remove an item
        public static ResultCode RemoveItem(Object dbLockObject, DataItem item)
        {
            lock (dbLockObject) 
            {
                using (DBClass db = new DBClass())
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


                    foreach (DataItem associationToDelete in associations)
                    {
                        db.Remove(associationToDelete);
                    }


                    db.Remove(item);

                    db.SaveChanges();
                }
                return ResultCode.SUCCESS;
            }
        }


        //Remove items matching a query. Doesn't automatically save changes.
        public static void RemoveItems(Object dbLockObject, IQueryable query)
        {
            lock (dbLockObject)
            {
                using (DBClass removeItemsInstance = new DBClass())
                {
                
                    foreach (DataItem item in query)
                    {
                        try
                        {
                            removeItemsInstance.Remove(item);
                        }
                        catch (Exception e)
                        {
                            _MiscExceptionHandler(e);
                        }
                    }
                    removeItemsInstance.SaveChanges();
                }
            }

            GC.Collect();
        }


        //Clears the entire database
        public static void Clear(Object dbLockObject)
        {
            lock (dbLockObject)
            {
                using (DBClass removeItemsInstance = new DBClass())
                {
                    IQueryable dataItems = from DataItem in removeItemsInstance.DataItems
                                           select DataItem;

                    IQueryable patternLists = from PatternList in removeItemsInstance.PatternLists
                                              select PatternList;

                    IQueryable dataItemPatternLists = from DataItemPatternList in removeItemsInstance.DataItemPatternLists
                                                      select DataItemPatternList;

                    if (removeItemsInstance.DataItems.Count() > 0) RemoveItems(removeItemsInstance, dataItems);
                    if (removeItemsInstance.PatternLists.Count() > 0) RemoveItems(removeItemsInstance, patternLists);
                    if (removeItemsInstance.DataItemPatternLists.Count() > 0) RemoveItems(removeItemsInstance, dataItemPatternLists);
                    removeItemsInstance.SaveChanges();
                }
            }
            GC.Collect();
        }


        //Verify that an item exists in the file system
        public static ResultCode VerifyItem(DataItem item)
        {
            if (item == null)
            {
                return ResultCode.NULLCODE;
            }

            if (!item.IsFolder)
            {
                FileInfo file = new FileInfo(item.FullPath);

                if (file.Exists) return ResultCode.SUCCESS;
            }
            else
            {
                DirectoryInfo dir = new DirectoryInfo(item.FullPath);
                if (dir.Exists) return ResultCode.SUCCESS;
            }
            return ResultCode.FAIL;
        }


        //Create a new pattern list. Performs duplicate check.
        /*
        public static ResultCode CreatePatternList(Object dbLockObject, string newPattern)
        {
            const bool debug = true;
            const string debugName = "_CreatePatternList:";

            lock (dbLockObject)
            {
                using (DBClass db = new DBClass())
                {
                    if (_IsDuplicate(db, newPattern))
                    {
                        return ResultCode.DUPLICATE_FOUND;
                    }

                    PatternList newList = new PatternList { pattern = newPattern };

                    if (debug) _DebugOut(debugName + "New pattern list added: " + newList.pattern);

                    lock (db)
                    {
                        db.Add(newList);

                        db.SaveChanges();
                    }
                    //this has to be done here or the next duplicate check will check the DB on disk and miss
                    //a possible duplicate
                }
            }
            return ResultCode.SUCCESS;
        }
        */

        //Generate pattern lists based on common patterns. This will only create lists, it doesn't populate them.
        //Review if we actually need this
        /*
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
                    ResultCode result = CreatePatternList(db, item.CaseInsensitiveFilename[character].ToString());

                    //if (result == ResultCode.DUPLICATE_FOUND) _DebugOut("Dupey!!!!");
                }
            }

            db.SaveChanges();
        }*/


        //Populate pattern lists with all relevant DataItems
        //Review if we actually need this
        /*
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

                    DataItemPatternList association = new DataItemPatternList();
                    association.DataItem = match;
                    association.PatternList = list;

                    db.DataItemPatternLists.Add(association);
                }

            }

            db.SaveChanges();
            return ResultCode.SUCCESS;
        }*/



        //Format a database to a list. This is slow and will consume a lot of memory.
        public static List<DataItem> DBToList(Object dbLockObject)
        {
            lock (dbLockObject)
            {
                using (DBClass db = new DBClass())
                {
                    const bool debug = true;

                    if (debug) _DebugOut("DBToList: Start");

                    List<DataItem> theList = new List<DataItem>();

                    IQueryable theItems = from DataItem in db.DataItems select DataItem;

                    foreach (DataItem item in theItems) theList.Add(item);

                    if (debug) _DebugOut("DBToList: End");

                    return theList;
                }
            }
        }
        

        //Boilerplate test function
        public static void test(DBClass db)
        {
            _PrintAllFiles(db);
        }

        //**********************Private functions below

        //Call this to perform a recursive folder add. Set setDuplicateFlag = true to mark all results as duplicate checked
        private static async Task<ResultCode> _AddFolderRecursiveContainer(Object dbLockObject, System.IO.DirectoryInfo rootFolder, bool setDuplicateFlag)
        {
            const bool debug = true;
            const string debugName = "_AddFolderRecursiveContainer:";

            using (DBQueue queue = new DBQueue(dbLockObject))
            {

                List<System.IO.DirectoryInfo> folders = new List<System.IO.DirectoryInfo>();

                //Note: RunQueue must start before the first folder add; concurrency issues can otherwise cause this function
                //to return before the queue has begun processing
                queue.RunQueue();

                await _AddFolderRecursive(dbLockObject, rootFolder, folders, queue, setDuplicateFlag);

                while (folders.Count > 0)
                {
                    if (debug)
                    {
                        _DebugOutAsync(debugName + "Calling _AddFolderRecursive with :" + folders.Last<DirectoryInfo>().FullName);
                    }
                    await _AddFolderRecursive(dbLockObject, folders.Last<DirectoryInfo>(), folders, queue, setDuplicateFlag);
                }

                queue.SetComplete();

                while (queue.operationsPending) ; //Block until pending operations are completed

                if (debug)
                {
                    _DebugOutAsync(debugName + "Complete!");
                }
            }
            return ResultCode.SUCCESS;
        }


        //arg1 is the lock object, arg2 is the folder whose files to add, arg3 is a reference to a list which will receive any additional
        //folders found. This function is not to be called directly; _AddFolderRecursiveContainer should be called with the
        //root folder and it will call this as long as more folders are found.
        //True recursion is avoided due to potential stack overflow if there are many directories.
        //Set setDuplicateFlag = true to mark all results as duplicate-checked
        private static async Task<ResultCode> _AddFolderRecursive(Object dbLockObject, System.IO.DirectoryInfo folder, 
            List<System.IO.DirectoryInfo> nextFolder, DBQueue queue, bool setDuplicateFlag)
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

            DataItem thisFolder = new DataItem();
            thisFolder.FullPath = folder.FullName;
            thisFolder.CaseInsensitiveFilename = folder.Name.ToLower();
            //if (setDuplicateFlag) thisFolder.HasBeenDuplicateChecked = true;
            thisFolder.HasBeenDuplicateChecked = setDuplicateFlag;
            thisFolder.IsFolder = true;
            queue.AddToQueue(thisFolder);

            if (files == null && folders == null) return ResultCode.SUCCESS;

            if (files != null)
            {

                foreach (System.IO.FileInfo file in files)
                {
                    DataItem item = new DataItem { FullPath = file.FullName, CaseInsensitiveFilename = file.Name.ToLower() };
                    if (setDuplicateFlag) item.HasBeenDuplicateChecked = true;
                    queue.AddToQueue(item);
                }

            }
            else
            {
                if (debug) _DebugOutAsync("No files found in " + folder.FullName + " or files is null for some other reason.");
            }

            if (folders != null)
            {
                foreach (System.IO.DirectoryInfo foundfolder in folders)
                {
                    DataItem item =
                        new DataItem { FullPath = foundfolder.FullName, CaseInsensitiveFilename = foundfolder.Name.ToLower(),
                        IsFolder = false };

                    item.HasBeenDuplicateChecked = setDuplicateFlag;

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

            return ResultCode.SUCCESS;
            
        }


        //Check whether an identical DataItem is already in this database
        private async static Task<bool> _IsDuplicate(Object dbLockObject, DataItem itemToCheck)
        {
            lock (dbLockObject)
            {
                using (DBClass db = new DBClass())
                {
                    bool foundDupe = false;

                    int keysFound = 0;

                    IQueryable findDupe = from DataItem in db.DataItems
                                          where DataItem.FullPath == itemToCheck.FullPath
                                          select DataItem;

                    //await Task.Run(() =>
                    {
                        {
                            foreach (DataItem item in findDupe)
                            {
                                foundDupe = true;
                                break;
                            }
                        }
                    }

                    if (foundDupe) return true;
                    return false;
                }
            }

        }


        //Check whether an identical pattern list is already in this database
        /*
        private static bool _IsDuplicate(Object db, string pattern)
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
        }*/


        //Call when handling an unauthorized access exception from the file system
        private static void _UnauthorizedFileHandler(Exception e)
        {
            _DebugOutAsync("Exceptioun!!!!! Unauthorized File ignored");
        }


        private static void _MiscExceptionHandler(Exception e)
        {
            _DebugOutAsync("Exceptioun!!!!!" + e);
        }


        private static void _MiscError(string error)
        {
            _DebugOutAsync("DBHandler._MiscError:" + error);
        }


        private static void _DebugOut(string debugText)
        {
            _DebugOutAsync("DBHandler._DebugOut: " + debugText);
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
