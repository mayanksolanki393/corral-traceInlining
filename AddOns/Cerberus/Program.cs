using System;
using System.Diagnostics;
using MessagePassing;
using System.Text.RegularExpressions;

namespace Cerberus {

    class Config {
        public string serverUrl {get; set;}
        public string instanceType {get; private set;}
        public string inputFilePath {get; private set;}
        public string inputDirectory {get {return Path.GetDirectoryName(inputFilePath);} private set{}}
        public string inputFileName {get {return Path.GetFileName(inputFilePath);} private set{}}
        public string runType {get; private set;}
        public int maxWorkers {get; private set;}
        public List<string> corralArgs {get; private set;}
        public bool genSMTLogs {get; private set;}
            
        public static Config ParseConfig(string[] args) {
            Config config = new Config();
            config.corralArgs = new List<string>();
            config.instanceType = "server"; //default instaceType
            config.genSMTLogs = false;
            config.runType = "bootstrap";

            foreach (var arg in args) {
                if (arg.StartsWith("--")) {
                    if (arg.StartsWith("--serverUrl")) {
                        string[] items = arg.Split(":");
                        config.serverUrl = items[1] + ":" + items[2] + ":" + items[3];
                    }
                    else if (arg.StartsWith("--client")) {
                        config.instanceType = "client";
                    }
                    else if (arg.StartsWith("--maxWorkers")) {
                        string[] items = arg.Split(":");
                        config.maxWorkers = int.Parse(items[1]);
                    }
                    else if (arg.StartsWith("--genSMTLogs")) {
                        config.genSMTLogs = true;
                    }
                    else if (arg.StartsWith("--inputFile")) {
                        string[] items = arg.Split(":");
                        config.inputFilePath = items[1];
                    }
                    else if (arg.StartsWith("--runType")) {
                        string[] items = arg.Split(":");
                        config.runType = items[1];
                    }
                    else {
                        Console.WriteLine("Unexpected Cerberus Argument: {0}", arg);
                        System.Environment.Exit(1);
                    }
                }
                else if (arg.StartsWith("/")) {
                    config.corralArgs.Add(arg);
                }
                else {
                    config.inputFilePath = arg;
                }
            }

            return config;
        }
        
        public override String ToString() {
            return String.Join("\n", new List<string>{
                "=========================================",
                "Server URL:      " + serverUrl,
                "Instance Type:   " + instanceType,
                "Run Type:        " + runType,
                "Input File Path: " + inputFilePath,
                "Max Workers:     " + maxWorkers,
                "Generate SMT:    " + genSMTLogs,
                "Corral Args:     " + "\n\t" + String.Join("\n\t", corralArgs),
                "=========================================\n"
            });
        }
    }

    class Program {
        private static List<string> trustMapping = new List<string> {
            " /numberOfStates:1 /initialState:0 ",

            " /numberOfStates:2 /initialState:0 ",
            " /numberOfStates:2 /initialState:1 ",
            
            " /numberOfStates:4 /initialState:0 ",
            " /numberOfStates:4 /initialState:1 ",
            " /numberOfStates:4 /initialState:2 ",
            " /numberOfStates:4 /initialState:3 ",

            " /numberOfStates:8 /initialState:0 ",
            " /numberOfStates:8 /initialState:1 ",
            " /numberOfStates:8 /initialState:2 ",
            " /numberOfStates:8 /initialState:3 ",
            " /numberOfStates:8 /initialState:4 ",
            " /numberOfStates:8 /initialState:5 ",
            " /numberOfStates:8 /initialState:6 ",
            " /numberOfStates:8 /initialState:7 "
        };

