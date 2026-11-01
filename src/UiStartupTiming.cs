#if WEB_UI
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
namespace NetProbe {
    static class UiStartupTiming {
        static readonly Stopwatch timer=Stopwatch.StartNew();
        static readonly StringBuilder stages=new StringBuilder();
        static bool finished;
        public static void Mark(string stage){if(finished)return;stages.AppendLine(timer.ElapsedMilliseconds+" ms  "+stage);}
        public static void Finish(){if(finished)return;Mark("interface DOM ready");finished=true;try{File.WriteAllText(Path.Combine(Settings.Sub("runtime"),"interface-startup.txt"),stages.ToString());}catch{}}
    }
}
#endif
