﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirableLazy
{
    public class ExpirableLazy<T> where T : class
    {

        public class AtomicPair
        {
            public T value;
            public long maxTimeToLive;
            public AtomicPair(T value, long maxTimeToLive)
            {
                this.value = value;
                this.maxTimeToLive = maxTimeToLive;
            }
        }

        public class AtomicPairCalculating : AtomicPair
        {
            public AtomicPairCalculating(T value, long maxTimeToLive) : base(value, maxTimeToLive) { }                
        }



        private readonly Func<T> provider;
        private TimeSpan timeToLive;
        private Object mon;
        private volatile int waiters;

        private AtomicPair atomicPair;     

        public ExpirableLazy(Func<T> provider, TimeSpan timeToLive)
        {
            this.provider = provider;
            this.timeToLive = timeToLive;
            atomicPair = new AtomicPair(null, 0);
            mon = new Object();
            waiters = 0;
        }

        public T Value
        {
            get
            {
                AtomicPair ap = null;
                while (true)
                {
                    ap = atomicPair;
                    if (ap.value != null && DateTime.Now.Ticks < ap.maxTimeToLive)
                        return ap.value;

                    if (ap.GetType() == typeof(AtomicPairCalculating) || Interlocked.CompareExchange(ref atomicPair, new AtomicPairCalculating(null, 0), ap) == ap)
                        break;
                }

                if(ap.GetType() == typeof(AtomicPairCalculating)) {              
                    Monitor.Enter(mon);
                    try
                    {
                        waiters++;
                        Interlocked.MemoryBarrier();
                        while (true)
                        {
                            ap = atomicPair;
                            if (ap.value != null && DateTime.Now.Ticks < ap.maxTimeToLive)
                                return ap.value;

                            if (ap.GetType() != typeof(AtomicPairCalculating) && Interlocked.CompareExchange(ref atomicPair, new AtomicPairCalculating(null, 0), ap) == ap)
                                break;
                            try
                            {
                                Monitor.Wait(mon);
                            }
                            catch (ThreadInterruptedException)
                            {
                                Monitor.Pulse(mon);                                             //devo usar um boolean?
                            }
                        }
                    }
                    finally
                    {
                        waiters--;
                        Monitor.Exit(mon);
                    }
                }                   

                Boolean exception = false;
                T value = null;
                try
                {
                    value = provider();
                }
                catch (Exception)       
                {
                    exception = true;
                }

                ap = new AtomicPair(value, DateTime.Now.Ticks + timeToLive.Ticks);

                Interlocked.CompareExchange(ref atomicPair, ap, atomicPair);
                Boolean inte = false;

                if (waiters > 0)
                {

                    EnterUninterruptibly(mon, out inte);                                
                    try
                    {
                        if (inte)
                            Thread.CurrentThread.Interrupt();

                        if (!exception)
                        {
                            Monitor.PulseAll(mon);
                            return ap.value;                
                        }
                        Monitor.Pulse(mon);
                        throw new InvalidOperationException();
                    }
                    finally
                    {
                        Monitor.Exit(mon);
                    }
                }
                if(exception)
                    throw new InvalidOperationException();
                return ap.value;
            }
        }

        public void EnterUninterruptibly(object mon, out bool wasInterrupted)
        {
            wasInterrupted = false;
            while (true)
            {
                try
                {
                    Monitor.Enter(mon);
                    return;
                }
                catch (ThreadInterruptedException e)
                {
                    wasInterrupted = true;
                }
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
