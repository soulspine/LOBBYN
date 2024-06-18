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

            await mainHandler.CreateLobby(mapId:MapType.HOWLING_ABYSS, pickType:PickType.BLIND, password:"DHUWADHUIAWDHUJAW");

            while (mainHandler.isConnected)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
