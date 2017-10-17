using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

public class ExpirableLazy<T> where T : class {
    private long maxTickCount;
    private Func<T> provider;
    private TimeSpan timeToLive;
    private Object mon;
    private T value;
    private bool calculating;
    private bool goToProvider;
    public T Value { 
        get{
            lock(mon)
            {
                while (true)
                {
                    if (value != null && Environment.TickCount <= maxTickCount)
                        return value;
                    //se ela se encontra null ou o tempo acabou, chamar o provider e aumentar o tempo de vida da variável
                    if (calculating)
                        Monitor.Wait(mon);
                    else
                    {
                        calculating = true;
                        break;
                    }
                    if (goToProvider)
                        break;
                }

            }
            T aux = null;
            try
            {
                aux = provider();
            }
            catch (Exception e)
            {
                lock (mon)
                {
                    Monitor.Pulse(mon);     // verificar se tenho de por isto dentro de exclusão
                    goToProvider = true;
                }
            }
            lock (mon)
            {
                Monitor.PulseAll(mon);
                value = aux;
                maxTickCount = Environment.TickCount + timeToLive.Ticks;
                calculating = false;
                goToProvider = false;
                return value;
            }
        }
    } // throws InvalidOperationException, ThreadInterruptedException

    public ExpirableLazy(Func<T> provider, TimeSpan timeToLive)
    {
        //máximo tempo de vida
        maxTickCount =  Environment.TickCount + timeToLive.Ticks;   

        this.provider = provider;
        this.timeToLive = timeToLive;

        //monitor
        mon = new Object();       
                                  
        value = null;   //devo iniciar a variavel?
    }      
}


/*     lock (mon){
                if (value != null && Environment.TickCount <= maxTickCount) 
                    return value;    
                //se ela se encontra null ou o tempo acabou, chamar o provider e aumentar o tempo de vida da variável
                value = provider();
                maxTickCount = Environment.TickCount + timeToLive.Ticks;
                return value;
            }
*/

