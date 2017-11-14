using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ExpirableLazy;
using System.Threading;

namespace Tests
{
    public class ExpirableLazyTest
    {
        public class Provider
        {
            public int value;
            public Provider(int value)
            {
                this.value = value;
            }
        }

        [Fact]
        public void AllReadSameValue()
        {
            int numberOfThreads = 1000;
            Provider theSame = new Provider(888);
            ExpirableLazy<Provider> el = new ExpirableLazy<Provider>(() =>
            {
                theSame.value++;
                return theSame;
            }, new TimeSpan(0, 0, 10));
            Thread[] allThreads = new Thread[numberOfThreads];
            Provider[] allResults = new Provider[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                int local = i;
                allThreads[i] = new Thread(() =>
                {
                    allResults[local] = el.Value;
                });
                allThreads[i].Start();
            }

            foreach (Thread thread in allThreads)
                thread.Join();

            foreach (Provider result in allResults)
            {
                Assert.Equal(theSame, result);
                Assert.Equal(theSame.value, result.value);
            }
        }

    

        [Fact]
        public void ProviderAlwaysException()
        {
            int numberOfThreads = 1000;
            int value = 888;
            Provider theSame = new Provider(value);
            ExpirableLazy<Provider> el = new ExpirableLazy<Provider>(
                                                                    () => {
                                                                        throw new Exception();
                                                                    }, new TimeSpan(0, 1, 0));
            Thread[] allThreads = new Thread[numberOfThreads];
            bool[] allResults = new bool[numberOfThreads];

            for (int i = 0; i < numberOfThreads; i++)
            {
                int local = i;
                allThreads[i] = new Thread(() =>
                {
                    try
                    {
                        Provider aux = el.Value;
                    }
                    catch (Exception)
                    {
                        allResults[local] = true;
                    }
                });
                allThreads[i].Start();
            }

            foreach (Thread thread in allThreads)
                thread.Join();

            foreach (bool result in allResults)
            {
                Assert.True(result);
            }
        }

        [Fact]
        public void ProviderOneTimeException()
        {
            int value = 0;
            Provider theSame = new Provider(1);
            ExpirableLazy<Provider> el = new ExpirableLazy<Provider>(
                                                                    () => {
                                                                        if (value++ == 0)
                                                                        {
                                                                            Thread.Sleep(5000);
                                                                            throw new Exception();
                                                                        }
                                                                        return theSame;
                                                                    }, new TimeSpan(0, 1, 0));
            bool exception = false;
            Thread a = new Thread(() =>
            {
                try
                {
                    Provider ret = el.Value;
                }
                catch (Exception)
                {
                    exception = true;
                }
            });
            a.Start();

            Thread b = new Thread(() =>
            {
                try
                {
                    Provider ret = el.Value;
                }
                catch (Exception)
                {
                    exception = true;
                }
            });
            b.Start();
            b.Interrupt();

            Provider[] aux = new Provider[1];
            Thread c = new Thread(() =>
            {
                aux[0] = el.Value;
            });
            c.Start();

            a.Join();
            b.Join();
            c.Join();

            Assert.True(exception);
            Assert.Equal(theSame, aux[0]);
        }
    }
}
