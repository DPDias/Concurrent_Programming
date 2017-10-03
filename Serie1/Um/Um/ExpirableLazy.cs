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
    public T Value { 
        get{
            lock (mon){
                if (value != null && Environment.TickCount <= maxTickCount) 
                    return value;    
                //se ela se encontra null ou o tempo acabou, chamar o provider e aumentar o tempo de vida da variável
                value = provider();
                maxTickCount = Environment.TickCount + timeToLive.Ticks;
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


/*

public T Value
{
    get
    {
        lock (mon)
        {
            if (valueAux != null) // e tempo
                return valueAux;
            while (calculating)
                Monitor.Wait(mon);
            if

            calculating = true;
        }
        valueAux = provider();
        calculating = false;
        Monitor.Pulse(mon);
        return valueAux;
    }
}

*/

