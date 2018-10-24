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
using System.Text.RegularExpressions;

namespace SubtitleModifier
{
    enum Trit { TRUE, FALSE, UNKNOWN }
    class Program
    {
        const string OFLAG_START = "-s";
        const string OFLAG_ENDTC = "-e";
        const string OFLAG_SECOND = "-t";
        const string SP_SEPARATOR = "      ";
        const string TC_SEPARATOR = " ";
        const string FN_SEPARATOR = "   ";
        static char[] SPLIT_CHARS = { ' ', (char)9 };

        static void Main(string[] args)
        {
            //DoTesting();
            Console.WriteLine("Hit CTRL-C at any time to quit.");
            RunConverter();
        }

        static void RunConverter()
        {
            string path = "";
            Console.WriteLine("Enter path or directory of subtitle file:");
            bool isDirectory = false;
            // Get file path from user
            while (path == "")
            {
                path = Console.ReadLine();
                path = path.Replace("\"", "");
                isDirectory = Directory.Exists(path);
                if (!File.Exists(path) && !isDirectory)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        Console.WriteLine("\nThe specified file or directory '" + path + "' doesn't exist.");
                        Console.WriteLine("Re-enter file path or CTRL-C to exit.");
                    }
                    path = "";
                }
            }

            string dirPath = path;
            string[] files;
            Trit userOverwritePreference = Trit.UNKNOWN;
            if (isDirectory)
            {
                files = Directory.GetFiles(dirPath, "*.sst");
                Directory.CreateDirectory(dirPath + "Converted\\");
            }
            else
            {
                files = new string[] { path };
            }

            decimal fromFPS = 0m, toFPS = 0m, offset = 0m;

