using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace TesteDeSincronizadores
{
    [TestClass]
    public class UnitTest1
    {
        public class Contentor
        {
            public readonly int v;
            public Contentor(int i)
            {
                this.v = i;
            }
        }

        [TestMethod]
        public void AllReadTheSameValue()
        {
            int[] saveResults = new int[100];
            Thread[] saveThreads = new Thread[100];
            
            ExpirableLazy<Contentor> el = new ExpirableLazy<Contentor>(()=>new Contentor(1), new TimeSpan(0, 0, 50));

            for (int i = 0; i < saveThreads.Length ; i++)
            {
                int local = i;
                saveThreads[i] = new Thread( () => {                 
                    saveResults[local] = 1 ; 
                    int a  = el.Value.v; } );
            }

            foreach (Thread i in saveThreads)
                i.Start();

            foreach (Thread i in saveThreads) 
                i.Join();

            foreach (int i in saveResults)
                Assert.IsTrue(i == 1);
        }
    }
}