        static public void Main(String[] args) {
            Config config = Config.ParseConfig(args);
            
            if (config.instanceType == "server") {
                Console.Write("Starting Server - ");
                HttpMaster master = new HttpMaster(config.serverUrl);
                
                Console.WriteLine("Executing With:");
                Console.WriteLine(config);

                Thread masterThread = new Thread(master.Start);
                masterThread.IsBackground = true;
                masterThread.Start();
                Thread.Sleep(1000);
                Console.WriteLine("Server Started!");
                
                Console.WriteLine("Starting Clients - ");

                var requiredWorkers = config.maxWorkers;
                string[] bootStrapFiles = {};
                if (config.runType == "bootstrap") {
                    String bootStrapDir = Path.Join(config.inputDirectory, ".cerberus", config.inputFileName, "bootstrap");
                    if (Directory.Exists(bootStrapDir)) {
                        bootStrapFiles = Directory.GetFiles(bootStrapDir, "*.tree", SearchOption.TopDirectoryOnly);
                    }
                    else {
                        //This directory is being created for storing logs of clients
                        Directory.CreateDirectory(bootStrapDir);
                    }
                    requiredWorkers = Math.Min(config.maxWorkers-1, bootStrapFiles.Length) + 1;
                }

                for (var i=0; i<requiredWorkers; i++) {

                    string corralArgs = "--serverUrl:" + config.serverUrl + " --client ";
                    if (config.runType == "bootstrap") {
                        corralArgs += (i!=requiredWorkers-1 ? " /bootstrapFile:" + bootStrapFiles[i] : "");
                    }
                    else if (config.runType == "delayInlining") {
                        corralArgs += trustMapping[i];
                    }
                    corralArgs += String.Join(" ", args) + " " + config.inputFilePath;

                    Console.WriteLine("Running Client {0} with \n{1}", i, corralArgs);

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "Cerberus",
                            Arguments = corralArgs,
                            UseShellExecute = false, 
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                }
                masterThread.Join();
            }
            else {
                CorralDriver driver = new CorralDriver(config);
                driver.Start();
            }
            
        }
    }

    class CorralDriver {
        private Config config;

        public CorralDriver(Config config) {
            this.config = config;
        }
        
        public void Start() {
            Communicator connector = new HttpCommunicator(config.serverUrl);
            int clientId = connector.Register();
            try
            {
                string outputFilePath = Path.Join(config.inputDirectory, ".cerberus", config.inputFileName, "Client" + clientId + ".txt");
                string smtLogFilePath = Path.Join(config.inputDirectory, ".cerberus", config.inputFileName, "Client" + clientId + ".smt2");
                Dictionary<string, Object> result = new Dictionary<string, Object>();

                List<string> corralArgsFinal = new List<string>();
                corralArgsFinal.AddRange(config.corralArgs);
                corralArgsFinal.Add("/serverUrl:" + config.serverUrl);
                corralArgsFinal.Add("/clientId:" + clientId);
                if (config.genSMTLogs) corralArgsFinal.Add("/proverLog:" + smtLogFilePath);
                corralArgsFinal.Add(config.inputFilePath);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "corral",
                        Arguments = String.Join(" ", corralArgsFinal),
                        UseShellExecute = false, 
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
 
                process.Start();

                HashSet<string> toCapture = new HashSet<string>{"Return status", "Number of procedures inlined", "Total Time", "SummaryUsed", "SummaryRefined", "SummaryMethodInlined", "SummaryLargerThanVC", "Quantifier in summary"};
                Regex regex = new Regex(@"([A-Za-z ]+): ([A-Za-z0-9. ]+)");
                int numQueries = 0;
                using (StreamWriter writer = new StreamWriter(outputFilePath))  
                {
                    writer.WriteLine("Corral Args: {0}", String.Join(" ", corralArgsFinal));
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        writer.WriteLine(line);
                        writer.Flush();

                        if (line.Contains("OQ Outcome:")) numQueries++;

                        Match match = regex.Match(line);
                        string key = match.Groups[1].Value;
                        string value = match.Groups[2].Value;
                        if (toCapture.Contains(key) && !result.ContainsKey(key)) result.Add(key, value);
                    }
                }
                result.Add("Total queries", numQueries);
 
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