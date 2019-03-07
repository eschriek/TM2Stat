using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace tm
{
    class MainLoop
    {

        bool mapChanged;
        bool roundStart, roundEnd;

        int previousTime;

        FixedSizedQueue<int> time_history;
        const int time_history_limit = 100; /* Size of aforementioned fixed size Q */
        const int finish_detection_threshold_low = 5; /* Time counts 'hangs' for a short while when finished, this acts as threshold */
        const int finish_detection_threshold_high = 50; /* Same principle, but beyond this threshold means the map is quit mid-round */

        const int REFRESH_RATE = 100;
        const string MAP_NOMAP = "Unnamed";

        const int TIME_INACTIVE = -1;

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

        /* Next two variables (online/offline) change mutually exclusive */
        private string _CurrentOnlineMapName;
        private string CurrentOnlineMapName
        {
            get
            {
                return this._CurrentOnlineMapName;
            }

            set
            {
                if (value != this._CurrentOnlineMapName)
                {
                    this._CurrentOnlineMapName = value;

                    if (value != MAP_NOMAP)
                    {
                        Console.WriteLine("Switching map to (online) : " + value);
                        mapChanged = true;

                        this.CurrentMapName = value;
                    }
                }
            }
        }

        private string _CurrentOfflineMapName;
        private string CurrentOfflineMapName
        {
            get
            {
                return this._CurrentOfflineMapName;
            }

            set
            {
                if (value != this._CurrentOfflineMapName)
                {
                    this._CurrentOfflineMapName = value;

                    if (value != MAP_NOMAP)
                    {
                        Console.WriteLine("Switching map to (offline) : " + value);
                        mapChanged = true;

                        this.CurrentMapName = value;
                    }
                }
            }
        }

        private string CurrentMapName;
        private Timer InfoTimer;
        private const int INFO_TIMER_INTERVAL = 10000;

        Hack hack;
        Player player;
        Map currentMap;

        public MainLoop(Hack h, Player p)
        {
            this.hack = h;
            this.player = p;

            time_history = new FixedSizedQueue<int>(time_history_limit);
            InfoTimer = new Timer(INFO_TIMER_INTERVAL);
            InfoTimer.Elapsed += InfoTimer_Elapsed;

            roundStart = false;
            roundEnd = false;
            previousTime = TIME_INACTIVE;

            CurrentMapName = MAP_NOMAP;
            CurrentOfflineMapName = MAP_NOMAP;
            CurrentOnlineMapName = MAP_NOMAP;

            mapChanged = false;

            state = State.RESET;
        }

        private void InfoTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (CurrentMapName == MAP_NOMAP)
                return;

            int? bestTime = (from Time t in currentMap.Times
                             where t.Validated
                             select (int?)t.Value).Min();

            var str = string.Format("(Session) {0} -> Best time : {1}, {2} minutes effective playtime, {3} finishes and {4} crashes",
                                currentMap.Name,
                                ((bestTime == null) ? "N/A" : Utils.FormatTimeStamp(bestTime.Value)),
                                (currentMap.EffectivePlayTime / (1000 * 60)),
                                currentMap.Times.Count(x => x.Validated),
                                currentMap.AmountCrashes);

            Console.WriteLine(str);
        }

        private int PossibleFinishTime(int currentTime)
        {
            if (currentMap == null)
                return TIME_INACTIVE;

            var finish_time_query = from x in time_history
                                    group x by x into g
                                    let count = g.Count()
                                    where count >= finish_detection_threshold_low &&
                                            g.Key != TIME_INACTIVE
                                    select new { Value = g.Key, Count = count };

            /* Few sanity checks... */
            if (finish_time_query.Count() > 0
                && previousTime > 0
                && currentTime != TIME_INACTIVE)
            {
                var finishTime = finish_time_query.First().Value;

                if (currentMap.Times.Count > 0)
                {
                    /* Note that this mechanism will fail when debugging */
                    var timeDiff = DateTime.Now - currentMap.Times.Last().When;

                    if (timeDiff.TotalMilliseconds > finishTime)
                        return finishTime;
                }
                else
                {
                    return finishTime;
                }

            }
            return TIME_INACTIVE;
        }

        /* At the time a possible finish time is detected it is impossible to detect whether its a false positive */
        private void ValidateLastFinishTime(int currentTime)
        {
            if (currentMap == null)
                return;

            var finish_time_query = from x in time_history
                                    group x by x into g
                                    let count = g.Count()
                                    where count >= finish_detection_threshold_high &&
                                            g.Key != TIME_INACTIVE
                                    select new { Value = g.Key, Count = count };

            if (finish_time_query.Count() > 0)
            {
                var invalidFinishTime = finish_time_query.First().Value;

                int removals = currentMap.Times.RemoveAll(x => x.Value == invalidFinishTime);
                currentMap.EffectivePlayTime -= (removals*invalidFinishTime);

                Debug.Assert(currentMap.EffectivePlayTime >= 0);

                //Debug.WriteLine("Deleted invalid finish time : " + invalidFinishTime);
            }

        }

        public void Loop()
        {
            InfoTimer.Start();

            while (true)
            {
                int time = hack.GetMapTime();

                /* Scan for a new map name if the timer starts running */
                if (time != TIME_INACTIVE)
                {
                    CurrentOnlineMapName = Utils.TrimMapNameString(hack.GetOnlineMapName());
                    CurrentOfflineMapName = Utils.TrimMapNameString(hack.GetOfflineMapName());
                }

                if (mapChanged)
                {
                    mapChanged = false;
                    currentMap = new Map() { Name = CurrentMapName };

                    player.PlayedMaps.Add(currentMap);
                }

                /* Currently playing a map */
                if (CurrentMapName != MAP_NOMAP)
                {
                    time_history.Enqueue(time);

                    var possibleFinishTime = PossibleFinishTime(time);

                    ValidateLastFinishTime(time);

                    /* This counts as a valid finish time, if the finish_detection_threshold_high is not exceeded */
                    if (possibleFinishTime != TIME_INACTIVE)
                    {
                        state = State.ROUND_FINISHED;

                        var finishTime = possibleFinishTime;

                        currentMap.EffectivePlayTime += finishTime;
                        currentMap.Times.Add(new Time() { Value = finishTime, When = DateTime.Now });
                    }
                    else
                    {
                        /* All other states */
                        if (time == TIME_INACTIVE && previousTime > 0)
                        {
                            /* TODO, fix */
                            state = State.ROUND_CRASHED;

                            currentMap.EffectivePlayTime += previousTime;
                            currentMap.AmountCrashes++;
                        }
                        else if (time == TIME_INACTIVE && previousTime == TIME_INACTIVE)
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

                    /* Validate possible finish times, if they are not removed by finish_detection_threshold_high than they are
                     * considered valid */
                    foreach (Time t in currentMap.Times)
                    {
                        if (!t.Validated)
                        {
                            if ((DateTime.Now - t.When).TotalMilliseconds > (finish_detection_threshold_high * REFRESH_RATE))
                            {
                                t.Validated = true;

                                var str = string.Format("Finish : {0}, Mean : {1}, StdDev : {2}, F/C : {3}",
                                    Utils.FormatTimeStamp(t.Value),
                                    Utils.FormatTimeStamp((int)Math.Floor(currentMap.MeanTime())),
                                    Utils.FormatTimeStamp((int)Math.Floor(currentMap.StdDev())),
                                    currentMap.FCRatio());

                                Console.WriteLine(str);
                            }
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

                CheckMapAddresses();

                System.Threading.Thread.Sleep(REFRESH_RATE);
            }

            foreach (Map m in player.PlayedMaps)
            {
                var csv = new StringBuilder();

                var header = string.Format("{0},{1},{2}\n", m.Times.Count(), m.AmountCrashes, m.FCRatio());

                foreach (Time t in m.Times)
                {
                    var newLine = string.Format("{0},{1}\n", t.Value, t.When);
                    csv.Append(newLine);
                }

                File.WriteAllText(GetSafeFilename(string.Format("{0}_{1}.csv", m.Name, player.NickName)), csv.ToString());
            }
        }

        private void CheckMapAddresses()
        {
            var localMapAddress = hack.GetAddressByName(Hack.LOCAL_MAPNAME_ADDR_ID);

            if (localMapAddress == 0)
            {
                Boolean ret = hack.RecalculateAddress(Hack.LOCAL_MAPNAME_ADDR_ID);

                if (ret)
                {
                    CurrentOfflineMapName = Utils.TrimMapNameString(hack.GetOfflineMapName());
                }
            }

            var onlineMapAddress = hack.GetAddressByName(Hack.ONLINE_MAPNAME_ADDR_ID);

            if (onlineMapAddress == 0)
            {
                Boolean ret = hack.RecalculateAddress(Hack.ONLINE_MAPNAME_ADDR_ID);

                if (ret)
                {
                    CurrentOnlineMapName = Utils.TrimMapNameString(hack.GetOnlineMapName());
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
