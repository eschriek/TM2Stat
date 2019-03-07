using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace tm
{
    class Utils
    {
        public static string TrimMapNameString(string s)
        {
            try
            {
                int delimeter=s.IndexOf("'");

                var ret = s.Substring(0, delimeter);

                return DecodeAndPrintTrackManiaColorString(ret);
            }
            catch (Exception e)
            { }

            return "";
        }

        public static string FormatTimeStamp(int time)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(time);
            return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Minutes, t.Seconds, t.Milliseconds);
        }

        public static String DecodeAndPrintTrackManiaColorString(string s)
        {

            Regex r = new Regex(@"\$([0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]|[a-z])");
            var ret = r.Replace(s, "");

            return ret;
        }
    }
}
