using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Valheim_Server_Monitor
{
    
    class Program
    {
        static Logger Logger;
        static ServerConfig Config;
        static ValheimServerInfo ServerInfo = new ValheimServerInfo(); 

        static void Main(string[] args) {
            Logger = new Logger();
            Log("Logger started");
            Config = new ServerConfig();
            Log("Loaded server config");
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            String valheimDir = @"E:\SteamLibrary\steamapps\common\Valheim dedicated server";
            String valheimExe = "valheim_server.exe";
            RunApiServer(cancelSource.Token);

            Log("Locating Valheim Server file...");
            if (File.Exists(Path.Combine(valheimDir, valheimExe)))
                Log("Valheim server found...");
            else {
                Log("ERR: Valheim server EXE not found...", true);
            }
            
            ServerInfo.Name = Config.Name;
            ServerInfo.Port = Config.Port;
            ServerInfo.World = Config.World;
            ServerInfo.IsPassworded = !String.IsNullOrEmpty(Config.Password);
            ServerInfo.IsPublic = Config.IsPublic;

            ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(valheimDir, valheimExe))
            {
                WorkingDirectory = valheimDir,
                Arguments = Config.getRunArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
            };

            Process serverProc = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            Log($"Starting {valheimExe}");
            serverProc.Start();

            ReadStreamToConsoleAsync(serverProc.StandardOutput, cancelSource.Token);
            ReadStreamToConsoleAsync(serverProc.StandardError, cancelSource.Token);

            bool StoppingServer = false;
            while(!serverProc.HasExited)
            {
                if(StoppingServer)
                {
                    Thread.Sleep(500);
                    Console.Write(".");
                    continue;
                }

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Q) {
                    Log($"Shutting down server...");
                    StoppingServer = true;
                    ProcessStopHandler.StopProgram(serverProc);
                }
                else
                    Log(JsonConvert.SerializeObject(ServerInfo, Formatting.Indented));
            }
            Log($"Server shut down");
            Log($"Releasing and disposing of resources");

            cancelSource.Cancel();
            Logger.Close();
        }

        static Task ReadStreamToConsoleAsync(StreamReader reader, CancellationToken cancelToken) {
            return Task.Run(() => {
                while(true) {
                    String line = reader.ReadLine();
                    line = line.TrimEnd();

                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        if (line.StartsWith("(") && line.EndsWith(")")) { }
                        else {
                            ServerInfo.HandleServerMessage(line);
                            Log(line);
                        }
                    }
                }
            }, cancelToken);
        }

        static void Log(object data, bool isError = false)
        {
            if (isError)
                Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(data);
            Console.ResetColor();
            Logger.LogLine(data);
        }
    
        static void RunApiServer(CancellationToken CancelToken)
        {
            Task.Run(() => {
                HttpListener httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://+:8080/valheim/status/");
                httpListener.Start();

                while (true)
                {
                    HttpListenerContext context = httpListener.GetContext();

                    Task.Run(() => {
                        HttpListenerResponse response = context.Response;
                        response.ContentType = "application/json";
                        using (StreamWriter writer = new StreamWriter(response.OutputStream))
                        {
                            writer.Write(JsonConvert.SerializeObject(ServerInfo, Formatting.Indented));
                            writer.Flush();
                            writer.Close();
                        }
                    });
                }
            }, CancelToken);
        }
    }

    public class ValheimServerInfo
    {
        public ValheimServerInfo()
        {
            IsSteamGameServerInitialized = false;
            IsGameServerConnected = false;
        }

        public void HandleServerMessage(String message)
        {
            if (message.Contains("Initialize engine version")) {
                //Initialize engine version: 2019.4.20f1 (6dd1c08eedfa)
                EngineVersion = Regex.Match(message, @"version: ([0-9a-z\.]+)").Groups[1].Value;
            }
            else if (message.Contains("Initializing world generator")) {
                //03/02/2021 19:47:41: Initializing world generator seed:WTwHcgt9uL ( -2014396962 )   menu:False  worldgen version:1
                WorldSeed = Regex.Match(message, @"seed:([^)]+\))").Groups[1].Value;
                WorldGenVersion = Regex.Match(message, @"worldgen version:([0-9]+)").Groups[1].Value;
            }
            else if (message.Contains("Render threading mode")) {
                //03/02/2021 19:47:40: Render threading mode:SingleThreaded
                RenderThreadingMode = message.Substring(message.LastIndexOf(":")+1);
            }
            else if (message.Contains("Get create world")) {
                //03/02/2021 19:47:40: Get create world Sigfrej
                World = message.Substring(message.LastIndexOf(" ") + 1);
            }
            else if (message.Contains("Server ID")) {
                //03/02/2021 19:47:40: Server ID 90071992547409920
                ServerId = message.Substring(message.LastIndexOf(" ") + 1);
            }
            else if (message.Contains("Steam game server initialized")) {
                //03/02/2021 19:47:40: Steam game server initialized
                IsSteamGameServerInitialized = true;
            }
            else if (message.Contains("Zonesystem Awake")) {
                // 03/02/2021 19:47:41: Zonesystem Awake 374
                Zonesystem = int.Parse(Regex.Match(message, @"Zonesystem Awake ([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("DungeonDB Awake")) {
                // 03/02/2021 19:47:41: DungeonDB Awake 374
                DungeonDB = int.Parse(Regex.Match(message, @"DungeonDB Awake ([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Using mountain distance")) {
                // 03/02/2021 19:47:41: Using mountain distance: 1000
                MountainDistance = int.Parse(Regex.Match(message, @"Using mountain distance: ([0-9]+)").Groups[1].Value);
            }
            else if (message.EndsWith("mountain points")) {
                // 03/02/2021 19:47:41: Found 556 mountain points
                MountainPoints = int.Parse(Regex.Match(message, @"Found ([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Remaining mountains")) {
                // 03/02/2021 19:47:41: Remaining mountains:67
                RemainingLakes = int.Parse(Regex.Match(message, @"Remaining mountains:([0-9]+)").Groups[1].Value);
            }
            else if (message.EndsWith("lake points")) {
                // 03/02/2021 19:47:41: Found 8348 lake points
                LakePoints = int.Parse(Regex.Match(message, @"Found ([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Remaining lakes")) {
                // 03/02/2021 19:47:43: Remaining lakes:114
                RemainingLakes = int.Parse(Regex.Match(message, @"Remaining lakes:([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Rivers:")) {
                // 03/02/2021 19:47:43: Rivers:136
                Rivers = int.Parse(Regex.Match(message, @"Rivers:([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("River buckets")) {
                // 03/02/2021 19:47:43: River buckets 14014
                RiverBuckets = int.Parse(Regex.Match(message, @"River buckets ([0-9]+)").Groups[1].Value);
            }
            else if (message.EndsWith(" streams")) {
                // 03/02/2021 19:47:44: Placed 1883 streams
                StreamsPlaced = int.Parse(Regex.Match(message, @"Placed ([0-9]+)").Groups[1].Value);
            }
            else if (message.EndsWith(" locations")) {
                // 03/02/2021 19:47:45: Loaded 7115 locations
                LocationsLoaded = int.Parse(Regex.Match(message, @"Loaded ([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Game server connected")) {
                // 03/02/2021 19:47:49: Game server connected
                IsGameServerConnected = true;
            }
            else if (message.Contains("World saved")) {
                // 03/02/2021 20:07:50: World saved ( 597,4449ms )
                LastGameSave = DateTime.UtcNow;
            }
            else if (message.Contains(", day:")) {
                //03/07/2021 23:03:42: Time 392275,57418574, day:217    nextm:392670,000010729  skipspeed:32,8688187490528
                Day = int.Parse(Regex.Match(message, @"day:([0-9]+)").Groups[1].Value);
            }
            else if (message.Contains("Got connection SteamID")) {
                //03/02/2021 19:49:33: Got connection SteamID 76561198000887816
                String SteamId = Regex.Match(message, @"Got connection SteamID ([0-9]+)").Groups[1].Value;
                Players.Add(new PlayerInfo(SteamId));
            }
            else if (message.Contains("Got handshake from client")) {
                //03/02/2021 19:49:33: Got handshake from client 76561198000887816
                String SteamId = Regex.Match(message, @"Got handshake from client ([0-9]+)").Groups[1].Value;
                //players.First((p) => p.SteamID == SteamId).Handshake = true;
            }
            else if (message.Contains("VERSION check")) {
                //03/02/2021 19:49:36: VERSION check their:0.147.3  mine:0.147.3
                String GameVer = Regex.Match(message, @"their:([0-9\.]+)").Groups[1].Value;
                Players.Last().GameVersion = GameVer;
            }
            else if (message.Contains("Got character ZDOID from")) {
                //03/02/2021 19:50:56: Got character ZDOID from Proud : 229722225:1
                String ZDoid = Regex.Match(message, @" : ([0-9:]+)").Groups[1].Value;
                String Name = Regex.Match(message, @"from (\S+)").Groups[1].Value;
                Players.Last().CharacterZDOID = ZDoid;
                Players.Last().Name = Name;
            }
            else if (message.Contains("Closing socket")) {
                //03/02/2021 19:49:36: Closing socket 76561198000887816
                String SteamId = Regex.Match(message, @"Closing socket ([0-9]+)").Groups[1].Value;
                Players.RemoveAll((p) => p.SteamID == SteamId);
            }
        }

        [JsonProperty("name")]
        public String Name { get; set; }
        
        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("isPublic")]
        public bool IsPublic { get; set; }

        [JsonProperty("isPassworded")]
        public bool IsPassworded { get; set; }

        [JsonProperty("players")]
        public List<PlayerInfo> Players = new List<PlayerInfo>();

        [JsonProperty("serverStatus")]
        public String ServerStatus
        {
            get
            {
                if (!IsGameServerConnected)
                    return "Starting";
                else
                    return "Running";
            }
        }

        [JsonProperty("engineVersion")]
        public String EngineVersion { get; set; }

        [JsonProperty("worldSeed")]
        public String WorldSeed { get; set; }

        [JsonProperty("worldGenVersion")]
        public String WorldGenVersion { get; set; }

        [JsonProperty("renderThreadingMode")]
        public String RenderThreadingMode { get; set; }

        [JsonProperty("world")]
        public String World { get; set; }

        [JsonProperty("serverId")]
        public String ServerId { get; set; }

        [JsonProperty("isSteamGameServerInitialized")]
        public bool IsSteamGameServerInitialized { get; set; }

        [JsonProperty("zonesystem")]
        public int Zonesystem { get; set; }

        [JsonProperty("dungeonDB")]
        public int DungeonDB { get; set; }

        [JsonProperty("mountainDistance")]
        public int MountainDistance { get; set; }

        [JsonProperty("mountainPoints")]
        public int MountainPoints { get; set; }

        [JsonProperty("remainingMountains")]
        public int RemainingMountains { get; set; }

        [JsonProperty("lakePoints")]
        public int LakePoints { get; set; }

        [JsonProperty("remainingLakes")]
        public int RemainingLakes { get; set; }

        [JsonProperty("rivers")]
        public int Rivers { get; set; }

        [JsonProperty("riverBuckets")]
        public int RiverBuckets { get; set; }

        [JsonProperty("streamsPlaced")]
        public int StreamsPlaced { get; set; }

        [JsonProperty("locationsLoaded")]
        public int LocationsLoaded { get; set; }

        [JsonProperty("isGameServerConnected")]
        public bool IsGameServerConnected { get; set; }

        [JsonProperty("lastGameSave")]
        public DateTime LastGameSave { get; set; }

        [JsonProperty("day")]
        public int Day { get; set; }
    }

    public class PlayerInfo
    {
        public PlayerInfo(String SteamId) {
            SteamID = SteamId;
        }
        [JsonProperty("steamID")]
        public String SteamID { get; set; }
        [JsonProperty("gameVersion")]
        public String GameVersion { get; set; }
        [JsonProperty("characterZDOID")]
        public String CharacterZDOID { get; set; }
        [JsonProperty("name")]
        public String Name { get; set; }
    }
}

