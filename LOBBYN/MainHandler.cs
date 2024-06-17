using PoniLCU;
using static PoniLCU.LeagueClient;

using Salaros.Configuration;

namespace LOBBYN
{
    internal class MainHandler
    {
        private static LeagueClient _lcu = new LeagueClient(credentials.cmd);
        private ConfigParser _config = new ConfigParser(File.ReadAllText("config.ini"));

        private string _logfilePath;
        public MainHandler()
        {
            _logfilePath = _config.GetValue("Logger", "logfile");
        }

        public void log(string message, bool toFile = true)
        {
            string dateSig = $"{DateTime.Now.ToString("[dd-MM-yyyy | HH:mm:ss]")}";
            message = $"{dateSig} {message}\n";
            Console.WriteLine(message);

            StreamWriter sw = new StreamWriter(_logfilePath);
            sw.Write(message);
            sw.Close();
        }
    }
}