            foreach (string enumeratedFile in files)
            {
                path = enumeratedFile;
                Console.WriteLine("Converting file '" + enumeratedFile + "'.");
                string fileName = path.Substring(0, path.LastIndexOf("."));
                int lastSlashIdx = path.LastIndexOf("\\");
                lastSlashIdx = lastSlashIdx == -1 ? 0 : lastSlashIdx;
                string outputFile = dirPath + "\\Converted" + path.Substring(lastSlashIdx, path.Length - lastSlashIdx);

                //Console.WriteLine("Outputting file '" + outputFile + "'.");

                // Warn user if an output file already exists
                if (userOverwritePreference == Trit.UNKNOWN && File.Exists(outputFile))
                {
                    if (isDirectory)
                        Warn("Output file(s) already exist. \nDo you want to overwrite?");
                    else
                        Warn("An output file named '" + outputFile + "' already exists.\nDo you want to overwrite?");
                    string ans = Console.ReadLine().ToLower();
                    if (ans != "y" && ans != "yes")
                    {
                        userOverwritePreference = Trit.FALSE;
                    }
                    userOverwritePreference = Trit.TRUE;
                }

                // Skip the file if the user doesnt want to overwrite
                if (userOverwritePreference == Trit.TRUE && File.Exists(outputFile))
                {
                    continue;
                }

                List<string> headerText = new List<string>();
                List<string> subLines = new List<string>();

                Console.WriteLine("Reading input file...");
                ReadTimecodeFile(path, out headerText, out subLines);
                Console.WriteLine();
                string startString = subLines[0];
                string endString = subLines[subLines.Count - 1].Split(SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries)[1];

                Console.WriteLine("File read. Subtitle Count: " + subLines.Count + "\nStart: " + startString + "\nEnd: " + endString);

                decimal[] dArgs;

                if (fromFPS == 0)
                {
                    Console.WriteLine("Enter the FPS of the source file.\nOptionally, add a time offset here.");
                    Console.WriteLine("'25 -t 2.5' would be 25FPS with an offset of 2.5 seconds.");
                    Console.WriteLine("'30 -s hh:mm:ss:ff' would be 30FPS, starting at the given timecode.");
                    Console.WriteLine("'30 -e hh:mm:ss:ff' would be 30FPS, ending at the given timecode.");
                    int curLineIdx = 0;
                    while (!IsValidTimecodeLine(subLines[curLineIdx]))
                    {
                        curLineIdx++;
                    }
                    Timecode startTC = TimecodeFromString(subLines[curLineIdx].Split(SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries)[1]);
                    Timecode endTC = TimecodeFromString(endString);
                    GetFPSAndOffsetFromUser(startTC, endTC, out fromFPS, out offset);

                    Console.WriteLine("Enter the FPS of the output file(s).");
                    toFPS = GetDecimalFromUser();
                }

                // Create the output file
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                string[] line;
                Console.WriteLine("Writing to file " + outputFile + "...");
                using (StreamWriter writer = File.CreateText(outputFile))
                {
                    // write the header information
                    for (int ii = 0; ii < headerText.Count; ii++)
                    {
                        //DLog("Writing line " + headerText[ii]);
                        writer.WriteLine(headerText[ii]);
                    }

                    bool formatWarn = false;
                    for (int ii = 0; ii < subLines.Count; ii++)
                    {
                        try
                        {
                            //DLog("Parsing line '" + subLines[ii]+"'");

                            if (IsValidTimecodeLine(subLines[ii]))
                            {
                                line = subLines[ii].Split(SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries);
                                writer.Write(line[0].PadLeft(4, '0'));
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
                                if (ii < subLines.Count - 1)
                                {
                                    writer.WriteLine();
                                }
                            }
                            else
                            {
                                Warn("Line " + (headerText.Count + ii + 1) + " is an invalid time code data line:\n'" + subLines[ii] + "'");
                                if (!formatWarn)
                                {
                                    formatWarn = true;
                                    Console.WriteLine("Expected format [int] [hh:mm:ss:ff] [hh:mm:ss:ff] [file].");
                                }
                                //writer.WriteLine(subLines[ii]);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                Console.WriteLine("Conversion complete.");
            }
        }

        /// <summary>
        /// Gets the desired fps and offset from the user.
        /// Blocks until valid input is specified.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="fromFPS"></param>
        /// <param name="offset"></param>
        public static void GetFPSAndOffsetFromUser(Timecode start, Timecode end, out decimal fromFPS, out decimal offset)
        {
            offset = 0m;
            decimal fps = 0m;
            bool validInput = false;
            do
            {
                string rawInput = Console.ReadLine();
                if (rawInput == null || rawInput == "")
                {
                    continue;
                }
                string[] input = rawInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (input.Length == 1)
                {
                    // offset has not been set, just convert the fps
                    if (decimal.TryParse(input[0], out fps) && fps > 0m)
                    {
                        validInput = true;
                    }
                    else
                    {
                        Console.WriteLine("Invalid FPS. FPS must be a positive decimal number");
                    }
                }
                else if (input.Length == 3)
                {
                    // offset has been requested, parse the fps and calculate offset
                    if (decimal.TryParse(input[0], out fps) && fps > 0m)
                    {
                        if (CalculateOffset(start.GetAbsoluteTime(fps), end.GetAbsoluteTime(fps), fps, input[1], input[2], out offset))
                        {
                            validInput = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid FPS. FPS must be a positive decimal number");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input count. Valid input is in the format [fps] <[offset flag] [offset]>");
                }
            } while (!validInput);
            fromFPS = fps;
        }

        /// <summary>
        /// Returns true if the input was valid, assigns it to the out parameter if so.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="fps"></param>
        /// <param name="offsetFlag"></param>
        /// <param name="inOffset"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        static bool CalculateOffset(decimal start, decimal end, decimal fps, string offsetFlag, string inOffset, out decimal offset)
        {
            switch (offsetFlag)
            {
                case OFLAG_SECOND:
                    // offset is in seconds
                    return decimal.TryParse(inOffset, out offset);
                case OFLAG_START:
                    // find the time in seconds of the start timecode
                    try
                    {
                        Timecode t = TimecodeFromString(inOffset);
                        offset = t.GetAbsoluteTime(fps) - start;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Invalid timecode. Timecodes should be in the format hh:mm:ss:ff.");
                        offset = 0m;
                        return false;
                    }
                    return true;
                case OFLAG_ENDTC:
                    // find the time in seconds of the end timecode
                    try
                    {
                        Timecode t = TimecodeFromString(inOffset);
                        offset = t.GetAbsoluteTime(fps) - end;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Invalid timecode. Timecodes should be in the format hh:mm:ss:ff.");
                        offset = 0m;
                        return false;
                    }
                    return true;
            }
            offset = 0m;
            return false;
        }

        public static void ReadTimecodeFile(string path, out List<string> headerText, out List<string> subLines)
        {
            string current = "";
            headerText = new List<string>();
            subLines = new List<string>();

            using (StreamReader reader = File.OpenText(path))
            {
                // get all the text before the actual subtitle lines start
                // timecodes are preceded by the "SP_NUMBER  START  END  FILE_NAME" line.
                while (!current.ToUpper().StartsWith("SP_NUMBER"))
                {
                    // read all the header stuff (mostly human-readable information)
                    current = reader.ReadLine();
                    //Console.WriteLine("HD: '" + temp + "'");
                    headerText.Add(current);
                }

                do
                {
                    // Now the timecodes start
                    current = reader.ReadLine();
                    if (subLines.Count == 0 && !IsValidTimecodeLine(current))
                    {
                        headerText.Add(current);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(current))
                        {
                            subLines.Add(current);
                        }
                    }
                } while (current != null);
            }
        }

        static bool IsValidTimecodeLine(String line)
        {
            string[] words = line.Split(SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 4)
            {
                int sp;
                if (int.TryParse(words[0], out sp))
                {
                    try
                    {
                        TimecodeFromString(words[1]);
                        TimecodeFromString(words[2]);
                        return true;
                    }
                    catch (Exception e) { }
                }
            }

            return false;
        }

        static void Warn(String msg)
        {
            Console.WriteLine("[WARN] " + msg);
        }

        static void DLog(String msg)
        {
            Console.WriteLine("[Debug] " + msg);
        }

        static decimal GetDecimalFromUser()
        {
            decimal res = 0;
            while (res <= 0)
            {
                try
                {
                    res = decimal.Parse(Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid input. Input should be a decimal number.");
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
