using System;
using System.Diagnostics;
using MessagePassing;

namespace Cerberus {

    class Program {
        static public void Main(String[] args) {
            string serverUrl = "http://localhost:5000/";
            string inputDirectory = "/home/smayank/Desktop/MS/trace-interpol-3-oct/corral-traceInlining/mytests/cerberus/";
            string inputFile =  inputDirectory + args[1];
            int numClients = 3;

            if (args[0] == "-master") {
                Console.WriteLine("Starting Master");
                HttpMaster master = new HttpMaster(new List<string>{serverUrl});
                Thread masterThread = new Thread(master.Start);
                masterThread.IsBackground = true;
                masterThread.Start();
                Thread.Sleep(1000);
                
                Console.WriteLine("starting Clients");

                String treeDirctory = Path.Join(inputDirectory, ".cerberus", args[1], "trees");
                string[] treeFiles = Directory.GetFiles(treeDirctory, "*.tree", SearchOption.TopDirectoryOnly);

                numClients = Math.Min(numClients, treeFiles.Length);

                for (var i=0; i<numClients; i++) {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "Cerberus",
                            Arguments = "-slave " + args[1] + " " + treeFiles[i],
                            UseShellExecute = false, RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                }

                masterThread.Join();
            }
            else {
                Console.WriteLine("Starting Slave");
                CorralDriver driver = new CorralDriver(serverUrl, inputFile, args[2]);
                driver.Start();
            }
            
        }
    }

    class CorralDriver {
        private string serverUrl;
        private string inputFile;
        private string bootStrapFile;

        public CorralDriver(string serverUrl, string inputFile, string bootStrapFile="") {
            this.serverUrl = serverUrl;
            this.inputFile = inputFile;
            this.bootStrapFile = bootStrapFile;
        }
        
        public void Start() {
            Console.WriteLine("Driver Starting...");
            Communicator connector = new HttpCommunicator(serverUrl);
            int clientId = connector.Register();

            try
            {
                string logFileName = inputFile + "." + clientId + ".smt2";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "corral",
                        Arguments = "/clientId:" + clientId + " /traceInlining /proverLog:" + logFileName + " /recursionBound:3 /trackAllVars /si /bootstrapFile:" + bootStrapFile + " " + inputFile,
                        UseShellExecute = false, RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
 
                process.Start();
                using (StreamWriter writer = new StreamWriter(inputFile + "." + clientId + ".txt"))  
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        writer.WriteLine("client {0}: {1}", clientId, line);
                    }
                }
 
                process.WaitForExit();
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                connector.ReportCrash(e.ToString());
            }
            finally {
                connector.Unregister();
            }
        }
    }
}