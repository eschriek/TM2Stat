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
            var name = "Schriek"; //todo

            Player player = new Player { NickName = name };

            Hack h = new Hack();
            h.PrintGeekStats = true;

            //string[] joysticks = Input.FindJoysticks();
            //foreach (string s in joysticks)
            //{
            //    Console.WriteLine(s);
            //}

            MainLoop mainLoop = new MainLoop(h, player);
            mainLoop.Loop();
        }
    }
}
