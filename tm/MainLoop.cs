using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tm
{
    class MainLoop
    {

        bool mapChanged;
        bool roundStart, roundEnd;

        int previousTime;

        FixedSizedQueue<int> time_history;
        const int time_history_limit = 10; /* Size of aforementioned fixed size Q */
        const int finish_detection_threshold_low = 5; /* Time counts 'hangs' for a short while when finished, this acts as threshold */
        const int period_info_interval = 10; /* self explanatory */

        const int REFRESH_RATE = 100;
        const string MAP_NOMAP = "Unnamed";

        enum State
        {
            RESET, ROUND_BUSY, ROUND_SYNC, ROUND_CRASHED, ROUND_FINISHED
        }

        private State currentState;
        private State state
        {
            get
            {
                return this.currentState;
            }

            set
            {
                switch (value)
                {
                    case State.RESET:
                        {
                            roundStart = false;
                            roundEnd = false;

                            break;
                        }
                    case State.ROUND_BUSY:
                        {
                            if (this.currentState == State.ROUND_SYNC) //previous state sync -> round start
                            {
                                roundStart = true;
                            }

                            break;
                        }
                    case State.ROUND_SYNC:
                        {
                            if (this.currentState == State.ROUND_BUSY) //busy -> sync -> round end
                            {
                                roundEnd = true;
                            }

                            break;
                        }
                    case State.ROUND_CRASHED:
                        {
                            roundEnd = true;
                            break;
                        }
                    case State.ROUND_FINISHED:
                        {
                            roundEnd = true;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }

                this.currentState = value;
            }
        }

        private string currentMapName;
        private string CurrentMapName
        {
            get
            {
                return this.currentMapName;
            }

            set
            {

                if (value != this.currentMapName)
                {
                    this.currentMapName = value;

                    if (value != MAP_NOMAP)
                    {
                        Console.WriteLine("Switching map to : " + value);
                        mapChanged = true;

                    }
                }
            }
        }

        Hack hack;
        Player player;
        Map currentMap;

        public MainLoop(Hack h, Player p)
        {
            this.hack = h;
            this.player = p;

            time_history = new FixedSizedQueue<int>(time_history_limit);

            roundStart = false;
            roundEnd = false;
            previousTime = -1;

            CurrentMapName = MAP_NOMAP;
            mapChanged = false;

            state = State.RESET;
        }

        public void Loop()
        {
            while (true)
            {
                CurrentMapName = Utils.TrimMapNameString(hack.GetOnlineMapName());

                if (mapChanged)
                {
                    mapChanged = false;

                    currentMap = new Map() { Name = CurrentMapName };

                    player.PlayedMaps.Add(currentMap);
                }

                if (CurrentMapName != MAP_NOMAP) //currently playing a map
                {
                    int time = hack.GetMapTime();

                    time_history.Enqueue(time);

                    var q = from x in time_history
                            group x by x into g
                            let count = g.Count()
                            where count >= finish_detection_threshold_low &&
                                    g.Key != -1
                            select new { Value = g.Key, Count = count };

                    /* This counts as a valid finish time */
                    if (q.Count() > 0 &&
                        time == -1 && previousTime > 0)
                    {
                        state = State.ROUND_FINISHED;

                        currentMap.Times.Add(new Time() { Value = q.First().Value, When = DateTime.Now });
                    }
                    else
                    {
                        /* All other states */
                        if (time == -1 && previousTime > 0)
                        {
                            state = State.ROUND_CRASHED;

                            currentMap.AmountCrashes++;
                        }
                        else if (time == -1 && previousTime == -1)
                        {
                            state = State.RESET;
                        }
                        else if (time == 0)
                        {
                            state = State.ROUND_SYNC;
                        }
                        else
                        {
                            state = State.ROUND_BUSY;
                        }
                    }

                    if (state == State.ROUND_FINISHED)
                    {
                        var str = string.Format("Finish : {0}, Mean : {1}, StdDev : {2}, F/C : {3}",
                            Utils.FormatTimeStamp(previousTime),
                            Utils.FormatTimeStamp((int)Math.Floor(currentMap.MeanTime())),
                            Utils.FormatTimeStamp((int)Math.Floor(currentMap.StdDev())),
                            currentMap.FCRatio());

                        Console.WriteLine(str);
                        roundEnd = false;

                        if (currentMap.Times.Count() % period_info_interval == 0)
                        {
                            str = string.Format("(Session) Best time : {0} on map {1}, {3} finishes and {4} crashes",
                                Utils.FormatTimeStamp(currentMap.Times.Min(x => x.Value)),
                                currentMap.Name,
                                currentMap.Times.Count(),
                                currentMap.AmountCrashes);
                        }
                    }

                    previousTime = time;
                }

                /* Check if TM is still running and if not, export the data */
                var process = Process.GetProcessesByName(Hack.PROCESS_NAME);
                if (process.Length == 0)
                {
                    break;
                }

                CheckOfflineMapAddress();

                System.Threading.Thread.Sleep(REFRESH_RATE);
            }

            foreach (Map m in player.PlayedMaps)
            {
                var csv = new StringBuilder();

                var header = string.Format("{0},{1},{2}", m.Times.Count(), m.AmountCrashes, m.FCRatio());

                foreach (Time t in m.Times)
                {
                    var newLine = string.Format("{0},{1}", t.Value, t.When);
                    csv.Append(newLine);
                }

                File.WriteAllText(GetSafeFilename(string.Format("{0}_{1}.csv", m.Name, player.NickName)), csv.ToString());
            }
        }

        private void CheckOfflineMapAddress()
        {
            var address = hack.GetAddressByName(Hack.LOCAL_MAPNAME_ADDR_ID);

            if (address == 0)
            {
                Boolean ret = hack.RecalculateAddress(Hack.LOCAL_MAPNAME_ADDR_ID);

                if (ret)
                {
                    CurrentMapName = Utils.TrimMapNameString(hack.GetOfflineMapName());
                }
            }
        }

        private string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }

    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    T outObj;
                    base.TryDequeue(out outObj);
                }
            }
        }
    }
}
