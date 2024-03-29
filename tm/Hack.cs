﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace tm
{
    class Hack
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern int VirtualQueryEx(IntPtr handle, IntPtr baseAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int length);

        IntPtr processHandle;
        IntPtr moduleBaseAddress;

        Dictionary<string, Int64> addresses;
        bool printGeekStats = true;

        List<int> timeAddressOffsets = new List<int> { 0x1C50FB8, 0x28, 0xB8 };
        List<int> playerNicknameOffsets = new List<int> { 0x1C50EE8, 0x2D0, 0xD0, 0x58, 0x18, 0x20 };
        List<int> playerNicknameSzOffsets = new List<int> { 0x1C50EE8, 0x2D0, 0xD0, 0x58, 0x18, 0x1C };
        List<int> logInfoOffsets = new List<int> { 0x1C53D78, 0x00 };

        public const string SUPPORT_GAMEVERSION_STR = "date=2019-02-28_16_00 Svn=90238 GameVersion=3.3.0";
        /* This mask will hopefully match future version strings */
        const string GAMEVERSION_STR_MASK = "xxxxx????x??x??x??x??xxxxx?????xxxxxxxxxxxxx?????"; 
        const int VERSION_STR_LENGTH = 49;

        public const string MAPNAME_ADDR_ID = "MapName";
        public const string TIME_ADDR_ID = "Time";
        public const string NICKNAME_ADDR_ID = "PlayerNickname";
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
                Console.WriteLine("Waiting for ManiaPlanet");

                if (Process.GetProcessesByName(PROCESS_NAME).Count() > 0)
                {
                    process = Process.GetProcessesByName(PROCESS_NAME)[0];

                    if (process != null)
                        gameFound = true;
                }

                System.Threading.Thread.Sleep(2000);
            }

            /* 0x438 */
            processHandle = OpenProcess(1080, false, process.Id);
            moduleBaseAddress = process.MainModule.BaseAddress;

            if (printGeekStats)
                Console.WriteLine("Module base address : " + moduleBaseAddress.ToInt64().ToString("X"));

            Int64 timeAddress = CalculateAddress(processHandle, moduleBaseAddress, timeAddressOffsets);
            Int64 nicknameAddress = CalculateAddress(processHandle, moduleBaseAddress, playerNicknameOffsets);
            Int64 logInfoAddress = CalculateAddress(processHandle, moduleBaseAddress, logInfoOffsets);

            addresses.Add(TIME_ADDR_ID, timeAddress);
            addresses.Add(NICKNAME_ADDR_ID, nicknameAddress);
            addresses.Add(MAPNAME_ADDR_ID, logInfoAddress);

            if (printGeekStats)
            {
                Console.WriteLine("Time address : " + timeAddress.ToString("X"));
                Console.WriteLine("Nickname address : " + nicknameAddress.ToString("X"));
                Console.WriteLine("MapName address : " + logInfoAddress.ToString("X"));
            }
        }

        /* For 64 bit processes, which Maniaplanet is nowadays */
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

        public string ReadString(Int64 address, int offset, int bytes = 256, Boolean splitByte = false, byte splitByteDelimiter = 0x00)
        {
            byte[] value = new byte[bytes];
            int bytesread = 0;

            try
            {
                ReadProcessMemory((int)processHandle, ((Int64)address + offset), value, value.Length, ref bytesread);
            }
            catch (Exception e)
            {
                throw e;
            }

            if (splitByte)
            {
                var idx = Array.IndexOf(value, splitByteDelimiter);

                if (idx != -1)
                {
                    value = value.Skip(0).Take(idx).ToArray();
                }
            }

            return Encoding.UTF8.GetString(value);
        }

        public int ReadAddress32(Int64 address, int offset)
        {
            byte[] value = new byte[4];
            int bytesread = 0;

            try
            {
                ReadProcessMemory((int)processHandle, ((Int64)address + offset), value, 4, ref bytesread);
            }
            catch (Exception e)
            {
                throw e;
            }

            return BitConverter.ToInt32(value, 0);
        }

        public Int64 ReadAddress64(Int64 address, int offset)
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

        private List<IntPtr> AOBScan(byte[] arr, string pattern, int offset = 0)
        {
            Int64 current = 0;
            MEMORY_BASIC_INFORMATION memInfo = new MEMORY_BASIC_INFORMATION();

            /* The 0x7FFFFFFFFFFFFFFF limit might be a bit overkill... */
            while ((ulong)current < 0x7FFFFFFFFFFFFFFF && VirtualQueryEx(processHandle, new IntPtr(current), out memInfo, Marshal.SizeOf(memInfo)) != 0)
            {
                if (memInfo.State == 4096 && memInfo.Protect == 4 && (uint)memInfo.RegionSize != 0)
                {
                    SigScan s = new SigScan(processHandle, new IntPtr(current), memInfo.RegionSize.ToInt32());
                    List<IntPtr> addresses = s.FindPattern(arr, pattern, offset);

                    if (addresses.Count > 0)
                    {
                        return addresses;
                    }
                }

                current = memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64();
            }

            return null;
        }

        public String GetMapName()
        {
            var log = ReadString(GetAddressByName(MAPNAME_ADDR_ID), logInfoOffsets.Last());

            var pattern = @"\[Game\] init challenge '(.+?)'";
            var match = Regex.Match(log, pattern);

            if (match.Groups.Count < 2)
            {
                return MainLoop.MAP_NOMAP;
            }

            return match.Groups[1].Value;
        }

        public String GetGameVersion()
        {
            List<IntPtr> results = AOBScan(Encoding.ASCII.GetBytes(SUPPORT_GAMEVERSION_STR), "xxxxx????x??x??x??x??xxxxx?????xxxxxxxxxxxxx?????");

            /* Dont care which of the resulting addressses we pick, as we scanned for a specific array */
            if (results.Count > 0)
            {
                return ReadString(results.FirstOrDefault().ToInt64(), 0, VERSION_STR_LENGTH);
            }

            return "";
        }

        public String GetPlayerNickname()
        {
            return ReadString(GetAddressByName(NICKNAME_ADDR_ID), playerNicknameOffsets.Last(), GetPlayerNicknameSize());
        }

        public int GetPlayerNicknameSize()
        {
            return (int)ReadAddress32(GetAddressByName(NICKNAME_ADDR_ID), playerNicknameSzOffsets.Last());
        }

        public int GetMapTime()
        {
            return (int)ReadAddress64(GetAddressByName(TIME_ADDR_ID), timeAddressOffsets.Last());
        }

        public Boolean RecalculateAddress(string id)
        {
            List<int> offsets;

            /* Beautiful construct below */
            if (id.Equals(MAPNAME_ADDR_ID))
            {
                offsets = logInfoOffsets;
            }
            else if (id.Equals(TIME_ADDR_ID))
            {
                offsets = timeAddressOffsets;
            }
            else if (id.Equals(NICKNAME_ADDR_ID))
            {
                offsets = playerNicknameOffsets;
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
