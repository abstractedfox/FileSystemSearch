//Copyright 2023 Chris/abstractedfox.
//This work is not licensed for use as source or training data for any language model, neural network,
//AI tool or product, or other software which aggregates or processes material in a way that may be used to generate
//new or derived content from or based on the input set, or used to build a data set or training model for any software or
//tooling which facilitates the use or operation of such software.

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

        //Add a single DataItem
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

        //Call this to perform a recursive folder add.
        private static async Task<ResultCode> _AddFolderRecursiveContainer(Object dbLockObject, System.IO.DirectoryInfo rootFolder, bool setDuplicateFlag)
        {
            const bool debug = true;
            const string debugName = "_AddFolderRecursiveContainer:";
            
            using (DBQueue queue = new DBQueue(dbLockObject))
            {
                List<System.IO.DirectoryInfo> folders = new List<System.IO.DirectoryInfo>();

                //Note: RunQueue must start before the first folder add; concurrency issues can otherwise cause this function
                //to return before the queue has begun processing
                //Additional note: This may no longer be true, but I'm leaving the first note in case deleting it causes terrible things to happen
                Task queueTask = queue.RunQueue();

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

                //while (queue.operationsPending) ; //Block until pending operations are completed

                await queueTask; //Block until pending operations are completed

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
