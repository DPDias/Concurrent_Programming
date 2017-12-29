/*
 * INSTITUTO SUPERIOR DE ENGENHARIA DE LISBOA
 * Licenciatura em Engenharia Informática e de Computadores
 *
 * Programação Concorrente - Inverno de 2009-2010, Inverno de 1017-2018
 * Paulo Pereira, Pedro Félix
 *
 * Código base para a 3ª Série de Exercícios.
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tracker
{
    /// <summary>
    /// Handles client requests.
    /// </summary>
    public sealed class Handler
    {
        /// <summary>
        /// Data structure that supports message processing dispatch.
        /// </summary>
        private static readonly Dictionary<string, Action<string[], StreamWriter, Logger>> MESSAGE_HANDLERS;

        static Handler()
        {
            MESSAGE_HANDLERS = new Dictionary<string, Action<string[], StreamWriter, Logger>>();
            MESSAGE_HANDLERS["SET"] = ProcessSetMessage;
            MESSAGE_HANDLERS["GET"] = ProcessGetMessage;
            MESSAGE_HANDLERS["KEYS"] = ProcessKeysMessage;
            MESSAGE_HANDLERS["SHUTDOWN"] = Listener.Shutdown;
            MESSAGE_HANDLERS["BGET"] = ProcessBGetMessage;
        }

        /// <summary>
        /// Handles SET messages.
        /// </summary>
        private static void ProcessSetMessage(string[] cmd, StreamWriter wr, Logger log)
        {
            if (cmd.Length - 1 != 2)
            {
                wr.WriteAsync(String.Format("(error) wrong number of arguments (given {0}, expected 2)\n", cmd.Length - 1));
            }
            string key = cmd[1];
            string value = cmd[2];
            Store.Instance.Set(key, value);
            wr.WriteAsync(String.Format("OK\n"));
        }

        private static async void ProcessBGetMessage(string [] cmd, StreamWriter wr, Logger log) {
            if (cmd.Length - 1 != 2) {
                wr.WriteLineAsync(String.Format("(error) wrong number of arguments (given {0}, expected 1)\n", cmd.Length - 1));
            }
            string value = Store.Instance.Get(cmd[1]);
            if(value == null) {
                await Task.Delay(int.Parse(cmd[2]));
                value = Store.Instance.Get(cmd[1]);
                if (value == null) {
                    wr.WriteLine("(nil)\n");
                    return;
                }
            }
            wr.WriteLineAsync(String.Format("\"{0}\"\n", value));
        }

        /// <summary>
        /// Handles GET messages.
        /// </summary>
        private static void ProcessGetMessage(string[] cmd, StreamWriter wr, Logger log)
        {
            if(cmd.Length - 1 != 1)
            {
                wr.WriteLineAsync(String.Format("(error) wrong number of arguments (given {0}, expected 1)\n", cmd.Length-1));
            }
            string value = Store.Instance.Get(cmd[1]);            
            if(value != null)
            {
                wr.WriteLineAsync(String.Format("\"{0}\"\n", value));
            }
            else
            {
               wr.WriteLine("(nil)\n");
            }
        }

        /// <summary>
        /// Handles KEYS messages.
        /// </summary>
        private static void ProcessKeysMessage(string[] cmd, StreamWriter wr, Logger log)
        {            
            if (cmd.Length -1 != 0)
            {
                wr.WriteAsync(String.Format("(error) wrong number of arguments (given {0}, expected 0)\n", cmd.Length - 1));
            }
            int ix = 1;
            foreach(string key in Store.Instance.Keys())
            {
                wr.WriteAsync(String.Format("{0}) \"{1}\"", ix++, key));
            }
            wr.WriteAsync("");   
        }
                
        /// <summary>
        /// The handler's input (from the TCP connection)
        /// </summary>
        private readonly StreamReader input;

        /// <summary>
        /// The handler's output (to the TCP connection)
        /// </summary>
        private readonly StreamWriter output;

        /// <summary>
        /// The Logger instance to be used.
        /// </summary>
        private readonly Logger log;

        /// <summary>
        ///	Initiates an instance with the given parameters.
        /// </summary>
        /// <param name="connection">The TCP connection to be used.</param>
        /// <param name="log">the Logger instance to be used.</param>
        public Handler(Stream connection, Logger log)
        {
            this.log = log;
            output = new StreamWriter(connection);
            input = new StreamReader(connection);
        }

        /// <summary>
        /// Performs request servicing.
        /// </summary>
        public async Task Run()
        {
            try
            {
                char[] requestBuffer = new char[1024];

                // Receive the request (we know that it is smaller than 1024 bytes);
                int bytesRead = await input.ReadAsync(requestBuffer, 0, requestBuffer.Length);
                

                byte[] requestBuffer1 = Encoding.ASCII.GetBytes(new string(requestBuffer));

                string request = Encoding.ASCII.GetString(requestBuffer1, 0, bytesRead);

                string[] cmd = request.Trim().Split(' ');
                Action<string[], StreamWriter, Logger> handler = null;
                if (cmd.Length < 1 || !MESSAGE_HANDLERS.TryGetValue(cmd[0], out handler))
                {
                    log.LogMessage("(error) unnown message type");
                    return;
                }
                // Dispatch request processing
                handler(cmd, output, log);
                await output.FlushAsync();
            }
            catch (IOException ioe)
            {
                // Connection closed by the client. Log it!
                log.LogMessage(String.Format("Handler - Connection closed by client {0}", ioe));
            }
            finally
            {
                input.Close();
                output.Close();
            }
        }
    }

    /// <summary>
    /// This class instances are file tracking servers. They are responsible for accepting 
    /// and managing established TCP connections.
    /// </summary>
    public sealed class Listener
    {
        /// <summary>
        /// TCP port number in use.
        /// </summary>
        private readonly int portNumber;

        // The maximum number of simultaneous connections allowed.
        private const int MAX_SIMULTANEOUS_CONNECTIONS = 10;

        private static int activeConnections;

        // variable to control de shutdown
        //when a thread executes shutdown command, will put this variable to 1, not letting others threads do shutdown
        private static volatile int shutdownDone = 0;

        // CancelationToken to command shutdown
        private static CancellationTokenSource cts;

        //Event used to block thread with SHUTDOWN connection
        private static ManualResetEvent shutdownEvent = new ManualResetEvent(false);

        private static TcpListener server = null;

        /// <summary> Initiates a tracking server instance.</summary>
        /// <param name="_portNumber"> The TCP port number to be used.</param>
        public Listener(int _portNumber, CancellationTokenSource _cts) { portNumber = _portNumber; cts = _cts; }

        /// <summary>
        ///	Server's main loop implementation.
        /// </summary>
        /// <param name="log"> The Logger instance to be used.</param>
        public async Task Run(Logger log, CancellationToken ctk)
        {
            
            // Create a listen socket bind to the server port.
            server = new TcpListener(IPAddress.Loopback, portNumber);

            // Start listen from server socket
            server.Start();

            try
            {
                var startedTasks = new HashSet<Task>();
                do
                {
                    try
                    {
                        var connection = await server.AcceptTcpClientAsync();

                        /**
                        * Start the processing the previously accepted connection.
                        */

                        connection.LingerState = new LingerOption(true, 10);
                        log.LogMessage(String.Format("Listener - Connection established with {0}.",
                            connection.Client.RemoteEndPoint));

                        Handler protocolHandler = new Handler(connection.GetStream(), log);
                        // Synchronously process requests made through de current TCP connection


                        Interlocked.Increment(ref activeConnections);

                        //
                        // Add the listen thread returned by the protocolHandler.Run method
                        // to the thread hast set.
                        //

                        startedTasks.Add(protocolHandler.Run().ContinueWith( _=>
                        {
                            int c2 = Interlocked.Decrement(ref activeConnections);
                            if (c2 == 1 && ctk.IsCancellationRequested)
                                shutdownEvent.Set();
                            if (c2 == 0 && ctk.IsCancellationRequested)
                                server.Stop();
                        }));

                        //
                        // If the threshold was reached, wait until one of the active
                        // worker threads complete its processing.
                        //

                        if (startedTasks.Count >= MAX_SIMULTANEOUS_CONNECTIONS)
                            startedTasks.Remove(await Task.WhenAny(startedTasks));
                    }
                    catch (ObjectDisposedException)
                    {
                        // benign exception
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("***error: {0}", ex.Message);
                    }
                } while (!ctk.IsCancellationRequested);


                /**
                 * before return, wait for completion of processing of all the accepted requests.
                 */
                server.Stop();
                await Task.WhenAll(startedTasks);
            }
            finally
            {
                log.LogMessage("Listener - Ending.");
                      //onde faço stop?
            }
        }


        /**
         * Method to attend shutdown requests in Handler
         */
        public static void Shutdown(String[] s, StreamWriter wr, Logger log)
        {

            //Interlocked to only one thread shutdown the server
            if (Interlocked.CompareExchange(ref shutdownDone, 1, 0) == 0)
            {
                //shutdown started
                cts.Cancel();
                wr.WriteLine("Shutdown Done it");

                //i am the last connection, then don't need to block
                if (activeConnections == 1)
                    return;
         
                //wait for all commands to finish
                shutdownEvent.WaitOne();
            }
            else
            {
                //if another shutdown command comes, will not be blocked and send a message
                wr.WriteLine("Shutdown already in progress");
            }
        }


    }

    class Program
    {
        
        /// <summary>
        ///	Application's starting point. Starts a tracking server that listens at the TCP port 
        ///	specified as a command line argument.
        /// </summary>
        public static void Main(string[] args)
        {
			String execName = AppDomain.CurrentDomain.FriendlyName.Split('.')[0];
            // Checking command line arguments
            if (args.Length > 1)
            {
                Console.WriteLine("Usage: {0} [<TCPPortNumber>]", execName);
                Environment.Exit(1);
            }

            ushort port = 8080;
			if (args.Length == 1) {
            	if (!ushort.TryParse(args[0], out port))
            	{
                	Console.WriteLine("Usage: {0} [<TCPPortNumber>]", execName);
                	return;
            	}
			}
			Console.WriteLine("--server starts listen on port {0}", port);

            // The cancellation token source used to shutdonw the server.
            CancellationTokenSource cts = new CancellationTokenSource();

            // Start servicing
            Logger log = new Logger();
            log.Start();
            try
            {
                new Listener(port, cts).Run(log, cts.Token).Wait();
            }
            finally
            {
                log.Stop();
            }
            Console.ReadKey();
        }
    }
}
