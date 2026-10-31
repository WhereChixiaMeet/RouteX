using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using NetProbe;
class MigrationTests
{
    static int checks;static string root,payload;static JavaScriptSerializer json=new JavaScriptSerializer();
    static object Call(string method,params object[] args){try{return typeof(NameMigration).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException ex){throw ex.InnerException;}}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    static NameMigration.Plan Plan(string mode,bool target=true)
    {
        string dir=Path.Combine(root,"case "+Guid.NewGuid().ToString("N")+" тест");Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir,"config","runtime"));Directory.CreateDirectory(Path.Combine(dir,"config","themes","chisa"));
        File.WriteAllText(Path.Combine(dir,"config","settings.ini"),"BypassRu=1\r\nTunStack=gvisor");
        File.WriteAllText(Path.Combine(dir,"config","themes","chisa","custom.png"),"THEME SENTINEL");
        File.WriteAllText(Path.Combine(dir,"config","runtime","handoff.ini"),"state=Vpn");
        File.WriteAllText(Path.Combine(dir,"NetProbe.exe"),"OLD LEGACY");if(target)File.WriteAllText(Path.Combine(dir,"RouteX.exe"),"OLD CANONICAL");
        File.WriteAllText(Path.Combine(dir,"mode.txt"),mode);
        var p=new NameMigration.Plan{Root=dir,Id=Guid.NewGuid().ToString("N"),Token=Guid.NewGuid().ToString("N"),Legacy=Path.Combine(dir,"NetProbe.exe"),Hash=Hash(payload),ParentPid=0,RestartOnFailure=false,ReadyTimeoutMs=800};
        string work=Work(p);Directory.CreateDirectory(work);File.Copy(payload,Path.Combine(work,"payload.exe"));File.WriteAllText(Path.Combine(work,"plan.json"),json.Serialize(p));return p;
    }
    static string Work(NameMigration.Plan p){return Path.Combine(p.Root,"config","runtime","name-migration",p.Id);}
    static void StopChild(NameMigration.Plan p){string file=Path.Combine(p.Root,"child.pid");if(!File.Exists(file))return;try{using(var child=Process.GetProcessById(int.Parse(File.ReadAllText(file)))){if(!child.HasExited){child.Kill();child.WaitForExit(3000);}}}catch(ArgumentException){}}
    static string TaskXml(string exe){var doc=new XmlDocument();doc.LoadXml("<Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><Actions><Exec><Command/></Exec></Actions></Task>");doc.SelectSingleNode("//*[local-name()='Command']").InnerText=exe;return doc.OuterXml;}
    [STAThread] static int Main(string[] args)
    {
        try
        {
            root=Path.GetFullPath(args[0]);payload=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Logger.ToFile=false;
            foreach(bool prior in new[]{false,true})
            {
                var p=Plan("ready",prior);try{Check((int)Call("RunPlan",p)==0,"successful transaction with prior target="+prior);Check(Hash(NameMigration.Canonical(p.Root))==p.Hash,"canonical executable has the staged digest");Check(!File.Exists(p.Legacy),"old launcher removed only after matching readiness token");Check(File.Exists(Path.Combine(Work(p),"previous-launcher.bak")),"old launcher backup retained");Check(File.ReadAllText(Path.Combine(p.Root,"config","settings.ini")).Contains("BypassRu=1"),"configuration retained");if(prior)Check(File.ReadAllText(Path.Combine(Work(p),"previous-target.bak"))=="OLD CANONICAL","preexisting RouteX version backed up");}finally{StopChild(p);}
            }
            foreach(string mode in new[]{"fail","hang","spoof","mutate-fail"})
            {
                var p=Plan(mode);try{Check((int)Call("RunPlan",p)==1,"rollback on "+mode);Check(File.ReadAllText(NameMigration.Canonical(p.Root))=="OLD CANONICAL","prior RouteX restored after "+mode);Check(File.ReadAllText(p.Legacy)=="OLD LEGACY","legacy launcher retained after "+mode);Check(File.ReadAllText(Path.Combine(p.Root,"config","settings.ini")).Contains("BypassRu=1"),"settings restored after "+mode);Check(File.ReadAllText(Path.Combine(p.Root,"config","runtime","handoff.ini"))=="state=Vpn","handoff retained/restored after "+mode);Check(File.ReadAllText(Path.Combine(p.Root,"config","themes","chisa","custom.png"))=="THEME SENTINEL","theme retained after "+mode);}finally{StopChild(p);}
            }
            var fresh=Plan("fail",false);Check((int)Call("RunPlan",fresh)==1&&!File.Exists(NameMigration.Canonical(fresh.Root)),"failed first migration leaves no broken canonical EXE");
            var keep=Plan("keep");try{Check((int)Call("RunPlan",keep)==0&&File.Exists(keep.Legacy),"incomplete Windows integration keeps the usable legacy launcher");}finally{StopChild(keep);}
            var corrupt=Plan("ready");File.AppendAllText(Path.Combine(Work(corrupt),"payload.exe"),"CORRUPT");Check((int)Call("RunPlan",corrupt)==1&&File.ReadAllText(NameMigration.Canonical(corrupt.Root))=="OLD CANONICAL","corrupt payload rejected before replacement");
            var locked=Plan("ready");using(var stream=new FileStream(NameMigration.Canonical(locked.Root),FileMode.Open,FileAccess.Read,FileShare.Read)){Check((int)Call("RunPlan",locked)==1,"locked target fails safely");}Check(File.ReadAllText(NameMigration.Canonical(locked.Root))=="OLD CANONICAL","locked target remains intact");
            var outside=Plan("ready");outside.Legacy=Path.Combine(root,"outside.exe");bool rejected=false;try{Call("Validate",outside);}catch(InvalidDataException){rejected=true;}Check(rejected,"cross-directory legacy path rejected");
            string download=Path.Combine(root,"empty download folder");Directory.CreateDirectory(download);
            Check((string)Call("ChooseRoot",download,new[]{corrupt.Root})==corrupt.Root,"download outside the installation locates the existing config folder");
            Check((string)Call("ChooseRoot",corrupt.Root,new[]{keep.Root})==corrupt.Root,"explicit installation folder takes priority over another install");
            Check((string)Call("ChooseRoot",download,new string[0])==download,"fresh install remains in the downloaded folder");
            rejected=false;try{Call("ChooseRoot",download,new[]{corrupt.Root,keep.Root});}catch(IOException){rejected=true;}Check(rejected,"ambiguous installations are not merged automatically");
            var traversal=Plan("ready");traversal.Id="..\\outside";rejected=false;try{Call("Validate",traversal);}catch(InvalidDataException){rejected=true;}Check(rejected,"transaction directory traversal rejected");
            string integration=Path.Combine(root,"Windows integration "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(integration);
            Check((bool)Call("IsOurTask",TaskXml(Path.Combine(integration,"NetProbe.exe")),integration),"legacy autorun target recognized by exact path");
            Check(!(bool)Call("IsOurTask",TaskXml(Path.Combine(root,"other","NetProbe.exe")),integration),"another installation's task is not owned");
            Check(!(bool)Call("IsOurTask","<invalid>",integration),"malformed task XML is not owned");
            var commandsField=typeof(NameMigration).GetField("TaskCommand",BindingFlags.Static|BindingFlags.NonPublic);object saved=commandsField.GetValue(null);
            var tasks=new Dictionary<string,string>();var names=(string[])Call("TaskNames",integration);string old=names.First(n=>n.StartsWith("NetProbe "));
            tasks[old]=TaskXml(Path.Combine(integration,"NetProbe.exe"));
            Func<string,string,int,Encoding,Proc.Res> runner=(exe,command,timeout,enc)=>{
                string name=Regex.Match(command,"/TN \\\"([^\\\"]+)\\\"").Groups[1].Value;
                if(command.StartsWith("/Create")){string file=Regex.Match(command,"/XML \\\"([^\\\"]+)\\\"").Groups[1].Value;tasks[name]=File.ReadAllText(file);return new Proc.Res{Exit=0};}
                if(command.StartsWith("/Delete")){tasks.Remove(name);return new Proc.Res{Exit=0};}
                return tasks.ContainsKey(name)?new Proc.Res{Exit=0,Out=tasks[name]}:new Proc.Res{Exit=1};
            };
            try
            {
                commandsField.SetValue(null,runner);
                Check(NameMigration.ConfigureAutorun(integration,true),"autorun migration succeeds with a mocked scheduler");
                Check(tasks.Count==1&&tasks.ContainsKey(NameMigration.AutorunTaskName(integration)),"canonical task replaces the owned legacy task");
                string xml=tasks.Values.First();var doc=new XmlDocument();doc.LoadXml(xml);
                Check(doc.SelectSingleNode("//*[local-name()='Command']").InnerText==NameMigration.Canonical(integration),"task command points at RouteX.exe");
                Check(doc.SelectSingleNode("//*[local-name()='WorkingDirectory']").InnerText==integration,"task working directory preserves the installation folder");
                var serviceType=Type.GetTypeFromProgID("Schedule.Service");object service=Activator.CreateInstance(serviceType);try{serviceType.InvokeMember("Connect",BindingFlags.InvokeMethod,null,service,null);object definition=serviceType.InvokeMember("NewTask",BindingFlags.InvokeMethod,null,service,new object[]{0});try{definition.GetType().InvokeMember("XmlText",BindingFlags.SetProperty,null,definition,new object[]{xml});Check(true,"Windows Task Scheduler accepts the generated XML without registering a task");}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(definition);}}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(service);}
                tasks[old]=TaskXml(Path.Combine(root,"another installation","NetProbe.exe"));Check(!NameMigration.ConfigureAutorun(integration,true)&&tasks.ContainsKey(old),"unrelated task is retained and legacy cleanup is withheld");
                Check(!NameMigration.ConfigureAutorun(integration,false)&&tasks.ContainsKey(old)&&!tasks.ContainsKey(NameMigration.AutorunTaskName(integration)),"disabling autorun deletes only owned tasks");
                commandsField.SetValue(null,new Func<string,string,int,Encoding,Proc.Res>((e,c,t,en)=>new Proc.Res{Exit=1}));Check(!NameMigration.ConfigureAutorun(integration,true),"scheduler registration failure is reported");
            }finally{commandsField.SetValue(null,saved);}
            var shellType=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(shellType);
            try
            {
                string link=Path.Combine(integration,"RouteX test.lnk"),legacy=Path.Combine(integration,"NetProbe.exe"),backup=Path.Combine(integration,"backup","old.lnk");
                object sc=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});var st=sc.GetType();st.InvokeMember("TargetPath",BindingFlags.SetProperty,null,sc,new object[]{legacy});st.InvokeMember("Arguments",BindingFlags.SetProperty,null,sc,new object[]{"--minimized"});st.InvokeMember("IconLocation",BindingFlags.SetProperty,null,sc,new object[]{legacy+",0"});st.InvokeMember("Save",BindingFlags.InvokeMethod,null,sc,null);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sc);
                Check((bool)Call("RewriteShortcut",shellType,shell,link,legacy,NameMigration.Canonical(integration),backup),"real Windows shortcut migrated in the isolated test folder");
                sc=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});try{st=sc.GetType();Check((string)st.InvokeMember("TargetPath",BindingFlags.GetProperty,null,sc,null)==NameMigration.Canonical(integration),"shortcut target updated");Check((string)st.InvokeMember("Arguments",BindingFlags.GetProperty,null,sc,null)=="--minimized","shortcut arguments retained");Check(((string)st.InvokeMember("IconLocation",BindingFlags.GetProperty,null,sc,null)).Contains("RouteX.exe"),"shortcut icon updated");Check(File.Exists(backup),"original shortcut backed up");}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sc);}
            }finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
            Console.WriteLine("TOTAL "+checks+" migration checks; no actual autorun tasks registered or user shortcuts modified.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
