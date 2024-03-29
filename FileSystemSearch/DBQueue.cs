﻿//Copyright 2023 Chris/abstractedfox.
//This work is not licensed for use as source or training data for any language model, neural network,
//AI tool or product, or other software which aggregates or processes material in a way that may be used to generate
//new or derived content from or based on the input set, or used to build a data set or training model for any software or
//tooling which facilitates the use or operation of such software.

//chriswhoprograms@gmail.com


namespace FileSystemSearch
{
    //A class for queueing database actions.
    //This is meant to enable some aspects of large file additions to work asynchronously while
    //keeping actual database writes synchronous
    class DBQueue : IDisposable
    {
        //private List<Task> _dbTasks;
        private List<DataItem> _dataItemQueue, _dataItemNext;
        private Object _dataItemQueueLock = new Object();
        private Object _dataItemNextLock = new Object();
        protected Object? _dbLockObject;
        protected bool _finished;
        private bool _disposed;

        private int _itemsAdded;

        private const bool _debug = false, _debugLocks = false;

        //The caller can read operationsPending to see if operations are still pending
        private bool _setOperationsPending;
        public bool operationsPending
        {
            get
            {
                return _setOperationsPending;
            }
        }

        public DBQueue(object dbLockObject)
        {
            //_dbTasks = new List<Task>();
            _dataItemQueue = new List<DataItem>();
            _dataItemNext = new List<DataItem>();
            _dbLockObject = dbLockObject;
            _finished = false;
            _setOperationsPending = false;
            if (_debug) _DebugReadout();
            _disposed = false;

            _itemsAdded = 0;
        }

        ~DBQueue(){
            if (_disposed) return;
            //_dbTasks.Clear();
            _dataItemQueue.Clear();
            _dataItemNext.Clear();
            _dbLockObject = null;

            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            //_dbTasks.Clear();
            _dataItemQueue.Clear();
            _dataItemNext.Clear();
            _dbLockObject = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public async Task RunQueue()
        {
            const string debugName = "DBQueue.RunQueue():";
            await Task.Run(() => {
                _setOperationsPending = true;

                bool debugQueueInfo = false;
                bool superdebug = false;
                while (true)
                {
                    //Run continuously until the caller says it's done sending data and there is no more data to process.
                    if (_dataItemQueue.Count == 0 && _dataItemNext.Count == 0)
                    {
                        if (_finished)
                        {
                            if (_debug) _DebugOutAsync(debugName + "Break conditions met, exiting RunQueue loop.");
                            break;
                        }
                        continue;
                    }

                    if (superdebug) _DebugOutAsync("RunQueue Continue. _dataItemQueue: " + _dataItemQueue.Count + " _dataItemNext: " + _dataItemNext.Count);

                    //This arrangement is intended to prevent a situation where locking dataItemQueue could defeat the purpose
                    //of this class by making it perform effectively synchronously.
                    if (_dataItemNext.Count > 0 && _dataItemQueue.Count == 0)
                    {
                        lock (_dataItemNextLock)
                        {
                            lock (_dataItemQueueLock)
                            {
                                if (debugQueueInfo) _DebugOutAsync("Flushing " + _dataItemNext.Count + " from dataItemNext");
                                if (_debugLocks) _DebugOutAsync("RunQueue dataItemNext lock");
                                _dataItemQueue = _dataItemNext;
                                
                            }
                            _dataItemNext = new List<DataItem>();
                        }
                        if (_debugLocks) _DebugOutAsync("RunQueue dataItemNext unlock");
                    }

                    if (_dataItemQueue.Count > 0)
                    {
                        lock (_dataItemQueueLock)
                        {
                            if (_debugLocks) _DebugOutAsync("RunQueue dataItemQueue lock");
                            lock (_dbLockObject)
                            {
                                using (DBClass addItemsInstance = new DBClass())
                                {
                                    foreach (DataItem item in _dataItemQueue)
                                    {
                                        addItemsInstance.Add(item);
                                        _itemsAdded++;
                                    }
                                    addItemsInstance.SaveChanges();
                                }
                            }
                            _dataItemQueue.Clear();
                        }
                        if (_debugLocks) _DebugOutAsync("RunQueue dataItemQueue unlock");
                    }
                }

                if (_debug) Console.WriteLine("DBQueue complete!!! Items added: " + _itemsAdded);
                lock (_dbLockObject)
                {
                    _setOperationsPending = false;
                }
                
            });
        }

        public void AddToQueue(DataItem item)
        {
            lock (_dataItemNextLock)
            {
                if (_debugLocks) _DebugOutAsync("AddToQueue dataItemNext lock");
                _dataItemNext.Add(item);
            }
            if (_debugLocks) _DebugOutAsync("AddToQueue dataItemNext unlock");
        }

        public void SetComplete()
        {
            _DebugOutAsync("SetComplete hit! dataItemNext count: " + _dataItemNext.Count + " dataItemQueue count: " + _dataItemQueue.Count);
            _finished = true;
        }

        private async void _DebugReadout()
        {
            while (!_finished || operationsPending)
            {
                await Task.Delay((1000));
                Console.WriteLine("Queue! Total items: " + _itemsAdded);
                Console.WriteLine("Queue! dataItemQueue count: " + _dataItemQueue.Count);
                Console.WriteLine("Queue! dataItemNext count: " + _dataItemNext.Count);
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

}
