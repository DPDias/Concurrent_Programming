using System;
using System.Threading;

namespace Um
{
    public class ExpirableLazy<T> where T : class
    {
        private long maxTickCount;
        private Func<T> provider;
        private TimeSpan timeToLive;
        private Object mon;
        private T value;
        private bool calculating;
        private bool goToProvider;
        public T Value
        {
            get
            {
                Monitor.Enter(mon);
                try
                {
                    while (true)
                    {
                        if (value != null && DateTime.Now.Ticks <= maxTickCount)
                            return value;

                        //se value se encontra null ou o tempo acabou, chamar o provider e aumentar o tempo de vida da variável
                        if (calculating)
                        {
                            try
                            {
                                Monitor.Wait(mon);
                            }
                            catch (ThreadInterruptedException)
                            {
                                Monitor.Pulse(mon);
                                throw;
                            }

                        }
                        else
                        {
                            calculating = true;
                            break;
                        }
                    }

                }
                finally { Monitor.Exit(mon); }

                T aux = null;
                Boolean exception = false;

                try
                {
                    aux = provider();
                }
                catch (Exception)
                {
                    exception = true;
                }

                Boolean interrupt = false;
                EnterUninterruptibly(mon, out interrupt);
                try
                {
                  
                    calculating = false;
                    if (exception)
                    {
                        Monitor.Pulse(mon);
                        throw new InvalidOperationException();
                    }
                    if (interrupt)
                        Thread.CurrentThread.Interrupt();
                    Monitor.PulseAll(mon);
                    value = aux;
                    maxTickCount = DateTime.Now.Ticks + timeToLive.Ticks;
                    return value;
                }
                finally { Monitor.Exit(mon); }
            }
        } // throws InvalidOperationException, ThreadInterruptedException

        public ExpirableLazy(Func<T> provider, TimeSpan timeToLive)
        {
            //máximo tempo de vida
            maxTickCount = DateTime.Now.Ticks + timeToLive.Ticks;

            this.provider = provider;
            this.timeToLive = timeToLive;

            //monitor
            mon = new Object();

            value = null;
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
}

