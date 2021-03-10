using System;
using System.IO;

namespace Valheim_Server_Monitor
{
    public class Logger
    {
        const String prevLogFile = "log.old.txt";
        const String logFile = "log.txt";

        static FileStream LogStream;
        static StreamWriter LogWriter;
        static StreamReader LogReader;

        public Logger() {
            if (File.Exists(logFile))
            {
                if (File.Exists(prevLogFile))
                    File.Delete(prevLogFile);
                File.Move(logFile, prevLogFile);
            }

            LogStream = File.Create(logFile);
            LogWriter = new StreamWriter(LogStream);
            LogReader = new StreamReader(LogStream);
        }

        public void Log(object data) {
            LogWriter.Write(data.ToString());
            LogWriter.Flush();
        }

        public void LogLine(object data) {
            LogWriter.WriteLine(data.ToString());
            LogWriter.Flush();
        }

        public string FullLog() {
            LogStream.Seek(0, SeekOrigin.Begin);
            return LogReader.ReadToEnd();
        }

        public void Close() {
            LogWriter.Close();
            LogReader.Close();
            LogStream.Close();
        }
    }
}
