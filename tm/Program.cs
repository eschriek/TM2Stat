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
        #region unmanaged

        /// <summary>
        /// This function sets the handler for kill events.
        /// </summary>
        /// <param name="Handler"></param>
        /// <param name="Add"></param>
        /// <returns></returns>
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        //delegate type to be used of the handler routine
        public delegate bool HandlerRoutine(ConsoleCtrlHandlerCode CtrlType);

        // control messages
        public enum ConsoleCtrlHandlerCode : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        #endregion

        private static MainLoop mainLoop;

        static void Main(string[] args)
        {
            HandlerRoutine hr = new HandlerRoutine(ConsoleCtrlCheck);
            // we have to keep the handler routine alive during the execution of the program,
            // because the garbage collector will destroy it after any CTRL event
            GC.KeepAlive(hr);
            SetConsoleCtrlHandler(hr, true);

            Hack h = new Hack();
            h.PrintGeekStats = true;

            while (h.GetAddressByName(Hack.NICKNAME_ADDR_ID) == 0)
            {
                h.RecalculateAddress(Hack.NICKNAME_ADDR_ID);

                Console.WriteLine("Waiting for player...");

                System.Threading.Thread.Sleep(2000);
            }

            Player player = new Player { NickName = Utils.DecodeAndPrintTrackManiaColorString(h.GetPlayerNickname()) };

            Console.WriteLine("Player : " + player.NickName);

            mainLoop = new MainLoop(h, player);
            mainLoop.Loop();
        }

        private static bool ConsoleCtrlCheck(ConsoleCtrlHandlerCode eventCode)
        {
            switch (eventCode)
            {
                case ConsoleCtrlHandlerCode.CTRL_CLOSE_EVENT:
                case ConsoleCtrlHandlerCode.CTRL_BREAK_EVENT:
                case ConsoleCtrlHandlerCode.CTRL_LOGOFF_EVENT:
                case ConsoleCtrlHandlerCode.CTRL_SHUTDOWN_EVENT:

                    if(mainLoop != null)
                    {
                        mainLoop.WriteCSV();
                    }

                    Environment.Exit(0);
                    break;
            }

            return false;
        }
    }
}
