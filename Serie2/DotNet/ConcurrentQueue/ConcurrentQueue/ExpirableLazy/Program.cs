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
                        Thread.MemoryBarrier();
                        while (true)
                        {
                            ap = atomicPair;
                            if (ap.value != null && DateTime.Now.Ticks < ap.maxTimeToLive)
                                return ap.value;

                            if (ap.GetType() != typeof(AtomicPairCalculating) && Interlocked.CompareExchange(ref atomicPair, new AtomicPairCalculating(null, 0), ap) == ap)
                                break;

                            Thread.MemoryBarrier();   //acho que posso retirar
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

                Boolean exception = false;
                T value = null;
                try
                {
                    value = provider();
                }
                catch (Exception)       //se deu exceção o values está a null mesmo com finnaly block
                {
                    exception = true;
                }

                ap = new AtomicPair(value, DateTime.Now.Ticks + timeToLive.Ticks);

                Interlocked.CompareExchange(ref atomicPair, ap, atomicPair);

                if (waiters > 0)
                {
                    Monitor.Enter(mon); //interruptible
                                        //podem entrar instruções para dentro da barreira
                    try
                    {
                        if (!exception)
                        {
                            Monitor.PulseAll(mon);
                            return ap.value;                //poderá o tempo ter acabado??
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
    }

    public class Program
    {

        public static void Main(String[] args)
        {

        }
    }
}
