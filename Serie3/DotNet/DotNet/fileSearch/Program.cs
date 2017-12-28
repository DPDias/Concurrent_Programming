using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace fileSearch
{
    class Program
    {
        static void Main(string[] args){
            Console.WriteLine("Escreva o caminho da pasta: ");
            String dirPath = Console.ReadLine();
            Console.WriteLine("Diga o numero de ficheiros a apresentar: ");
            int maxFiles = int.Parse(Console.ReadLine());
            CancellationTokenSource cts = new CancellationTokenSource();
            var bg = new BiggerFiles(dirPath, maxFiles, cts.Token);
            Task b = bg.Start();

            Console.WriteLine("Deseja cancelar (s/n)?");
            String answer = Console.ReadLine();
            if (answer.Equals("s"))
                cts.Cancel();          
          

            b.Wait();
            Console.WriteLine(bg.GetNumberOfFiles());
            /*
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            stopWatch.Stop();
            long duration = stopWatch.ElapsedMilliseconds;*/
            int a = 0;
        }
    }
}
