/* Copyright 2017 Scabbage

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
THE SOFTWARE.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SubtitleModifier
{

    /// <summary>
    /// Holds information about a timecode as well as conversion and parsing methods.
    /// Note that a Timecode struct doesnt hold information about the FPS, which
    /// affects how the actual time is calculated.
    /// </summary>
    struct Timecode
    {
        enum ConversionType {ROUND, FLOOR, CEILING}
        static ConversionType CONV_TYPE = ConversionType.ROUND;

        public int hh, mm, ss, ff;

        public Timecode(int hours, int minutes, int seconds, int frames)
        {
            hh = hours;
            mm = minutes;
            ss = seconds;
            ff = frames;
        }

        public Timecode(double absoluteTime, double framerate)
        {
            double remainingTime = absoluteTime;
            hh = (int)(remainingTime / 3600d);
            remainingTime -= hh * 3600;
            mm = (int)(remainingTime / 60d);
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
        public double GetAbsoluteTime(double framerate)
        {
            double result = 0d;
            result += 3600d * hh;
            result += 60d * mm;
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
        public Timecode FromToFramerate(double fps1, double fps2)
        {
            // Get the absolute time of this code in fps1:
            double abs = GetAbsoluteTime(fps1);
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
        public Timecode FromToFramerate(double fps1, double fps2, double offset)
        {
            // Get the absolute time of this code in fps1:
            double abs = GetAbsoluteTime(fps1);
            // Add the offset
            abs += offset;
            Timecode t2 = new Timecode(ConvertTime(abs, fps1, fps2), fps2);
            return t2;
        }

        public override string ToString()
        {
            return hh.ToString("D2") + ":" + mm.ToString("D2") + ":" + ss.ToString("D2") + ":" + ff.ToString("D2");
        }

        
        public static double ConvertTime(double abs, double fps1, double fps2)
        {
            return abs * fps1 / fps2;
        }
        
    }



    


    class Program
    {
        const string SP_SEPARATOR = "      ";
        const string TC_SEPARATOR = " ";
        const string FN_SEPARATOR = "   ";


        static void Main(string[] args)
        {
            //DoTesting();
            Console.WriteLine("Hit CTRL-C at any time to quit.");
            RunConverter();
            Console.ReadKey();
        }

        static void RunConverter()
        {
            string path = "";
            Console.WriteLine("Enter path of subtitle file:");
            // Get file path from user
            while (path == "")
            {
                path = Console.ReadLine();

                if (!File.Exists(path))
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        Console.WriteLine("\nThe specified file '" + path + "' doesn't exist.");
                        Console.WriteLine("Re-enter file path or CTRL-C to exit.");
                    }
                    path = "";
                }
            }
            string fileType = path.Substring(path.LastIndexOf("."));
            string fileName = path.Substring(0, path.LastIndexOf("."));
            Console.WriteLine("File type: " + fileType);
            string outputFile = fileName + "_converted" + fileType;
            Console.WriteLine("Output file: " + outputFile);

            // Warn user if an output file already exists
            if (File.Exists(outputFile))
            {
                Console.WriteLine("Warning: An output file named '" + outputFile + "' already exists.\nDo you want to overwrite?");
                string ans = Console.ReadLine().ToLower();
                if (ans != "y" && ans != "yes")
                {
                    return;
                }
            }
            List<string> headerText = new List<string>();
            List<string> subLines = new List<string>();
            string temp = "";

            Console.WriteLine("Reading input file...");
            // get subtitle file into a buffered text reader
            using (StreamReader reader = File.OpenText(path))
            {
                // get all the text before the actual subtitle lines start
                // timecodes are preceded by the "SP_NUMBER  START  END  FILE_NAME" line.
                while (!temp.ToUpper().StartsWith("SP_NUMBER"))
                {
                    // read all the header stuff (mostly human-readable information)
                    temp = reader.ReadLine();
                    //Console.WriteLine("HD: '" + temp + "'");
                    headerText.Add(temp);
                }

                do
                {
                    // Now the timecodes start
                    temp = reader.ReadLine();
                    if (!String.IsNullOrWhiteSpace(temp))
                    {
                        //Console.WriteLine("TC: '" + temp + "'");
                        subLines.Add(temp);
                    }
                } while (temp != null);
            }
            double fromFPS = 0f, toFPS = 0f, offset = 0d;
            double[] dArgs;
            Console.WriteLine("Enter the FPS of the source file.\nOptionally, add an offset, in seconds, here.\neg. '25 2.5' would be 25FPS with an offset of 2.5 seconds.");
            dArgs = GetDoubleArrFromUser(1);
            fromFPS = dArgs[0];
            if (dArgs.Length > 1)
            {
                offset = dArgs[1];
            }
            Console.WriteLine("Enter the FPS of the output file.");
            toFPS = GetDoubleFromUser();

            // Create the output file
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            temp = "";
            string[] line;
            Console.WriteLine("Writing to file " + outputFile + "...");
            using (StreamWriter writer = File.CreateText(outputFile))
            {
                // write the header information
                for (int ii = 0; ii < headerText.Count; ii++)
                {
                    writer.WriteLine(headerText[ii]);
                }


                for (int ii = 0; ii < subLines.Count; ii++)
                {
                    line = subLines[ii].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    writer.Write(line[0]);
                    writer.Write(SP_SEPARATOR);
                    // convert the start timecode to the new framerate
                    Timecode tc = TimecodeFromString(line[1]);
                    tc = tc.FromToFramerate(fromFPS, toFPS, offset);
                    writer.Write(tc);
                    writer.Write(TC_SEPARATOR);

                    // now convert the end timecode
                    tc = TimecodeFromString(line[2]);
                    tc = tc.FromToFramerate(fromFPS, toFPS, offset);
                    writer.Write(tc);
                    writer.Write(FN_SEPARATOR);

                    // now write the filename of this subtitle
                    writer.Write(line[3]);

                    // Don't write a newline if this is the last line of the file
                    if(ii < subLines.Count - 1)
                    {
                        writer.WriteLine();
                    }
                }
            }
            Console.WriteLine("Conversion complete.");
        }

        
        static double GetDoubleFromUser()
        {
            double res = 0;
            while (res <= 0)
            {
                try
                {
                    res = double.Parse(Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid input.");
                }
            }
            return res;
        }

        static double[] GetDoubleArrFromUser(int minLength)
        {
            double[] res = null;
            string[] input;
            while (res == null)
            {
                try
                {
                    input = Console.ReadLine().Split(' ');
                    if(input.Length < minLength)
                    {
                        throw new ArgumentException();
                    }
                    res = new double[input.Length];
                    // parse array
                    for(int ii = 0; ii < input.Length; ii++)
                    {
                        res[ii] = double.Parse(input[ii]);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid input.");
                    res = null;
                }
            }
            return res;
        }

        static void DoTesting()
        {
            Console.WriteLine("-----DEBUG-----");
            // timecode parsing/conversion testing


            Console.WriteLine("-----DEBUG-----");
            
        }

        
        static Timecode TimecodeFromString(string timecode)
        {
            Timecode result = new Timecode();
            // remove any whitespace
            timecode.Trim(' ', '	');
            timecode = Regex.Replace(timecode, " ", "");
            string[] components = timecode.Split(':');

            // parse all components of the timecode
            // This may throw a format exception, it's up to the calling method
            // to handle the invalid string.
            result.hh = int.Parse(components[0]);
            result.mm = int.Parse(components[1]);
            result.ss = int.Parse(components[2]);
            result.ff = int.Parse(components[3]);
            return result;
        }
    }
}
