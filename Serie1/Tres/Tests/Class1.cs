using System;
using System.Collections.Generic;
using System.Threading;
using Tres;
using Xunit;


namespace Tests
{
    public class Class1
    {
        [Fact]
        public void NoRepiteblePairs()
        {
            Pairing<String, int> pair = new Pairing<string, int>();
            List<string> t = new List<string>(10);          // "a", "aa", "aaa", "aaaa", "aaaaa", "aaaaaa", "aaaaaaa", "aaaaaaaa", "aaaaaaaaa", "aaaaaaaaaa" );
            List<int> u = new List<int>(10);
            String add = "a";
            for (int i = 0; i < 10; i++)
            {
                t.Add(add);
                u.Add(i);
                add += "a";
            }
            Thread[] allThreads = new Thread[t.Count + u.Count];
            Tres.Tuple<String, int>[] results = new Tres.Tuple<string, int>[10];
            for(int i = 0; i < t.Count; i++)
            {
                int local = i;
                allThreads[i] = new Thread(()=>
                {
                    results[local] = pair.Provide(t[local], 10000);
                });

                allThreads[i].Start();

                allThreads[i + u.Count] = new Thread(() =>
                {
                    pair.Provide(u[local], 10000);
                });

                allThreads[i + u.Count].Start();
            }

            foreach(Thread thread in allThreads)
                thread.Join();

            for(int i = 0; i < results.Length; i++)
            {
                Assert.True(t.Contains(results[i].t));
                Assert.True(u.Contains(results[i].u));
            }
        }

        [Fact]
        public void ListEmpty()
        {
            Pairing<String, int> pair = new Pairing<string, int>();
            List<string> t = new List<string>(10);          // "a", "aa", "aaa", "aaaa", "aaaaa", "aaaaaa", "aaaaaaa", "aaaaaaaa", "aaaaaaaaa", "aaaaaaaaaa" );
            List<int> u = new List<int>(10);
            String add = "a";
            for (int i = 0; i < 10; i++)
            {
                t.Add(add);
                u.Add(i);
                add += "a";
            }
            Thread[] allThreads = new Thread[t.Count + u.Count];
            Tres.Tuple<String, int>[] results = new Tres.Tuple<string, int>[10];
            for (int i = 0; i < t.Count; i++)
            {
                int local = i;
                allThreads[i] = new Thread(() =>
                {
                    results[local] = pair.Provide(t[local], 10000);
                });

                allThreads[i].Start();

                allThreads[i + u.Count] = new Thread(() =>
                {
                    pair.Provide(u[local], 10000);
                });

                allThreads[i + u.Count].Start();
            }

            foreach (Thread thread in allThreads)
                thread.Join();

            for (int i = 0; i < results.Length; i++)
            {
                Assert.True(t.Contains(results[i].t));
                Assert.True(u.Contains(results[i].u));
            }

            Boolean timeoutA = false;
            Thread a = new Thread(() =>
            {
                try
                {
                    pair.Provide("String", 100);
                }
                catch (TimeoutException e)
                {
                    timeoutA = true;
                }
            });
            a.Start();
            a.Join();
            
            Boolean timeoutB = false;
            Thread b = new Thread(() =>
            {
                try
                {
                    pair.Provide(9, 100);
                }
                catch (TimeoutException e)
                {
                    timeoutB = true;
                }
            });
            b.Start();
            b.Join();

            Assert.True(timeoutA && timeoutB);
        }

        [Fact]
        public void RemovingFromListWithTimeout()
        {
            Pairing<String, int> pair = new Pairing<string, int>();
           
            Boolean timeoutA = false;
            Thread a = new Thread(() =>
            {
                try
                {
                    pair.Provide("String", 1000);
                }
                catch (TimeoutException e)
                {
                    timeoutA = true;
                }
            });
            a.Start();
            a.Join();

            Tres.Tuple<String, int>[] results = new Tres.Tuple<string, int>[2];
            Thread b = new Thread(() =>
            {
                results[0] = pair.Provide(9, 2000);       
            });
            b.Start();

            Thread c = new Thread(() =>
            {                
                 results[1] = pair.Provide("Nove", 2000);                            
            });
            c.Start();
         
            b.Join();
            c.Join();

            Assert.True(timeoutA);
            Assert.Equal(results[0], results[1]);
            Assert.Equal("Nove", results[0].t);
            Assert.Equal(9, results[0].u);
        }
    }
}
