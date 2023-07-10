//Copyright 2023 Chris/abstractedfox.
//This work is not licensed for use as source or training data for any language model, neural network,
//AI tool or product, or other software which aggregates or processes material in a way that may be used to generate
//new or derived content from or based on the input set, or used to build a data set or training model for any software or
//tooling which facilitates the use or operation of such software.

//chriswhoprograms@gmail.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

//Experiment for condensing DBQueue to use a single collection instead of two.
//Not actually that performant! But let's keep it and see about tuning it up later.
namespace FileSystemSearch.Experimental
{
    internal class DBQueueBetter : DBQueue
    {
        private LinkedList<DataItem> _dataItemQueue = new LinkedList<DataItem>();
        private LinkedListNode<DataItem> lockNode;
        private object _localLockObj = new object();

        public DBQueueBetter(object dbLockObject) : base(dbLockObject)
        {
        }

        private bool loopCondition()
        {
            lock (_localLockObj)
            {
                return !_finished || !_finished && _dataItemQueue.Count == 0;
            }
        }


        new public async Task RunQueue()
        {
            await Task.Run(() =>
            {
                while (loopCondition())
                {
                    Console.WriteLine("main loop continue");
                    while (new Func<bool>(() =>
                    {
                        lock (_localLockObj)
                        {
                            //If the first item in the list is not the lock node, or if the caller is done sending data,
                            //pop the first item. If the first item is null, don't do anything.

                            return _dataItemQueue.First != null &&
                            _dataItemQueue.First != lockNode// && _dataItemQueue.First.Next != lockNode) 
                            || _finished && _dataItemQueue.Count > 0 && _dataItemQueue.First != null;
                        }
                    })())
                    {
                        lock (_dbLockObject)
                        {
                            if (_dataItemQueue.First == null)
                            {
                                continue;
                            }
                            using (DBClass addItemsInstance = new DBClass())
                            {
                                //Console.WriteLine("queue size:" + _dataItemQueue.Count);
                                var jawn = addItemsInstance.Add(_dataItemQueue.First.Value);
                                var test = addItemsInstance.SaveChanges();
                            }
                        }
                        _dataItemQueue.RemoveFirst();
                    }
                }
                Console.WriteLine("task exit");
            });
            Console.WriteLine("function exit");
        }

        new public void AddToQueue(DataItem item)
        {
            lock (_localLockObj)
            {
                if (_dataItemQueue.Count == 0)
                {
                    _dataItemQueue.AddLast(item);
                    lockNode = _dataItemQueue.Last;
                    return;
                }
            }

            lock (_localLockObj)
            {
                _dataItemQueue.AddLast(item);
                lockNode = _dataItemQueue.Last.Previous;
            }
        }
    }
}
