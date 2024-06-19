using PoniLCU;

namespace LOBBYN
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            MainHandler mainHandler = new MainHandler();

            while (mainHandler.isReady)
            {
                Console.WriteLine(mainHandler.isReady);
                Thread.Sleep(1000);
            }
            
        }
    }
}
