using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
class MigrationChild
{
    static int Main(string[] args)
    {
        int index=Array.IndexOf(args,"--migration-plan"); if(index<0)return 8;
        string file=args[index+1];var p=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(file));
        string root=(string)p["Root"],mode=File.ReadAllText(Path.Combine(root,"mode.txt"));
        File.WriteAllText(Path.Combine(root,"child.pid"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
        if(mode=="fail")return 7;
        if(mode=="mutate-fail"){File.WriteAllText(Path.Combine(root,"config","settings.ini"),"DAMAGED");File.Delete(Path.Combine(root,"config","runtime","handoff.ini"));return 7;}
        if(mode=="ready"||mode=="keep")File.WriteAllText(Path.Combine(Path.GetDirectoryName(file),"ready.txt"),(string)p["Token"]+Environment.NewLine+(mode=="ready"?"clean":"keep"));
        if(mode=="spoof")File.WriteAllText(Path.Combine(Path.GetDirectoryName(file),"ready.txt"),"wrong-token\nclean");
        Thread.Sleep(mode=="ready"||mode=="keep"?1200:5000);return 0;
    }
}
