using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace soulspine.LOBBYN
{
    internal class Logger
    {
        public string logfilePath { get; private set; }

        public Logger(string logfilePath)
        {
            this.logfilePath = logfilePath;
        }

        public void log(string message, bool toFile = true)
        {
            string dateSig = $"{DateTime.Now.ToString("[dd-MM-yyyy | HH:mm:ss]")}";
            message = $"{dateSig} {message}";
            Console.WriteLine(message);

            if (!toFile) return;

            if (!File.Exists(logfilePath)) File.Create(logfilePath).Close();

            var sw = new StreamWriter(File.Open(logfilePath, FileMode.Append));
            sw.Write($"{message}\n");
            sw.Close();
        }

        public void logError(string message, bool toFile = true)
        {
            log($"[ERROR] {message}", toFile);
        }
    }
}
