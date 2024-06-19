using PoniLCU;

namespace LOBBYN
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            MainHandler mainHandler = new MainHandler();
            mainHandler.log("Started LOBBYN.");

            while (!mainHandler.isConnected)
            {
                Thread.Sleep(1000);
            }

            Summoner summoner = await mainHandler.getSummoner("Hide on bush", "KR");
            mainHandler.log($"Summoner: {summoner.displayName} - Level {summoner.summonerLevel}", false);

            while (mainHandler.isConnected)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
