using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace fileSearch {
    public class BiggerFiles {
        private String dirPath;
        private FileInfo [] maxFiles;
        private volatile int numberOfFiles;
        private Object mon;
        private Comparer<FileInfo> com;
        private int idx = 0;
        private CancellationToken ct;

        public BiggerFiles(String dirPath, int maxFiles, CancellationToken ct) {
            this.dirPath = dirPath;
            this.maxFiles = new FileInfo [maxFiles];
            com = Comparer<FileInfo>.Create((b, a) => (int)(a.Length - b.Length));
            numberOfFiles = 0;
            mon = new Object();
            this.ct = ct;
        }

        public Task Start() {
            return SearchFiles(Task.Run(() => new DirectoryInfo(dirPath)));
        }

        private async Task SearchFiles(Task<DirectoryInfo> dir) {
            if (ct.IsCancellationRequested)
                return;
            DirectoryInfo di = await dir;
            FileInfo[] allFiles = await Task.Run( () => di.GetFiles());    
            DirectoryInfo[] allDirs = await Task.Run( () => di.GetDirectories());

            Interlocked.Add(ref numberOfFiles, allFiles.Length);
     
            Parallel.For(0, allFiles.Length, (i) => {
                if (ct.IsCancellationRequested)
                    return;
                if (idx < maxFiles.Length) {
                    lock (mon) {
                        if (idx < maxFiles.Length) {
                            maxFiles[idx++] = allFiles[i];
                            if (idx == maxFiles.Length)
                                BuildMaxHeap(maxFiles, idx, com);
                        } else {
                            if (maxFiles[0].Length < allFiles[i].Length) {
                                maxFiles[0] = allFiles[i];
                            }
                        }
                    }
                } else {
                    lock (mon) {
                        if (maxFiles[0].Length < allFiles[i].Length) {
                            maxFiles[0] = allFiles[i];
                        }
                        maxHeapify(maxFiles, idx, com, 0);
                    }
                }
                
            });


            foreach (DirectoryInfo aux in allDirs)
                await SearchFiles(Task.Run(() => aux));

        }

        public int GetNumberOfFiles() {
            return numberOfFiles;
        }

        public FileInfo [] getBiggerFiles() {
            return maxFiles;
        }

        public static void BuildMaxHeap<T>(T[] a, int size, Comparer<T> cmp) {
            for (int i = parent(size - 1); i >= 0; --i)
                maxHeapify(a, size, cmp, i);
        }


        public static void maxHeapify<T>(T[] heap, int heapSize, Comparer<T> c, int i) {
            int l = left(i);
            int r = right(i);
            int largest = (l >= heapSize || c.Compare(heap[l], heap[i]) <= 0) ? i : l;
            if (r < heapSize && c.Compare(heap[largest], heap[r]) < 0) largest = r;
            if (largest != i) {
                swap(heap, i, largest);
                maxHeapify(heap, heapSize, c, largest);
                
            }
        }

        public static int left(int i) {
            return (i << 1) + 1;
        }

        public static int right(int i) {
            return (i << 1) + 2;
        }

        public static int parent(int i) {
            return (i - 1) >> 1;
        }

        public static void swap<T>(T[] a, int i1, int i2) {
            T aux = a[i1];
            a[i1] = a[i2];
            a[i2] = aux;
        }


    }
}
