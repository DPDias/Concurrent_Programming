using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tres
{
    public class Pairing<T, U>
    {
        private readonly LinkedList<Tuple<T, U>> pairList = new LinkedList<Tuple<T, U>>();
        private readonly Object mon = new Object();

        public Tuple<T, U> Provide(T value, int timeout)
        {
            Monitor.Enter(mon);         // verificar se tem problemas
            try
            {
                if (pairList.Count > 0 && EqualityComparer<T>.Default.Equals(pairList.First.Value.t, default(T)))
                {
                    //remover 
                    pairList.First.Value.t = value;
                    pairList.First.Value.completed = true;
                    LinkedListNode<Tuple<T, U>> a = pairList.First;
                    SyncUtils.Pulse(mon, a);
                    return pairList.First.Value;
                }

                if (timeout == 0)
                    throw new TimeoutException();

                LinkedListNode<Tuple<T, U>> node = pairList.AddLast(new Tuple<T, U>());
                node.Value.t = value;

                //timer

                while (true)
                {
                    try
                    {
                        SyncUtils.Wait(mon, node, timeout /* alterar*/);
                    }
                    catch (ThreadInterruptedException e)
                    {
                        if (node.Value.completed)
                        {
                            Thread.CurrentThread.Interrupt();
                            return node.Value;
                        }
                        pairList.Remove(node);
                        throw;
                    }

                    if (node.Value.completed)
                    {
                        return node.Value;
                    }

                    //reajustar o tempo

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
                if (pairList.Count > 0 && EqualityComparer<U>.Default.Equals(pairList.First.Value.u, default(U)))
                {
                    //remover 
                    pairList.First.Value.u = value;
                    pairList.First.Value.completed = true;
                    LinkedListNode<Tuple<T, U>> a = pairList.First;
                    SyncUtils.Pulse(mon, a);
                    return pairList.First.Value;
                }

                if (timeout == 0)
                    throw new TimeoutException();

                LinkedListNode<Tuple<T, U>> node = pairList.AddLast(new Tuple<T, U>());
                node.Value.u = value;

                //timer

                while (true)
                {
                    try
                    {
                        SyncUtils.Wait(mon, node, timeout /* alterar*/);
                    }
                    catch (ThreadInterruptedException e)
                    {
                        if (node.Value.completed)
                        {
                            Thread.CurrentThread.Interrupt();
                            return node.Value;
                        }
                        pairList.Remove(node);
                        throw;
                    }

                    if (node.Value.completed)
                    {
                        return node.Value;
                    }

                    //reajustar o tempo

                }
            }
            finally
            {
                Monitor.Exit(mon);
            }
        }




        public class Tuple<T, U>{
            public T t;
            public U u;
            public bool completed;
            public Tuple()
            {
                this.t = default(T) ;
                this.u = default(U);
                completed = false;
            }
        }
    }
}
