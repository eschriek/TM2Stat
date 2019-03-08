using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace tm
{
    class Program
    {

        static void Main(string[] args)
        {
            Hack h = new Hack();
            h.PrintGeekStats = true;

            while(h.GetAddressByName(Hack.NICKNAME_ADDR_ID) == 0)
            {
                h.RecalculateAddress(Hack.NICKNAME_ADDR_ID);

                Console.WriteLine("Waiting for player...");

                System.Threading.Thread.Sleep(2000);
            }

            Player player = new Player { NickName = Utils.DecodeAndPrintTrackManiaColorString(h.GetPlayerNickname()) };

            Console.WriteLine("Player : " + player.NickName);

            MainLoop mainLoop = new MainLoop(h, player);
            mainLoop.Loop();
        }
    }
}
