using EloBuddy;
using EloBuddy.SDK.Events;

namespace MoonyDiana
{
    class Program
    {
        static void Main(string[] args)
        {
            Loading.OnLoadingComplete += eventArgs =>
            {
                //if (ObjectManager.Player.ChampionName == "Diana")
                // ReSharper disable once ObjectCreationAsStatement
                new Main();
            };
        }
    }
}
