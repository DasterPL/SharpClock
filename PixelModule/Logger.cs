using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpClock
{
    public static class Logger
    {
        static string file = "WebPage/System.log";
        static public void Clear()
        {
            File.Delete(file);
        }
        /// <summary>
        /// Write specified string to standart output and Log file
        /// </summary>
        /// <param name="args">Value to write, Use ConsoleColor before Value to change color</param>
        public static void Log(params object[] args)
        {
            string logString = "";
            foreach (var arg in args)
            {
                if (arg is ConsoleColor)
                    Console.ForegroundColor = (ConsoleColor)arg;
                else
                {
                    Console.Write(arg.ToString());
                    logString += arg.ToString();
                }
            }
            Console.ResetColor();
            Console.Write("\n");
            using (StreamWriter sw = File.AppendText("WebPage/System.log"))
            {
                sw.WriteLine(logString);
            }
        }
    }
}
