using System;
using System.Diagnostics;
using MessagePassing;
using System.Text.RegularExpressions;

namespace Cerberus {

    class Program {
        static public void Main(String[] args) {
            string serverUrl = "http://localhost:5000/";
            string inputDirectory = Path.GetDirectoryName(args[1]);
            string inputFile = Path.GetFileName(args[1]);
            int numClients = 5;

            if (args[0] == "-server") {
                Console.WriteLine("Starting Server");
                HttpMaster master = new HttpMaster(new List<string>{serverUrl});
                Thread masterThread = new Thread(master.Start);
                masterThread.IsBackground = true;
                masterThread.Start();
                Thread.Sleep(1000);
                
                Console.WriteLine("Starting Clients...");

                String treeDirectory = Path.Join(inputDirectory, ".cerberus", inputFile, "trees");
                string[] treeFiles = {};
                if (Directory.Exists(treeDirectory)) {
                    treeFiles = Directory.GetFiles(treeDirectory, "*.tree", SearchOption.TopDirectoryOnly);
                }
                else {
                    Directory.CreateDirectory(treeDirectory);
                }

                numClients = Math.Min(numClients, treeFiles.Length) + 1;

                for (var i=0; i<numClients; i++) {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "Cerberus",
                            Arguments = i==numClients-1 ? "-slave " + args[1] : "-slave " + args[1] + " " + treeFiles[i],
                            UseShellExecute = false, RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                }

                masterThread.Join();
            }
            else {
                CorralDriver driver;
                if (args.Length == 3)
                    driver = new CorralDriver(serverUrl, args[1], args[2]);
                else
                    driver = new CorralDriver(serverUrl, args[1]);
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
            Console.WriteLine("Client Driver Starting...");
            Communicator connector = new HttpCommunicator(serverUrl);
            int clientId = connector.Register();
            Console.WriteLine("Client Started: client Id: {0}", clientId);

            try
            {
                string inputDirectory = Path.GetDirectoryName(inputFile);
                string fileName = Path.GetFileName(inputFile);
                string logFilePath = Path.Join(inputDirectory, ".cerberus", fileName, "Slave" + clientId + ".smt2");
                string outputFilePath = Path.Join(inputDirectory, ".cerberus", fileName, "Slave" + clientId + ".txt");
                Dictionary<string, string> result = new Dictionary<string, string>();

                string finalStatus;
                string timeTaken;
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "corral",
                        Arguments = "/clientId:" + clientId + " /traceInlining /proverLog:" + logFilePath + " /recursionBound:3 /trackAllVars /si /bootstrapFile:" + bootStrapFile + " " + inputFile,
                        UseShellExecute = false, RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
 
                process.Start();
                HashSet<string> toCapture = new HashSet<string>{"Total queries", "Return status", "Number of procedures inlined", "Total Time"};
                Regex regex = new Regex(@"([A-Za-z ]+): ([A-Za-z0-9. ]+)");
                using (StreamWriter writer = new StreamWriter(outputFilePath))  
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        writer.WriteLine(line);
                        writer.Flush();

                        Match match = regex.Match(line);
                        string key = match.Groups[1].Value;
                        string value = match.Groups[2].Value;
                        if (toCapture.Contains(key)) result.Add(key, value);
                    }
                }
 
                process.WaitForExit();
                if (result.ContainsKey("Return status")) connector.ReportFinished(result);
                
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