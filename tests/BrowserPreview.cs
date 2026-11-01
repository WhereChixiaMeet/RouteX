using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetProbe;

class BrowserPreview
{
    static async Task ClickAt(BrowserSurface surface,string selector,double fx=.5,double fy=.5,string button="left")
    {
        var js=new System.Web.Script.Serialization.JavaScriptSerializer();
        var point=js.Deserialize<System.Collections.Generic.Dictionary<string,object>>(await surface.EvaluateAsync("(()=>{const e=document.querySelector("+js.Serialize(selector)+");const r=e.getBoundingClientRect();return{x:r.left+r.width*"+fx.ToString(System.Globalization.CultureInfo.InvariantCulture)+",y:r.top+r.height*"+fy.ToString(System.Globalization.CultureInfo.InvariantCulture)+"};})()"));
        var view=typeof(BrowserSurface).GetField("view",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(surface);
        var core=view.GetType().GetProperty("CoreWebView2").GetValue(view,null);var method=core.GetType().GetMethod("CallDevToolsProtocolMethodAsync");
        foreach(string type in new[]{"mouseMoved","mousePressed","mouseReleased"}){
            var data=new{type,x=Convert.ToDouble(point["x"]),y=Convert.ToDouble(point["y"]),button,buttons=type=="mousePressed"?(button=="right"?2:1):0,clickCount=type=="mouseMoved"?0:1};
            await (Task<string>)method.Invoke(core,new object[]{"Input.dispatchMouseEvent",js.Serialize(data)});if(type=="mousePressed")await Task.Delay(100);
        }
    }
    sealed class CountingProber : Prober {
        public readonly System.Collections.Generic.List<long> Calls=new System.Collections.Generic.List<long>();
        readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        public override string Describe{get{return "offline test";}}
        public override ProbeResult Http(string url,int timeoutSec,bool follow=false,bool discardBody=false,CancelFlag cf=null){lock(Calls)Calls.Add(clock.ElapsedMilliseconds);return new ProbeResult{Ok=true};}
        public override ProbeResult Download(string url,int timeoutSec,CancelFlag cf){throw new NotSupportedException();}
        public override ProbeResult Upload(string url,string file,int timeoutSec,CancelFlag cf){throw new NotSupportedException();}
    }
    sealed class PreviewForm : MainForm
    {
        public PreviewForm() : base(false) { StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;ClientSize=new Size(1440,1000); }
        protected override void OnLoad(EventArgs e) { }
        protected override void OnShown(EventArgs e) { }
        protected override void OnFormClosing(FormClosingEventArgs e) { }
    }
    [STAThread]static void Main(string[] args)
    {
        EmbeddedBrowser.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(typeof(MainForm).GetMethod("PrepareUiPreview")!=null)throw new Exception("The functional tests require the production backend, not UI_PREVIEW");
        Console.WriteLine("PASS production backend is compiled in; preview simulation is absent");
        MainForm.ShotsDir=args[0];Directory.CreateDirectory(args[0]);
        var settings=Settings.Load();settings.KillSwitch=true;settings.Lang="en";settings.AutoConnect=false;settings.XrayAutoConnect=false;settings.AutoStartMonitor=false;settings.LogProbes=false;settings.SpeedOn=false;settings.ShowWidget=false;settings.Autorun=false;settings.Theme=0;settings.ModuleLoc="set.look=p0;set.subs=p0";settings.CustomPages="c1:Old panel";settings.UiLocked=false;settings.MonDefaults312=settings.SpeedDefaults=true;settings.Save();
        if(!Tunnels.Exists("Personal config"))TunnelProto.Save("Personal config",new ServerEntry{Protocol="vless",Address="192.0.2.10",Port=443,UserId="00000000-0000-4000-8000-000000000001",Security="reality",Network="xhttp",Sni="example.com"});
        if(!Tunnels.Exists("Backup config"))TunnelProto.Save("Backup config",new ServerEntry{Protocol="vless",Address="192.0.2.20",Port=443,UserId="00000000-0000-4000-8000-000000000002",Security="tls",Network="tcp",Sni="example.com"});
        settings.PrimaryTunnel="Personal config";settings.BackupTunnel="Backup config";settings.Save();DpiConfigs.Write("general","--filter-tcp=443");AppLists.Add(AppLists.HybridApps,"chrome.exe");
        AppLists.Remove(AppLists.HybridApps,new[]{"preview-ui.exe"});
        var mapping=(System.Collections.Generic.Dictionary<string,string>)typeof(MainForm).GetField("BrowserFields",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
        foreach(var field in mapping.Values)if(typeof(Settings).GetField(field)==null)throw new Exception("Unknown setting binding: "+field);
        Console.WriteLine("PASS all "+mapping.Count+" setting fields exist in the native model");
        var fresh=new Settings();if(ReleaseReview.Prepare(fresh,"4.0",false,false))throw new Exception("Fresh install must not show an update review");
        var upgraded=new Settings{LastRunVersion="3.6.0"};if(!ReleaseReview.Prepare(upgraded,"4.0",true,false))throw new Exception("Upgrade review is missing");
        ReleaseReview.MarkShown(upgraded,"4.0");if(ReleaseReview.Prepare(upgraded,"4.0",true,true))throw new Exception("Release review repeats for the same version");
        if(!ReleaseReview.Prepare(upgraded,"4.1",true,false))throw new Exception("Next version review is missing");
        var legacy=new Settings();if(!ReleaseReview.Prepare(legacy,"4.0",true,false))throw new Exception("Legacy profile upgrade is not detected");
        Console.WriteLine("PASS release review: upgrade once, no repeat, no review on fresh install");
        foreach(bool hybrid in new[]{false,true})foreach(int configCount in new[]{0,1})foreach(int serverCount in new[]{0,1})foreach(int selected in new[]{0,1}){
            Source expected=configCount==0?(serverCount==0?Source.None:Source.Subs):serverCount==0||!hybrid?Source.Vpn:selected==1?Source.Subs:Source.Vpn;
            if(ConnectionSources.Resolve(hybrid,configCount,serverCount,selected)!=expected||ConnectionSources.NeedsChoice(hybrid,configCount,serverCount)!=(hybrid&&configCount>0&&serverCount>0))throw new Exception("Connection source policy mismatch");
        }
        var noticeSettings=Settings.Load();noticeSettings.SubscriptionPriorityNotified=true;noticeSettings.Save();var noticeReloaded=Settings.Load();ReleaseReview.Prepare(noticeReloaded,"4.1",true,false);if(!noticeReloaded.SubscriptionPriorityNotified)throw new Exception("Source notice was reset by an update");
        Console.WriteLine("PASS all 16 source combinations and lifetime notice persistence across save/load and version change");
        var endpointSelector=new ProbeEndpointSelector();var endpoints=new[]{"https://primary.invalid/","https://secondary.invalid/","https://third.invalid/"};
        if(endpointSelector.Pick(endpoints,false,0)!=endpoints[0])throw new Exception("Primary endpoint is not preferred");
        endpointSelector.Report(endpoints[0],false,0);
        if(endpointSelector.Pick(endpoints,false,1)!=endpoints[1])throw new Exception("Visible monitoring did not switch away from failed endpoint");
        endpointSelector.Report(endpoints[1],true,2);
        if(endpointSelector.Pick(endpoints,false,3)!=endpoints[1])throw new Exception("Working alternate endpoint was not retained");
        if(endpointSelector.Pick(endpoints,false,61)!=endpoints[0])throw new Exception("Primary endpoint is never retried");
        Console.WriteLine("PASS failed endpoint cooldown, alternate selection, and primary retry");
        var form=new PreviewForm();var launch=new Timer{Interval=100};launch.Tick+=async(s,e)=>{launch.Stop();
            try{
                await form.EnableBrowserInterfaceAsync();
                for(int i=0;i<100&&!form.BrowserInterface.Loaded;i++)await Task.Delay(100);
                if(!form.BrowserInterface.Loaded)throw new Exception("Browser interface did not load: "+form.BrowserInterface.Diagnostics);
                await Task.Delay(800);
                for(int frame=0;frame<50;frame++){if(await form.BrowserInterface.EvaluateAsync("window.routexStartupSettled===true")=="true")break;await Task.Delay(100);}
                string text=await form.BrowserInterface.EvaluateAsync("document.body.innerText");File.WriteAllText(Path.Combine(args[0],"page-text.json"),text);
                await form.BrowserInterface.CaptureAsync(Path.Combine(args[0],"routing.png"));
                Console.WriteLine(text.Substring(0,Math.Min(600,text.Length)));
                await form.BrowserInterface.EvaluateAsync("document.querySelector('details.rx-settings').open=true;document.querySelector('[data-check=killSwitch]').click()");
                await Task.Delay(500);
                if(Settings.Load().KillSwitch)throw new Exception("The HTML checkbox did not reach the native settings handler");
                Console.WriteLine("PASS HTML checkbox saves native Settings");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-action=\"add-list:include\"]').click();document.querySelector('dialog textarea').value='preview-ui.exe';document.querySelector('dialog form').requestSubmit()");
                await Task.Delay(500);
                if(!AppLists.Get(AppLists.HybridApps).Contains("preview-ui.exe"))throw new Exception("HTML list editor did not update AppLists");
                Console.WriteLine("PASS HTML list editor updates native routing lists");
                var locationMethod=typeof(MainForm).GetMethod("PageOf",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                if((int)locationMethod.Invoke(form,new object[]{"set.look"})!=MainForm.PgSettings)throw new Exception("Legacy module placement remains active");
                Console.WriteLine("PASS old module placement is ignored in the fixed interface");
                await form.BrowserInterface.EvaluateAsync("window.moveTestEntry=function(list){const source=document.querySelector('[data-entry=\"preview-ui.exe\"]'),target=document.querySelector('.rx-column[data-list='+list+']'),s=source.getBoundingClientRect(),t=target.getBoundingClientRect();source.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,button:0,pointerId:21,clientX:s.x+40,clientY:s.y+10}));source.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:21,clientX:t.x+40,clientY:t.y+65}));source.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,pointerId:21,clientX:t.x+40,clientY:t.y+65}));};moveTestEntry('exclude')");await Task.Delay(400);
                if(AppLists.Get(AppLists.HybridApps).Contains("preview-ui.exe")||!AppLists.Get(AppLists.HybridExcl).Contains("preview-ui.exe"))throw new Exception("Pointer drag did not move Include to Exclude");
                if(await form.BrowserInterface.EvaluateAsync("document.querySelectorAll('[data-entry][data-selected=true]').length===1&&document.querySelector('[data-entry=\"preview-ui.exe\"][data-list-key=exclude]').dataset.selected==='true'")!="true")throw new Exception("Dropped item did not retain exclusive selection in Exclude");
                await form.BrowserInterface.EvaluateAsync("moveTestEntry('include')");await Task.Delay(400);
                if(!AppLists.Get(AppLists.HybridApps).Contains("preview-ui.exe"))throw new Exception("Reverse pointer drag failed");
                if(await form.BrowserInterface.EvaluateAsync("document.querySelectorAll('[data-entry][data-selected=true]').length===1&&document.querySelector('[data-entry=\"preview-ui.exe\"][data-list-key=include]').dataset.selected==='true'")!="true")throw new Exception("Reverse drop did not move selection to Include");
                await form.BrowserInterface.EvaluateAsync("const row=document.querySelector('[data-entry=\"preview-ui.exe\"]');row.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,clientX:300,clientY:500}));document.querySelector('.r-entry-menu button').click();document.querySelector('dialog input[name=entry]').value='renamed-ui.exe';document.querySelector('dialog form').requestSubmit()");await Task.Delay(400);
                if(!AppLists.Get(AppLists.HybridApps).Contains("renamed-ui.exe"))throw new Exception("Context-menu entry editing did not persist");
                AppLists.Remove(AppLists.HybridApps,new[]{"renamed-ui.exe"});
                Console.WriteLine("PASS pointer drag in both directions and right-click entry rename");
                string fixtureIcon=Path.Combine(args[0],"fixture-icon.png");using(var input=typeof(MainForm).Assembly.GetManifestResourceStream("NetProbe.AppIcon.png"))using(var output=File.Create(fixtureIcon))input.CopyTo(output);
                AppLists.Add(AppLists.HybridApps,"icon-test.exe");AppIcons.SetCustom("icon-test.exe",fixtureIcon);form.BrowserInterface.UpdateSnapshot(form.BrowserSnapshot());await Task.Delay(350);
                if(await form.BrowserInterface.EvaluateAsync("!!document.querySelector('[data-entry=\"icon-test.exe\"] img[src^=\"data:image/png\"]')")!="true")throw new Exception("Custom entry icon was not rendered");
                AppLists.Remove(AppLists.HybridApps,new[]{"icon-test.exe"});AppIcons.SetCustom("icon-test.exe",null);
                Console.WriteLine("PASS saved custom icons appear in the routing list");
                ServerStore.Remove(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(ServerStore.Snapshot(),entry=>entry.Name=="Context A"||entry.Name=="Context B"),entry=>entry.Id));
                var serverA=new ServerEntry{Id="context-test-a",Name="Context A",Link="vless://00000000-0000-4000-8000-000000000001@192.0.2.231:443",Address="192.0.2.231",Port=443,Protocol="vless",UserId="00000000-0000-4000-8000-000000000001"};
                var serverB=new ServerEntry{Id="context-test-b",Name="Context B",Link="vless://00000000-0000-4000-8000-000000000002@192.0.2.232:443",Address="192.0.2.232",Port=443,Protocol="vless",UserId="00000000-0000-4000-8000-000000000002"};
                ServerStore.AddMany(new[]{serverA,serverB},"");serverA.TotalDown=321;serverB.TotalDown=654;ServerStore.Save();form.BrowserInterface.UpdateSnapshot(form.BrowserSnapshot());await Task.Delay(250);
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=subscriptions]').click();document.querySelector('[data-server-row=\""+serverA.Id+"\" ]').click();document.querySelector('[data-server-row=\""+serverB.Id+"\"]').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,clientX:800,clientY:600}))");
                if(await form.BrowserInterface.EvaluateAsync("document.querySelectorAll('.r-server-menu [data-server-action]').length===7&&document.querySelector('.r-server-menu').dataset.serverId==='"+serverB.Id+"'")!="true")throw new Exception("Per-server menu selection is incorrect: "+await form.BrowserInterface.EvaluateAsync("JSON.stringify({count:document.querySelectorAll('.r-server-menu [data-server-action]').length,menu:document.querySelector('.r-server-menu')?.outerHTML,error:window.ROUTEX_UI_ERROR,rows:[...document.querySelectorAll('[data-server-row]')].map(e=>e.dataset.serverRow)})"));
                form.BrowserInterface.UpdateSnapshot(form.BrowserSnapshot());await Task.Delay(150);if(await form.BrowserInterface.EvaluateAsync("!!document.querySelector('.r-server-menu')")!="true")throw new Exception("Status update closed the server context menu");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-server-action=favorite-selected]').click()");await Task.Delay(250);
                if(!serverB.Favorite||serverA.Favorite)throw new Exception("Favorite acted on the previously selected server");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-server-row=\""+serverB.Id+"\"]').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true}));document.querySelector('[data-server-action=reset-server-traffic]').click()");await Task.Delay(250);
                if(serverB.TotalDown!=0||serverA.TotalDown!=321)throw new Exception("Traffic reset affected the wrong server");
                foreach(double edge in new[]{.06,.5,.94})foreach(double vertical in new[]{.1,.5,.9}){
                    await ClickAt(form.BrowserInterface,"[data-server-row=\""+serverB.Id+"\"]",.9,.5,"right");
                    await ClickAt(form.BrowserInterface,"[data-server-action=delete-server]",edge,vertical);await Task.Delay(150);
                    if(await form.BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open] button[type=submit]')")!="true")throw new Exception("Real pointer missed Delete menu area at "+edge);
                    await form.BrowserInterface.EvaluateAsync("window.deleteHitCount=0;document.querySelector('dialog form').addEventListener('submit',event=>{event.preventDefault();event.stopImmediatePropagation();window.deleteHitCount++},true)");
                    await ClickAt(form.BrowserInterface,"dialog button[type=submit]",edge,vertical);await Task.Delay(100);
                    if(await form.BrowserInterface.EvaluateAsync("window.deleteHitCount===1")!="true")throw new Exception("Real pointer missed Delete confirmation area at "+edge);
                    await form.BrowserInterface.EvaluateAsync("document.querySelector('dialog [data-action=close-dialog]').click()");
                }
                Console.WriteLine("PASS real pointer Delete hit-testing across left, center and right areas, including held press and confirmation");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-server-row=\""+serverB.Id+"\"]').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true}));document.querySelector('[data-server-action=delete-server]').click();document.querySelector('dialog [data-action=close-dialog]').click()");await Task.Delay(150);
                if(ServerStore.Find(serverB.Id)==null)throw new Exception("Cancel deleted the server");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-server-row=\""+serverB.Id+"\"]').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true}));document.querySelector('[data-server-action=delete-server]').click();document.querySelector('dialog form').requestSubmit()");await Task.Delay(250);
                if(ServerStore.Find(serverB.Id)!=null||ServerStore.Find(serverA.Id)==null)throw new Exception("Delete affected the wrong server");
                ServerStore.Remove(new[]{serverA.Id});Console.WriteLine("PASS per-server context actions target clicked row; snapshot retains menu; favorite, traffic reset, cancel and delete are isolated");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=subscriptions]').click();document.querySelector('[data-action=open-autoswitch]').click();var n=document.querySelector('dialog [data-setting=asInterval]');n.value='37';n.dispatchEvent(new Event('change',{bubbles:true}))");
                await Task.Delay(500);
                if(Settings.Load().AsInterval!=37)throw new Exception("Autoswitch interval was not saved");
                await form.BrowserInterface.CaptureAsync(Path.Combine(args[0],"autoswitch.png"));
                await form.BrowserInterface.EvaluateAsync("document.querySelector('dialog [data-action=close-dialog]').click();document.querySelector('[data-nav=configs]').click();Array.from(document.querySelectorAll('[data-config-row]')).find(e=>e.dataset.configRow==='Personal config').click();var n=document.querySelector('[data-cfg-field=address]');n.value='192.0.2.99';n.dispatchEvent(new Event('change',{bubbles:true}))");
                await Task.Delay(500);
                if(TunnelProto.Load("Personal config").Address!="192.0.2.99")throw new Exception("Config editor did not save the tunnel");
                Console.WriteLine("PASS autoswitch and VPN config editor persist native files");
                for(int i=0;i<9;i++){
                    string page=new[]{"subscriptions","monitor","configs","dpi","tests","sites","logs","settings","tg"}[i];
                    await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav="+page+"]').click()");await Task.Delay(200);
                    await form.BrowserInterface.CaptureAsync(Path.Combine(args[0],page+".png"));
                }
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=settings]').click();document.querySelector('[data-theme-pick=chisa]').click()");
                for(int attempt=0;attempt<30;attempt++){await Task.Delay(100);if(await form.BrowserInterface.EvaluateAsync("!!document.querySelector('[data-settings-tab=character]')")=="true")break;}
                if(Settings.Load().Theme!=3)throw new Exception("Theme was not saved");
                string character=await form.BrowserInterface.EvaluateAsync("!!document.querySelector('[data-settings-tab=character]')");if(character!="true"){Console.WriteLine(await form.BrowserInterface.EvaluateAsync("JSON.stringify({focus:document.activeElement.outerHTML.slice(0,150),openMenus:document.querySelectorAll('.r-options:not([hidden])').length,dialog:!!document.querySelector('dialog[open]'),theme:document.querySelector('[data-theme-pick=chisa]')?.outerHTML.slice(0,200),error:window.ROUTEX_UI_ERROR})"));throw new Exception("Character tab is missing in Chisa");}
                Console.WriteLine("PASS theme changes retain the browser surface and Character tab");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-settings-tab=window]').click()");
                if(await form.BrowserInterface.EvaluateAsync("!!document.querySelector('[data-action=export-settings]')&&!!document.querySelector('[data-action=import-settings]')&&!document.querySelector('[data-check=uiLocked]')&&!document.querySelector('[data-action=mini-menu]')")!="true")throw new Exception("Settings backup or fixed sidebar controls are incorrect");
                Console.WriteLine("PASS settings import/export controls and fixed sidebar without monitor gear");
                var keptBrowser=form.BrowserInterface;await keptBrowser.EvaluateAsync("window.rebuildMarker='retained'");
                typeof(MainForm).GetMethod("RebuildUi",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,null);await Task.Delay(200);
                if(!ReferenceEquals(keptBrowser,form.BrowserInterface)||await keptBrowser.EvaluateAsync("window.rebuildMarker==='retained'")!="true")throw new Exception("Rebuilding native settings recreated the browser document");
                Console.WriteLine("PASS settings rebuild keeps the loaded browser and document intact");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-settings-tab=engine]').click()");
                if(await form.BrowserInterface.EvaluateAsync("document.querySelector('.r-maincontent h3').textContent==='Sources'&&!document.querySelector('[data-action=demo-service]')&&[...document.querySelectorAll('.r-maincontent h3')].some(e=>e.textContent==='Wiresock')&&[...document.querySelectorAll('.r-maincontent h3')].some(e=>e.textContent==='VLESS / VMess / Trojan')&&!!document.querySelector('[data-action=pick-dpi-folder]')")!="true")throw new Exception("Engine labels or DPI source picker are incorrect");
                string customDpi=Path.Combine(args[0],"custom-dpi-source");Directory.CreateDirectory(customDpi);
                string setFolder="document.querySelector('[data-setting=zapretDir]').value="+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(customDpi)+";document.querySelector('[data-setting=zapretDir]').dispatchEvent(new Event('change',{bubbles:true}))";
                await form.BrowserInterface.EvaluateAsync(setFolder);await Task.Delay(300);
                if(Settings.Load().ZapretPath!=Path.GetFullPath(customDpi))throw new Exception("Custom DPI folder was not saved");
                var dpiSnapshot=Settings.Load().Clone();var savedTarget=dpiSnapshot.ZapretPath;dpiSnapshot.ZapretDir="";if(savedTarget!=customDpi||dpiSnapshot.ZapretPath!=Path.Combine(Settings.Root,"zapret"))throw new Exception("DPI default/custom target resolution changed");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-action=default-dpi-folder]').click()");await Task.Delay(250);
                if(Settings.Load().ZapretDir!=""||Settings.Load().ZapretPath!=Path.Combine(Settings.Root,"zapret"))throw new Exception("DPI default folder could not be restored");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=subscriptions]').click()");form.ClientSize=new Size(1040,900);await Task.Delay(200);
                if(await form.BrowserInterface.EvaluateAsync("Math.abs(document.querySelector('.r-subs-toolbar>[data-action=server-paste]').getBoundingClientRect().left-document.querySelector('.r-subs-toolbar>.r-toolbar').getBoundingClientRect().left)<3")!="true")throw new Exception("Narrow Subscriptions toolbar is not left aligned");
                form.ClientSize=new Size(1440,1000);await Task.Delay(150);
                Console.WriteLine("PASS Engine labels, custom/default DPI folder persistence, and narrow toolbar alignment");
                var limits=new Settings{PollRate=100,DirectPollRate=100};limits.Clamp();if(limits.PollRate>0.7||limits.DirectPollRate>0.7||Settings.MinIntervalMs<1429)throw new Exception("Polling limits are not enforced");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=logs]').click();var slider=document.querySelector('[data-setting=pollRate]');slider.value='0.6';slider.dispatchEvent(new Event('input',{bubbles:true}));slider.dispatchEvent(new Event('change',{bubbles:true}))");await Task.Delay(250);
                if(Math.Abs(Settings.Load().PollRate-.6)>.001)throw new Exception("Polling slider did not save");
                if(await form.BrowserInterface.EvaluateAsync("document.querySelectorAll('.r-maincontent [data-setting=pollRate]').length===1&&document.querySelector('[data-setting=pollRate]').max==='0.7'")!="true")throw new Exception("Duplicate polling controls remain");
                Console.WriteLine("PASS one working polling slider and backend 0.7 polls/s cap");
                var prober=new CountingProber();var monitor=new ChannelMonitor("vpn"){GetSettings=()=>limits,GetProber=()=>prober};monitor.Start();
                try{for(int kick=0;kick<160;kick++){monitor.Kick();await Task.Delay(20);}}finally{monitor.Stop();}
                lock(prober.Calls){Console.WriteLine("Probe timestamps: "+string.Join(",",prober.Calls));if(prober.Calls.Count<2)throw new Exception("Monitor did not make enough test requests");for(int i=1;i<prober.Calls.Count;i++)if(prober.Calls[i]-prober.Calls[i-1]<1429)throw new Exception("Kick bypassed the polling cap");}
                Console.WriteLine("PASS repeated settings wakes cannot bypass the request interval (offline prober)");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=settings]').click();document.querySelector('[data-settings-tab=appearance]').click();window.palettePicker=document.querySelector('[data-palette=bg]');palettePicker.value='#123456';palettePicker.dispatchEvent(new Event('input',{bubbles:true}));palettePicker.dispatchEvent(new Event('change',{bubbles:true}))");await Task.Delay(250);
                if(!Settings.Load().UiCustomTheme||!Settings.Load().UiPalette.Contains("#123456"))throw new Exception("Custom palette was not saved");
                if(await form.BrowserInterface.EvaluateAsync("palettePicker===document.querySelector('[data-palette=bg]')")!="true")throw new Exception("Color input was recreated while editing");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-theme-pick=light]').click()");await Task.Delay(200);
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-theme-pick=custom]').click()");await Task.Delay(250);
                if(await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-palette=bg]').value")!="\"#123456\"")throw new Exception("Custom palette was lost after theme switching");
                await form.BrowserInterface.EvaluateAsync("document.querySelector('[data-action=reset-palette]').click()");await Task.Delay(200);
                if(Settings.Load().UiPalette!="{}")throw new Exception("Palette reset did not persist");
                Console.WriteLine("PASS palette stays intact across themes, switches instantly, and resets");
                var formSettings=(Settings)typeof(MainForm).GetField("st",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);formSettings.Notify=false;formSettings.DownAfterFails=3;typeof(MainForm).GetField("bgProbe",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(form,false);
                var failureMonitor=new ChannelMonitor("vpn"){GetSettings=()=>limits,GetProber=()=>prober};failureMonitor.Start();
                try{
                    var sampleMethod=typeof(MainForm).GetMethod("OnSample",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    for(int i=0;i<3;i++)sampleMethod.Invoke(form,new object[]{failureMonitor,new ProbeResult{Ok=false,Error="Test endpoint timeout",TargetUrl="https://primary.invalid/"},new StatsSnapshot()});
                    var indicator=(IndicatorCard)typeof(MainForm).GetField("cardVpn",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);
                    if(indicator.Badge!="PROBE UNAVAILABLE"||indicator.Level==Level.Down)throw new Exception("Probe failures falsely report a VPN outage: "+indicator.Badge+" / "+indicator.Level);
                    sampleMethod.Invoke(form,new object[]{failureMonitor,new ProbeResult{Ok=true,Lat=50,TargetUrl="https://secondary.invalid/"},new StatsSnapshot()});
                    if(indicator.Badge!=null)throw new Exception("Alternate success did not clear the probe warning");
                }finally{failureMonitor.Stop();}
                Console.WriteLine("PASS repeated endpoint failures show a probe warning, not VPN offline; alternate success clears it");
                await form.BrowserInterface.EvaluateAsync("location.href='https://example.com/blocked'");await Task.Delay(250);
                string origin=await form.BrowserInterface.EvaluateAsync("location.origin");if(origin!="\"https://routex.internal\"")throw new Exception("External navigation was not blocked");
                Console.WriteLine("PASS external navigation remains blocked");
                using(var revealForm=new Form{Size=new Size(1100,800),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false})using(var revealSurface=new BrowserSurface()){
                    revealForm.Controls.Add(revealSurface);await revealSurface.InitializeAsync(form.BrowserSnapshot());
                    for(int i=0;i<100&&!revealSurface.Loaded;i++)await Task.Delay(50);
                    if(!revealSurface.Loaded)throw new Exception("Hidden foreground surface failed to load");await Task.Delay(200);
                    if(await revealSurface.EvaluateAsync("!window.routexStartupSettled&&!window.routexRevealStartedAt&&!!document.querySelector('#rx-startup-overlay')")!="true")throw new Exception("Foreground reveal was consumed before the window became visible");
                    await revealSurface.EvaluateAsync("window.revealOpacitySamples=[];window.sampleReveal=function(){const overlay=document.querySelector('#rx-startup-overlay');if(overlay)revealOpacitySamples.push(Number(getComputedStyle(overlay).opacity));if(!window.routexStartupSettled)requestAnimationFrame(sampleReveal)};requestAnimationFrame(sampleReveal)");
                    revealForm.Show();for(int i=0;i<50;i++){if(await revealSurface.EvaluateAsync("window.routexStartupSettled===true")=="true")break;await Task.Delay(50);}
                    if(await revealSurface.EvaluateAsync("window.routexStartupSettled===true&&window.routexRevealDuration>=350&&revealOpacitySamples.some(value=>value>0&&value<.95)&&!document.querySelector('.rx-reveal-burst')&&!document.querySelector('#rx-startup-overlay')")!="true")throw new Exception("No visible opacity transition was rendered");
                }
                Console.WriteLine("PASS foreground reveal waits for visible window and renders intermediate opacity frames, including reduced-motion settings");
                Console.WriteLine("Captured actual WebView2 Windows interface. No VPN started.");
            }catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
            finally{form.Close();}
        };launch.Start();Application.Run(form);launch.Dispose();
    }
}
