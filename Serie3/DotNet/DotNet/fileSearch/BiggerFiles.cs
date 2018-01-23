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
        private List<FileInfo> maxFiles;
        private volatile int numberOfFiles;
        private Object mon;
        private Comparer<FileInfo> com;
        private int NumberMaxFiles;
        private CancellationToken ct;
        private Boolean ArrayChanged = false;

        public BiggerFiles(String dirPath, int maxFiles, CancellationToken ct) {
            this.dirPath = dirPath;
            this.maxFiles = new List<FileInfo> (maxFiles);
            NumberMaxFiles = maxFiles;
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

            ParallelOptions options = new ParallelOptions { CancellationToken = ct };

            DirectoryInfo di = await dir.ConfigureAwait(false);
            FileInfo[] allFiles = await Task.Run( () => di.GetFiles());    
            DirectoryInfo[] allDirs = await Task.Run( () => di.GetDirectories());

            Interlocked.Add(ref numberOfFiles, allFiles.Length);

            Task[] allTasksDir = new Task[allDirs.Length];
            int k = 0;
            foreach (DirectoryInfo aux in allDirs) {
                allTasksDir[k++] = SearchFiles(Task.Run(() => aux));
            }       

            Parallel.ForEach(allFiles, options, () => new List<FileInfo>(NumberMaxFiles), (i, loopState, partial) =>
            {
                if (ct.IsCancellationRequested)
                {
                    loopState.Stop();
                }
                if (partial.Count < NumberMaxFiles)
                    partial.Add(i);
                else
                    if (i.Length > partial[0].Length)
                        partial[0] = i;            
                return partial.OrderBy(a => a.Length).ToList();
            },
            partial => {
                
                
                lock (mon)
                {
                    if (maxFiles.Count == 0 || maxFiles.Last().Length < partial.Last().Length)
                        ArrayChanged = true;
                    else return;     
                  
                    var newArray = partial.Concat(maxFiles).OrderByDescending(a => a.Length).ToList();
                    if (newArray.Count > NumberMaxFiles)
                        maxFiles = newArray.GetRange(0, NumberMaxFiles);
                    else maxFiles = newArray;
                }
            });

            await Task.WhenAll(allTasksDir);  
        }

        public int GetNumberOfFiles() {
            return numberOfFiles;
        }

        public List<FileInfo> getBiggerFiles() {
            List<FileInfo> ret = new List<FileInfo>();
            lock (mon)
            {
                if (ArrayChanged)
                {
                    ArrayChanged = false;
                    maxFiles.ForEach(a => ret.Add(a));
                }
                else return null;
            }
            return ret;
        }
    }
}
