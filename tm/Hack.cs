using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace tm
{
    class Hack
    {
        const int PROCESS_WM_READ = 0x0010;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        IntPtr processHandle;
        IntPtr moduleBaseAddress;

        Dictionary<string, Int64> addresses;
        bool printGeekStats = true;

        List<int> timeAddressOffsets = new List<int> {
            0x1C50FB8,
            0x28,
            0xB8
        };

        List<int> mapOfflineNameOffsets = new List<int> { 0x1CE6998, 0x09 };
        List<int> mapOnlineNameOffsets = new List<int> { 0x1C53D78, 0x17 };

        public const string LOCAL_MAPNAME_ADDR_ID = "LocalMapName";
        public const string ONLINE_MAPNAME_ADDR_ID = "OnlineMapName";
        public const string TIME_ADDR_ID = "Time";
        public const string PROCESS_NAME = "ManiaPlanet";

        public bool PrintGeekStats
        {
            get { return printGeekStats; }
            set { printGeekStats = value; }
        }

        public Hack()
        {
            addresses = new Dictionary<string, Int64>();

            Process process = null;
            bool gameFound = false;

            while (!gameFound)
            {
                Console.WriteLine("Waiting for MainaPlanet");

                if (Process.GetProcessesByName(PROCESS_NAME).Count() > 0)
                {
                    process = Process.GetProcessesByName(PROCESS_NAME)[0];

                    if (process != null)
                        gameFound = true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            processHandle = OpenProcess(PROCESS_WM_READ, false, process.Id);
            moduleBaseAddress = process.MainModule.BaseAddress;

            if (printGeekStats)
                Console.WriteLine("Module base address : " + moduleBaseAddress.ToInt64().ToString("X"));

            Int64 timeAddress = CalculateAddress(processHandle, moduleBaseAddress, timeAddressOffsets);
            Int64 mapOfflineNameAddress = CalculateAddress(processHandle, moduleBaseAddress, mapOfflineNameOffsets);
            Int64 mapOnlineNameAddress = CalculateAddress(processHandle, moduleBaseAddress, mapOnlineNameOffsets);

            addresses.Add(TIME_ADDR_ID, timeAddress);
            addresses.Add(LOCAL_MAPNAME_ADDR_ID, mapOfflineNameAddress);
            addresses.Add(ONLINE_MAPNAME_ADDR_ID, mapOnlineNameAddress);

            if (printGeekStats)
            {
                Console.WriteLine("Time address : " + timeAddress.ToString("X"));
                Console.WriteLine("LocalMapName address : " + mapOfflineNameAddress.ToString("X"));
                Console.WriteLine("OnlineMapName address : " + mapOnlineNameAddress.ToString("X"));
            }
        }

        private Int64 CalculateAddress(IntPtr game_handle, IntPtr moduleBase, List<int> offsets)
        {
            byte[] nextAddress = new byte[8];
            int bytesread = 0; //unused

            if (offsets.Count == 1)
            {
                ReadProcessMemory((int)game_handle, ((Int64)moduleBase + offsets[0]), nextAddress, 8, ref bytesread);
            }
            else
            {
                //minus one as the last offset is not used to determine a address, but rather to determine a value
                for (int i = 0; i < (offsets.Count - 1); i++)
                {
                    if (i == 0)
                        ReadProcessMemory((int)game_handle, ((Int64)moduleBase + offsets[i]), nextAddress, 8, ref bytesread);
                    else
                        ReadProcessMemory((int)game_handle, (BitConverter.ToInt64(nextAddress, 0) + offsets[i]), nextAddress, 8, ref bytesread);

                    //if (printGeekStats)
                    //    Console.WriteLine("Intermediate address : " + BitConverter.ToInt64(nextAddress, 0).ToString("X"));
                }
            }

            return BitConverter.ToInt64(nextAddress, 0);
        }

        public string ReadString(Int64 address, int offset)
        {
            byte[] value = new byte[256];
            int bytesread = 0;

            try
            {
                ReadProcessMemory((int)processHandle, ((Int64)address + offset), value, value.Length, ref bytesread);
            }
            catch (Exception e)
            {
                throw e;
            }

            return Encoding.UTF8.GetString(value);
        }

        public Int64 ReadAddress(Int64 address, int offset)
        {
            byte[] value = new byte[8];
            int bytesread = 0;

            try
            {
                ReadProcessMemory((int)processHandle, ((Int64)address + offset), value, 8, ref bytesread);
            }
            catch (Exception e)
            {
                throw e;
            }

            return BitConverter.ToInt64(value, 0);
        }

        public Int64 GetAddressByName(string name)
        {
            Int64 val = 0;
            if (addresses.ContainsKey(name))
            {
                addresses.TryGetValue(name, out val);
                return val;
            }

            return val;
        }

        public String GetOfflineMapName()
        {
            return ReadString(GetAddressByName(LOCAL_MAPNAME_ADDR_ID), mapOfflineNameOffsets.Last());
        }

        public String GetOnlineMapName()
        {
            return ReadString(GetAddressByName(ONLINE_MAPNAME_ADDR_ID), mapOnlineNameOffsets.Last());
        }

        public int GetMapTime()
        {
            return (int)ReadAddress(GetAddressByName(TIME_ADDR_ID), timeAddressOffsets.Last());
        }

        public Boolean RecalculateAddress(string id)
        {
            List<int> offsets;

            if (id.Equals(LOCAL_MAPNAME_ADDR_ID))
            {
                offsets = mapOfflineNameOffsets;
            }
            else if (id.Equals(ONLINE_MAPNAME_ADDR_ID))
            {
                offsets = mapOnlineNameOffsets;
            }
            else if (id.Equals(TIME_ADDR_ID))
            {
                offsets = timeAddressOffsets;
            }
            else
            {
                return false;
            }

            Int64 newAddress = CalculateAddress(processHandle, moduleBaseAddress, offsets);

            addresses[id] = newAddress;

            if (newAddress > 0)
                Console.WriteLine(string.Format("Recalculated {0} address at {1}", id, newAddress.ToString("X")));

            return newAddress > 0;
        }

        public IntPtr GetProcessHandle()
        {
            return processHandle;
        }

        public IntPtr GetModuleBaseAddress()
        {
            return moduleBaseAddress;
        }
    }
}
