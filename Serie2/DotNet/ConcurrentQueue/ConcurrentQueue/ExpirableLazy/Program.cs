using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirableLazy
{
    public class ExpirableLazy<T> where T : class
    {

        private readonly Func<T> provider;
        private TimeSpan timeToLive;
        private long maxTimeToLive;
        private T value;
        private Object mon;
        private volatile int calculating;
        private volatile int waiters;

        public ExpirableLazy(Func<T> provider, TimeSpan timeToLive)
        {
            this.provider = provider;
            this.timeToLive = timeToLive;
            maxTimeToLive = 0;
            value = null;
            mon = new Object();
            calculating = 0;
            waiters = 0;
        }

        public T Value
        {
            get
            {
                if (value != null && DateTime.Now.Ticks < maxTimeToLive)
                    return value;
                Thread.MemoryBarrier();
                if(Interlocked.CompareExchange(ref calculating, 1, 0) != 0)
                {
                    Monitor.Enter(mon);
                    try
                    {
                        waiters++;
                        Thread.MemoryBarrier();
                        while (true)
                        {
                            if (value != null && DateTime.Now.Ticks < maxTimeToLive)
                                return value;

                            if (Interlocked.CompareExchange(ref calculating, 1, 0) != 0)
                                break;
                            Thread.MemoryBarrier();
                            try
                            {
                                Monitor.Wait(mon);
                            }
                            catch (ThreadInterruptedException)
                            {
                                Monitor.Pulse(mon); //devo usar um boolean?
                            }
                        }
                    }
                    finally
                    {
                        waiters--;
                        Monitor.Exit(mon);
                    }
                }
                if (value != null && DateTime.Now.Ticks < maxTimeToLive) {
                    Interlocked.CompareExchange(ref calculating, 0, 1);
                    return value;
                }

                Thread.MemoryBarrier();             //será que podia tirar?

                Boolean exception = false;
                try
                {
                    value = provider();
                }
                catch (Exception)
                {
                    exception = true;
                }
                if (!exception)
                {
                    maxTimeToLive = DateTime.Now.Ticks + timeToLive.Ticks;
                    Interlocked.CompareExchange(ref calculating, 0, 1);
                    Thread.MemoryBarrier();
                    if (waiters > 0)
                    {
                        Monitor.Enter(mon); //interruptible
                        try
                        {
                            Monitor.PulseAll(mon);
                        }
                        finally
                        {
                            Monitor.Exit(mon);
                        }
                    }
                    return value;
                }
                else
                {
                    Interlocked.CompareExchange(ref calculating, 0, 1);
                    Thread.MemoryBarrier();
                    if (waiters > 0)
                    {
                        Monitor.Enter(mon); //interruptible
                        try
                        {
                            Monitor.Pulse(mon);
                        }
                        finally
                        {
                            Monitor.Exit(mon);
                        }
                    }
                }
                throw new InvalidOperationException();
            }
        }
    }

    public class Program
    {

        public static void Main(String[] args)
        {

        }
    }
}
