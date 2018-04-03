using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubtitleModifier
{
    /// <summary>
    /// Holds information about a timecode as well as conversion and parsing methods.
    /// Note that a Timecode struct doesnt hold information about the FPS, which
    /// affects how the actual time is calculated.
    /// </summary>
    public struct Timecode
    {
        private enum ConversionType { ROUND, FLOOR, CEILING }
        static ConversionType CONV_TYPE = ConversionType.ROUND;

        public int hh, mm, ss, ff;

        public Timecode(int hours, int minutes, int seconds, int frames)
        {
            hh = hours;
            mm = minutes;
            ss = seconds;
            ff = frames;
        }

        public Timecode(decimal absoluteTime, decimal framerate)
        {
            decimal remainingTime = absoluteTime;
            hh = (int)(remainingTime / 3600m);
            remainingTime -= hh * 3600;
            mm = (int)(remainingTime / 60m);
            remainingTime -= mm * 60;
            ss = (int)remainingTime;
            remainingTime -= ss;
            switch (CONV_TYPE)
            {
                case ConversionType.FLOOR:
                    ff = (int)Math.Floor(remainingTime * framerate);
                    break;
                case ConversionType.CEILING:
                    ff = (int)Math.Ceiling(remainingTime * framerate);
                    break;
                case ConversionType.ROUND:
                    ff = (int)Math.Round(remainingTime * framerate);
                    break;
                default:
                    ff = (int)(remainingTime * framerate);
                    break;
            }
        }

        /// <summary>
        /// Gives the value of this timecode in absolute time, assuming the timecode is in the given framerate.
        /// </summary>
        /// <returns></returns>
        public decimal GetAbsoluteTime(decimal framerate)
        {
            decimal result = 0m;
            result += 3600m * hh;
            result += 60m * mm;
            result += ss;
            result += ff / framerate;
            return result;
        }

        /// <summary>
        /// Returns a timecode (in fps2) equivalent to this timecode (in fps1).
        /// </summary>
        /// <param name="fps1"></param>
        /// <param name="fps2"></param>
        /// <returns></returns>
        public Timecode FromToFramerate(decimal fps1, decimal fps2)
        {
            // Get the absolute time of this code in fps1:
            decimal abs = GetAbsoluteTime(fps1);
            Timecode t2 = new Timecode(ConvertTime(abs, fps1, fps2), fps2);
            return t2;
        }

        /// <summary>
        /// Returns a timecode (in fps2) equivalent to this timecode (in fps1) with an offset, in seconds.
        /// </summary>
        /// <param name="fps1"></param>
        /// <param name="fps2"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public Timecode FromToFramerate(decimal fps1, decimal fps2, decimal offset)
        {
            // Get the absolute time of this code in fps1:
            decimal abs = GetAbsoluteTime(fps1);
            // Add the offset
            abs += offset;
            Timecode t2 = new Timecode(ConvertTime(abs, fps1, fps2), fps2);
            return t2;
        }

        public override string ToString()
        {
            return hh.ToString("D2") + ":" + mm.ToString("D2") + ":" + ss.ToString("D2") + ":" + ff.ToString("D2");
        }


        public static decimal ConvertTime(decimal abs, decimal fps1, decimal fps2)
        {
            return abs * fps1 / fps2;
        }

    }
}
