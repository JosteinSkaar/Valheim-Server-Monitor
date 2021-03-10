using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Valheim_Server_Monitor
{
    public class ServerConfig
    {
        const String configFile = "server-config.json";
        const String DefaultName = "Valheim dedicated server";
        const int DefaultPort = 2456;
        const String DefaultWorld = "MyWorld";
        const String DefaultPassword = "";
        const int DefaultPublic = 1;

        public String Name { get; set; }
        public int Port { get; set; }
        public String World { get; set; }
        public String Password { get; set; }
        
        private int Public = 0;
        public bool IsPublic { 
            get {
                return Public == 1;
            }
            set {
                if (value)
                    Public = 1;
                else
                    Public = 0;
            }
        }

        public ServerConfig() {
            FileStream file;
            if (!File.Exists(configFile)) {
                file = File.Create(configFile);
                StreamWriter writer = new StreamWriter(file);
                String defConfig = GetDefaultConfig();
                writer.Write(defConfig);
                writer.Flush();

                file.Seek(0, SeekOrigin.Begin);
            } else {
                file = File.Open(configFile, FileMode.Open);
            }

            String configJson = new StreamReader(file).ReadToEnd();
            JObject config = JsonConvert.DeserializeObject<JObject>(configJson);

            this.Name = config.Value<string>("name") ?? DefaultName;
            this.Port = config.Value<int?>("port") ?? DefaultPort;
            this.World = config.Value<string>("world") ?? DefaultWorld;
            this.Password = config.Value<string>("password") ?? DefaultPassword;
            this.Public = config.Value<int?>("public") ?? DefaultPublic;
        }

        public string getRunArgs => $"-nographics -batchmode -name \"{Name}\" -port {Port} -world \"{World}\" -password \"{Password}\" -public {Public}";

        private string GetDefaultConfig() => $@"{{
    ""name"": ""{DefaultName}"",
    ""port"": {DefaultPort},
    ""world"": ""{DefaultWorld}"",
    ""password"": ""{DefaultPassword}"",
    ""public"": {DefaultPublic},
}}";
    }
}
