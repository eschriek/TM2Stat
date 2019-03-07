using System;
using System.Collections.Generic;
using System.Linq;

namespace tm
{
    public class Map
    {
        public Map()
        {
            Times = new List<Time>();
        }

        public int Id { get; set; }
        public string Name { get; set; }

        public int AmountCrashes { get; set; }
        public List<Time> Times { get; set; }

        /* Crash to finish ratio */
        public float FCRatio()
        {
            return (float)Times.Count / AmountCrashes;
        }

        /* Average Time */
        public float MeanTime()
        {
            var rawTimes = Times.Select(x => (float)x.Value);

            return rawTimes.Average();
        }

        /* Std. Dev. of time */
        public float StdDev()
        {
            var rawTimes = Times.Select(x => (float)x.Value);
            var average = rawTimes.Average();

            /* Sum of squares */
            var sumOfSquares = rawTimes.Select(x => (x - average) * (x - average)).Sum();

            return (float)Math.Sqrt(sumOfSquares / rawTimes.Count());
        }
    }

    public class Time
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public DateTime When { get; set; }
    }
}
