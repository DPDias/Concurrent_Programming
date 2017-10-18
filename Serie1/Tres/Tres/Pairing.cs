using System;
using System.Collections.Generic;
using System.Threading;

namespace Tres
{
    public class Pairing<T, U>
    {
        private readonly LinkedList<Container<T, U>> pairList = new LinkedList<Container<T, U>>();
        private readonly Object mon = new Object();

        public Tuple<T, U> Provide(T value, int timeout)
        {
            Monitor.Enter(mon);        
            try
            {
                if (pairList.Count > 0 && pairList.First.Value.uIsPresent)
                {
                    LinkedListNode<Container<T, U>> a = pairList.First;
                    pairList.RemoveFirst();
                    a.Value.tuple.t = value;
                    a.Value.tIsPresent = true;                
                    SyncUtils.Pulse(mon, a);
                    return a.Value.tuple;
                }

                if (timeout == 0)
                    throw new TimeoutException();

                LinkedListNode<Container<T, U>> node = pairList.AddLast(new Container<T, U>(value));

                var time = new TimeoutInstant(timeout);

                while (true)
                {
                    try
                    {
                        SyncUtils.Wait(mon, node, timeout /* alterar*/);
                    }
                    catch (ThreadInterruptedException e)
                    {
                        if (node.Value.uIsPresent)
                        {
                            Thread.CurrentThread.Interrupt();
                            return node.Value.tuple;
                        }
                        pairList.Remove(node);
                        throw;
                    }

                    if (node.Value.uIsPresent)
                    {
                        return node.Value.tuple;
                    }

                    if (time.IsTimeout)
                    {
                        pairList.Remove(node);
                        throw new TimeoutException();
                    }

                }
            }
            finally
            {
                Monitor.Exit(mon);
            }
        }

        public Tuple<T, U> Provide(U value, int timeout)
        {
            Monitor.Enter(mon);         // verificar se tem problemas
            try
            {
                if (pairList.Count > 0 && pairList.First.Value.tIsPresent)
                {
                    LinkedListNode<Container<T, U>> a = pairList.First;
                    pairList.RemoveFirst();
                    a.Value.tuple.u = value;
                    a.Value.uIsPresent = true;
                    SyncUtils.Pulse(mon, a);
                    return a.Value.tuple;
                }

                if (timeout == 0)
                    throw new TimeoutException();

                LinkedListNode<Container<T, U>> node = pairList.AddLast(new Container<T, U>(value));

                var time = new TimeoutInstant(timeout);

                while (true)
                {
                    try
                    {
                        SyncUtils.Wait(mon, node, time.Remaining);
                    }
                    catch (ThreadInterruptedException e)
                    {
                        if (node.Value.tIsPresent)
                        {
                            Thread.CurrentThread.Interrupt();
                            return node.Value.tuple;
                        }
                        pairList.Remove(node);
                        throw;
                    }

                    if (node.Value.tIsPresent)
                    {
                        return node.Value.tuple;
                    }

                    if (time.IsTimeout)
                    {
                        pairList.Remove(node);
                        throw new TimeoutException();
                    }
                }
            }
            finally
            {
                Monitor.Exit(mon);
            }
        }

        public class Container<T, U>
        {
            public bool uIsPresent;
            public bool tIsPresent;
            public Tuple<T, U> tuple;
            public Container(T t)
            {
                uIsPresent = false;
                tIsPresent = true;
                tuple = new Tuple<T, U>(t);
            }

            public Container(U u)
            {
                uIsPresent = true;
                tIsPresent = false;
                tuple = new Tuple<T, U>(u);
            }
        }  
    }

    public class Tuple<T, U>
    {
        public T t;
        public U u;
        public Tuple(T t)
        {
            this.t = t;
            this.u = default(U);
        }
        public Tuple(U u)
        {
            this.t = default(T);
            this.u = u;
        }
    }

    
}
