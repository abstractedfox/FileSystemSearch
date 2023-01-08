//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com




namespace FileSystemSearch
{
    //A class for queueing database actions.
    //This is meant to enable some aspects of large file additions to work asynchronously while
    //keeping actual database writes synchronous
    class DBQueue : IDisposable
    {
        private List<Task> _dbTasks;
        private List<DataItem> _dataItemQueue, _dataItemNext;
        private Object _dataItemQueueLock = new Object();
        private Object _dataItemNextLock = new Object();
        private Object? _dbLockObject;
        private bool _finished;
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

        public DBQueue(Object lockObject)
        {
            _dbTasks = new List<Task>();
            _dataItemQueue = new List<DataItem>();
            _dataItemNext = new List<DataItem>();
            _dbLockObject = lockObject;
            _finished = false;
            _setOperationsPending = false;
            if (_debug) _DebugReadout();
            _disposed = false;

            _itemsAdded = 0;
        }

        ~DBQueue(){
            if (_disposed) return;
            _dbTasks.Clear();
            _dataItemQueue.Clear();
            _dataItemNext.Clear();
            _dbLockObject = null;

            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _dbTasks.Clear();
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

                    if (superdebug) _DebugOutAsync("RunQueue Continue");

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
                                foreach (DataItem item in _dataItemNext)
                                {
                                    _dataItemQueue.Add(item);
                                }
                            }

                            _dataItemNext.Clear();
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

        public async void AddToQueue(DataItem item)
        {
            //This isn't awaited because we don't want the caller loop to block waiting for this to return
            Task.Run(() =>
            {
                lock (_dataItemNextLock)
                {
                    if (_debugLocks) _DebugOutAsync("AddToQueue dataItemNext lock");
                    _dataItemNext.Add(item);
                }
                if (_debugLocks) _DebugOutAsync("AddToQueue dataItemNext unlock");
            });
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
