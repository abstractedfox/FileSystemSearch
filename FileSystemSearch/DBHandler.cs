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

    internal class DBHandler
    {
        //Add the contents of a single folder to the database.
        //Pass 'true' to arg3 to recursively perform this for subdirectories
        public static ResultCode AddFolder(DBClass db, System.IO.DirectoryInfo folder, bool recursive)
        {
            System.IO.FileInfo[] files = null;

            if (recursive)
            {
                if (_AddFolderRecursiveContainer(db, folder) == ResultCode.SUCCESS)
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
                        if (AddItem(db, item) == ResultCode.DUPLICATE_FOUND)
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
        public static ResultCode AddItem(DBClass db, DataItem item)
        {
            if (!_IsDuplicate(db, item))
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
            IQueryable items = from PatternList in db.PatternLists
                              where PatternList == list
                              select PatternList;


            foreach (PatternList listToDelete in items)
            {
                try
                {
                    listToDelete.DataItems.Clear();
                    Console.WriteLine("jawns: " + listToDelete.DataItems.Count());
                    
                    db.Remove(listToDelete);
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
                    list.DataItems.Add(match);
                    if (verbose)
                    {
                        _DebugOut(debugName + "Match add confirm:" + list.DataItems.Last<DataItem>().CaseInsensitiveFilename);
                        list.Size++;
                    }
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


        //**********************Private functions below

        //Call this to perform a recursive folder add
        private static ResultCode _AddFolderRecursiveContainer(DBClass db, System.IO.DirectoryInfo rootFolder)
        {
            const bool debug = false;
            const string debugName = "_AddFolderRecursiveContainer:";

            List<System.IO.DirectoryInfo> folders = new List<System.IO.DirectoryInfo>();

            _AddFolderRecursive(db, rootFolder, folders);

            while (folders.Count > 0)
            {
                if (debug)
                {
                    _DebugOut(debugName + "Calling _AddFolderRecursive with :" + folders.Last<DirectoryInfo>().FullName);
                }
                _AddFolderRecursive(db, folders.Last<DirectoryInfo>(), folders);
            }

            return ResultCode.SUCCESS;
        }


        //arg1 is db, arg2 is the folder whose files to add, arg3 is a reference to a list which will receive any additional
        //folders found. This function is not to be called directly; _AddFolderRecursiveContainer should be called with the
        //root folder and it will call this as long as more folders are found.
        //True recursion is avoided due to potential stack overflow if there are many directories
        private static ResultCode _AddFolderRecursive(DBClass db, System.IO.DirectoryInfo folder, List<System.IO.DirectoryInfo> nextFolder)
        {
            const bool debug = false;
            const string debugName = "_AddFolderRecursive:";
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] folders = null;

            if (nextFolder.Contains(folder)) nextFolder.Remove(folder);

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
                    foreach (System.IO.FileInfo file in files)
                    {
                        DataItem item = new DataItem { FullPath = file.FullName, CaseInsensitiveFilename = file.Name.ToLower() };
                        if (!_IsDuplicate(db, item))
                        {
                            db.Add(item);
                        }
                        else
                        {
                            _DebugOut("Duplicate found: " + item.CaseInsensitiveFilename);
                        }
                    }
                }

                if (folders != null)
                {
                    foreach (System.IO.DirectoryInfo foundfolder in folders)
                    {
                        DataItem item =
                            new DataItem { FullPath = foundfolder.FullName, CaseInsensitiveFilename = foundfolder.Name.ToLower() };

                        nextFolder.Add(foundfolder);

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

            PatternList newList = new PatternList { pattern = newPattern, Size = 0 };

            if (debug) _DebugOut(debugName + "New pattern list added: " + newList.pattern);

            db.Add(newList);

            db.SaveChanges(); 
            //this has to be done here or the next duplicate check will check the DB on disk and miss
            //a possible duplicate

            return ResultCode.SUCCESS;
        }


        //Pass a list key in arg2 and arg3. Return value is a logical AND of those lists in the form 
        //of a List<int> of keys
        private static List<int> ANDLists(DBClass db, long listPKey1, long listPKey2)
        {
            return new List<int>();
        }


        //Check whether an identical DataItem is already in this database
        private static bool _IsDuplicate(DBClass db, DataItem itemToCheck)
        {
            
            //using (db) //this causes issues, maybe because the passed db is already in a using block
            //in a calling function?
            {
                int keysFound = 0;

                
                IQueryable findDupe = from DataItem in db.DataItems
                                      where DataItem.FullPath == itemToCheck.FullPath
                                      select DataItem;


                foreach (DataItem item in findDupe)
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
