using System.Diagnostics;
using System.Net;
using Newtonsoft.Json;

namespace LOBBYN
{
    internal class StartupChecker
    {
        private bool lcuRunning;
        private int? _lcuPort = null;
        private string? _lcuToken = null;
        private HttpClient client = new HttpClient(new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            }
        });
        public StartupChecker()
        {
            if (_isRunning("LeagueClientUx"))
            {
                lcuRunning = true;
            }
            else
            {
                lcuRunning = false;
                return;
            }

            List<(string, string)> leagueArgs = _getProcessCmdArgs("LeagueClientUx.exe");

            foreach ((string arg, string value) in leagueArgs)
            {

                if (arg == "app-port")
                {
                    _lcuPort = int.Parse(value);
                }
                else if (arg == "remoting-auth-token")
                {
                    _lcuToken = _Base64Encode("riot:" + value);
                }

                if (_lcuPort != null && _lcuToken != null) break;
            }
        }

        public async Task<bool> IsLCUReady()
        {
            if (!lcuRunning) return false;

            string requestUrl = $"https://127.0.0.1:{_lcuPort}/lol-gameflow/v1/availability";

            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(requestUrl),
                Headers =
                    {
                        { HttpRequestHeader.Authorization.ToString(), "Basic " + _lcuToken },
                        { HttpRequestHeader.Accept.ToString(), "application/json" }
                    },
            };

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request);
            }
            catch { return false; }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                GameflowAvailability gameflowAvailability = JsonConvert.DeserializeObject<GameflowAvailability>(await response.Content.ReadAsStringAsync());

                return gameflowAvailability.isAvailable;
            }
            else
            {
                return false;
            }

        }


        private bool _isRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        private List<(string, string)> _getProcessCmdArgs(string processName)
        {
            string command = $"wmic process where \"caption='{processName}'\" get CommandLine";
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c " + command;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            List<(string, string)> outputList = new List<(string, string)>();

            foreach (string line in output.Split("\" \""))
            {
                string rawArg = line.Replace("\"", "").Replace("--", "");

                if (rawArg.Contains("="))
                {
                    string arg = rawArg.Split("=")[0];
                    string value = rawArg.Split("=")[1];
                    outputList.Add((arg, value));
                }
                else
                {
                    outputList.Add((rawArg, ""));
                }
            }

            return outputList;
        }

        private string _Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private class GameflowAvailability
        {
            public bool isAvailable { get; set; }
            public string state { get; set; }
        }
    }
}