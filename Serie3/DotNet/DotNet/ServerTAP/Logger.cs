/*
 * INSTITUTO SUPERIOR DE ENGENHARIA DE LISBOA
 * Licenciatura em Engenharia Informática e de Computadores
 *
 * Programação Concorrente - Inverno de 2009-2010, Inverno de 1017-2018
 * João Trindade
 *
 * Código base para a 3ª Série de Exercícios.
 *
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Servidor
{
	// Logger single-threaded.
	public class Logger
	{
		private readonly TextWriter Writer;
		private DateTime StartTime;
		private int NumberOfRequests;
        private volatile bool stop = false;
        private volatile int waiting = 0;
        private volatile Object mon;
        private ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private Thread LoggerThread = null;
        private int Timeout = 1000;

		public Logger() : this(Console.Out) {}
		public Logger(string logfile) : this(new StreamWriter(new FileStream(logfile, FileMode.Append, FileAccess.Write))) {}
		public Logger(TextWriter tw)
		{
		    NumberOfRequests = 0;
		    Writer = tw;
		}

        private void loggerWork() {
            string loggerMessage = "";
            int observedCount;
            while (true) {
                loggerMessage = "";
                if (queue.TryDequeue(out loggerMessage)) {
                    Writer.WriteLine(String.Format("{0}: {1}", DateTime.Now, loggerMessage));
                } else if (stop)
                    break;
                observedCount = queue.Count;
                if (observedCount == 0) {
                    lock (mon) {
                        if (observedCount == queue.Count) {
                            waiting = 1;
                            while (true) {
                                if (!stop) {
                                    Monitor.Wait(mon);
                                    if (waiting == 0)
                                        break;
                                } else
                                    return;
                            }
                        }
                    }
                }
            }
        }

	    public void Start()
		{
            Writer.WriteLine();
            Writer.WriteLine(String.Format("::- LOG STARTED @ {0} -::", DateTime.Now));
            Writer.WriteLine();

            mon = new Object();
            StartTime = DateTime.Now;

            LoggerThread = new Thread(loggerWork);
            LoggerThread.Priority = ThreadPriority.Lowest;
            LoggerThread.Start();
		}

		public void LogMessage(string msg)
		{
            if (!stop)
            {                
                queue.Enqueue("["+Thread.CurrentThread.ManagedThreadId+"]"+msg);
                while (true)
                {
                    int observedWaiting = waiting;
                    if (observedWaiting == 1)
                    {                        
                        if (Interlocked.CompareExchange(ref waiting, 0, observedWaiting) == observedWaiting)
                        {
                            lock (mon)
                            {                                
                                Monitor.PulseAll(mon);
                                return;
                            }
                        }
                    }
                    else if (observedWaiting == waiting)
                        return;
                }
            }
		}

		public void IncrementRequests()
		{
			++NumberOfRequests;
		}

		public void Stop()
		{            
			long elapsed = DateTime.Now.Ticks - StartTime.Ticks;
			Writer.WriteLine();
			LogMessage(String.Format("Running for {0} second(s)", elapsed / 10000000L));

            stop = true;
            LoggerThread.Join();
           
            Writer.WriteLine();
			Writer.WriteLine(String.Format("::- LOG STOPPED @ {0} -::", DateTime.Now));
			Writer.Close();
		}
	}
}
