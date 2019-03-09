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

        Boolean mapChanged;
        int previousTime;
        int mapEntryTime;

        FixedSizedQueue<int> time_history;
        const int time_history_limit = 150; /* Size of aforementioned fixed size Q */
        const int finish_detection_threshold_low = 5; /* Time counts 'hangs' for a short while when finished, this acts as threshold */
        const int finish_detection_threshold_high = 100; /* Same principle, but beyond this threshold means the map is quit mid-round */

        const int REFRESH_RATE = 100;
        public const string MAP_NOMAP = "Unnamed";

        const int TIME_INACTIVE = -1;

        private string _CurrentMapName;
        private string CurrentMapName
        {
            get
            {
                return this._CurrentMapName;
            }

            set
            {
                if (value != this._CurrentMapName)
                {
                    this._CurrentMapName = value;

                    if (value != MAP_NOMAP)
                    {
                        Console.WriteLine("Switching map to : " + value);
                        mapChanged = true;

                        this.CurrentMapName = value;
                    }
                }
            }
        }

        private Timer InfoTimer;
        private const int INFO_TIMER_INTERVAL = 3 * 60000;

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

            previousTime = TIME_INACTIVE;
            mapEntryTime = TIME_INACTIVE;
            CurrentMapName = MAP_NOMAP;

            mapChanged = false;
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
                                    where g.Count() >= finish_detection_threshold_low &&
                                            g.Key != TIME_INACTIVE
                                    select new { Value = g.Key, Count = g.Count() };

            /* Few sanity checks... */
            if (finish_time_query.Count() > 0
                && previousTime > 0
                && currentTime > 0)
            {
                var finishTime = finish_time_query.First().Value;

                Debug.Assert(finishTime > 0);

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
                                    where (g.Count() >= finish_detection_threshold_high &&
                                            g.Key != TIME_INACTIVE) ||
                                            (g.Key == mapEntryTime && g.Key != TIME_INACTIVE)
                                    select new { Value = g.Key, Count = g.Count() };

            if (finish_time_query.Count() > 0)
            {
                var invalidFinishTime = finish_time_query.First().Value;

                int removals = currentMap.Times.RemoveAll(x => x.Value == invalidFinishTime);
                currentMap.EffectivePlayTime -= (removals * invalidFinishTime);

                Debug.Assert(currentMap.EffectivePlayTime >= 0);

                Console.WriteLine("Deleted invalid finish time : " + invalidFinishTime);
            }

        }

        public void Loop()
        {
            Console.WriteLine(string.Format("Periodic session information every {0} minutes.", INFO_TIMER_INTERVAL / 60000));

            InfoTimer.Start();

            while (true)
            {
                while (hack.GetAddressByName(Hack.TIME_ADDR_ID) == 0)
                {
                    hack.RecalculateAddress(Hack.TIME_ADDR_ID);
                }

                int time = hack.GetMapTime();

                CurrentMapName = Utils.TrimMapNameString(hack.GetMapName());

                if (mapChanged)
                {
                    mapChanged = false;
                    mapEntryTime = time;

                    currentMap = new Map() { Name = CurrentMapName };

                    player.PlayedMaps.Add(currentMap);

                    time_history.Clear();
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
                        var finishTime = possibleFinishTime;

                        currentMap.EffectivePlayTime += finishTime;
                        currentMap.Times.Add(new Time() { Value = finishTime, When = DateTime.Now });
                    }
                    else
                    {
                        /* All other states */
                        if (time == TIME_INACTIVE && previousTime > 0)
                        {
                            Debug.Assert(time_history.Last() == TIME_INACTIVE);

                            var prev = time_history.ElementAt(time_history.Count - 2);
                            var prev_2 = time_history.ElementAt(time_history.Count - 3);

                            if (prev != prev_2)
                            {
                                currentMap.EffectivePlayTime += previousTime;
                                currentMap.AmountCrashes++;
                            }
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

                CheckMapAddress();

                System.Threading.Thread.Sleep(REFRESH_RATE);
            }

            WriteCSV();
        }

        public void WriteCSV()
        {
            foreach (Map m in player.PlayedMaps)
            {
                var csv = new StringBuilder();

                var header = string.Format("{0},{1},{2}\n", m.EffectivePlayTime, m.Times.Count(), m.AmountCrashes);
                csv.AppendLine(header);

                foreach (Time t in m.Times)
                {
                    if (!t.Validated)
                        continue;

                    var newLine = string.Format("{0},{1}\n", t.Value, t.When);
                    csv.Append(newLine);
                }

                File.WriteAllText(GetSafeFilename(string.Format("{0}_{1}_{2}.csv", m.Name, player.NickName, DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss"))),
                    csv.ToString());
            }
        }

        private void CheckMapAddress()
        {
            var mapAddress = hack.GetAddressByName(Hack.MAPNAME_ADDR_ID);

            if (mapAddress == 0)
            {
                Boolean ret = hack.RecalculateAddress(Hack.MAPNAME_ADDR_ID);

                if (ret)
                {
                    CurrentMapName = Utils.TrimMapNameString(hack.GetMapName());
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

    internal static class ConcurrentQueueExtensions
    {
        public static void Clear<T>(this ConcurrentQueue<T> queue)
        {
            T item;
            while (queue.TryDequeue(out item))
            {
                // do nothing
            }
        }
    }
}
