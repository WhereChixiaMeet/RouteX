#if WEB_UI && UI_PREVIEW
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NetProbe
{
    public partial class MainForm
    {
        bool previewVpn=true,previewDpi,previewMonitoring=true;
        public void StartUiPreviewRevealSmoke()
        {
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ClientSize=new Size(1100,760);ShowInTaskbar=false;
            string folder=Path.Combine(Settings.Root,"reveal-frames");Directory.CreateDirectory(folder);
            var timer=new Timer{Interval=85};var records=new System.Collections.Generic.List<object>();bool busy=false;int attempts=0,welcomeFrames=0;
            timer.Tick+=async(s,e)=>{
                if(busy)return;busy=true;
                try{
                    if(BrowserInterface==null||!BrowserInterface.Loaded){if(++attempts>200)throw new Exception("Reveal capture startup timed out");return;}
                    string status=await BrowserInterface.EvaluateAsync("(()=>{const overlay=document.querySelector('#rx-startup-overlay');return{time:performance.now(),opacity:overlay?Number(getComputedStyle(overlay).opacity):0,settled:!!window.routexStartupSettled,welcome:!!document.querySelector('dialog[open]')};})()");
                    records.Add(status);await BrowserInterface.CaptureAsync(Path.Combine(folder,"frame-"+records.Count.ToString("D3")+".png"));
                    if(status.Contains("\"welcome\":true"))welcomeFrames++;
                    if(welcomeFrames>=7||records.Count>=45){timer.Stop();File.WriteAllText(Path.Combine(folder,"timeline.json"),"["+string.Join(",",records)+"]");timer.Dispose();Close();}
                }catch(Exception ex){timer.Stop();File.WriteAllText(Path.Combine(Settings.Root,"reveal-error.txt"),ex.ToString());Environment.ExitCode=1;Close();}
                finally{busy=false;}
            };timer.Start();
        }
        public void StartUiPreviewBackgroundSmoke()
        {
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;
            var check=new Timer{Interval=200};int attempts=0;
            check.Tick+=async(s,e)=>{
                if(BrowserInterface==null||!BrowserInterface.Loaded){if(++attempts<150)return;check.Stop();File.WriteAllText(Path.Combine(Settings.Root,"background-error.txt"),"Hidden interface did not initialize");Environment.ExitCode=1;Close();return;}
                check.Stop();
                try{
                    for(int i=0;i<40;i++){if(await BrowserInterface.EvaluateAsync("window.routexStartupSettled===true")=="true")break;await System.Threading.Tasks.Task.Delay(100);}
                    if(Visible)throw new Exception("Background initialization showed the main window");
                    if(st.WhatsNewPending!=Version||st.WhatsNewShownFor==Version)throw new Exception("Welcome was consumed while hidden");
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('#rx-startup-overlay')&&!document.querySelector('dialog[open]')")!="true")throw new Exception("Hidden warmup left a loading overlay or welcome dialog");
                    var original=BrowserInterface;await original.EvaluateAsync("window.backgroundWarmupMarker='ready'");
                    RestoreFromTray();
                    for(int i=0;i<30;i++){if(await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open]')")=="true")break;await System.Threading.Tasks.Task.Delay(100);}
                    if(!ReferenceEquals(original,BrowserInterface)||await BrowserInterface.EvaluateAsync("backgroundWarmupMarker==='ready'&&!document.querySelector('#rx-startup-overlay')")!="true")throw new Exception("Tray restore reloaded the interface");
                    if(st.WhatsNewShownFor!=Version)throw new Exception("Welcome was not shown on first foreground activation");
                    await BrowserInterface.EvaluateAsync("document.querySelector('dialog [data-action=close-dialog]').click()");
                    HideToTray(true);RestoreFromTray();await System.Threading.Tasks.Task.Delay(200);
                    if(await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open]')")=="true")throw new Exception("Welcome repeated on tray restore");
                    File.WriteAllText(Path.Combine(Settings.Root,"background-pass.txt"),"PASS hidden startup initializes the interface without showing the window; tray restore reuses the same document without Loading; welcome waits for first activation and does not repeat.");
                }catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(Settings.Root,"background-error.txt"),ex.ToString());}
                finally{check.Dispose();Close();}
            };check.Start();
        }
        public void StartUiPreviewSmoke()
        {
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ClientSize=new Size(1440,1000);ShowInTaskbar=false;
            var smoke=new Timer{Interval=200};int attempts=0;
            smoke.Tick+=async(s,e)=>{
                if(BrowserInterface==null||!BrowserInterface.Loaded){if(++attempts<150)return;smoke.Stop();Environment.ExitCode=1;File.WriteAllText(Path.Combine(Settings.Root,"smoke-error.txt"),"Interface startup timed out");Close();return;}
                smoke.Stop();
                try{
                    await System.Threading.Tasks.Task.Delay(600);
                    for(int frame=0;frame<50;frame++){if(await BrowserInterface.EvaluateAsync("window.routexStartupSettled===true")=="true")break;await System.Threading.Tasks.Task.Delay(100);}
                    if(st.WhatsNewPending==Version)for(int frame=0;frame<30;frame++){if(await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open]')")=="true")break;await System.Threading.Tasks.Task.Delay(100);}
                    await System.Threading.Tasks.Task.Delay(300);
                    string folder=Path.Combine(Settings.Root,"screenshots");Directory.CreateDirectory(folder);
                    if(await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open]')")=="true"){
                        await BrowserInterface.CaptureAsync(Path.Combine(folder,"whats-new.png"));
                        await BrowserInterface.CaptureElementAsync(Path.Combine(folder,"welcome.png"),"dialog[open]");
                        if(await BrowserInterface.EvaluateAsync("document.querySelector('dialog h2').textContent==='Welcome to RouteX 4.0'&&!!document.querySelector('.r-release-brand img')")!="true")throw new Exception("Welcome branding is missing");
                        int navigationCount=BrowserInterface.DocumentNavigations;
                        await BrowserInterface.EvaluateAsync("window.welcomeDocumentMarker='same-document';document.querySelector('dialog [data-dismiss-welcome]').click()");
                        await System.Threading.Tasks.Task.Delay(3000);
                        if(BrowserInterface.DocumentNavigations!=navigationCount||await BrowserInterface.EvaluateAsync("window.welcomeDocumentMarker==='same-document'&&!document.querySelector('#rx-startup-overlay')&&!document.querySelector('dialog[open]')")!="true")throw new Exception("Continue reloaded the interface or restarted Loading");
                        if(Settings.Load().WhatsNewShownFor!=Version)throw new Exception("Release review was not recorded");
                    }
                    await BrowserInterface.EvaluateAsync("window.testDpiButton=document.querySelector('[data-power=dpi]');testDpiButton.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}))");
                    BrowserInterface.UpdateSnapshot(BrowserSnapshot());await System.Threading.Tasks.Task.Delay(160);
                    if(await BrowserInterface.EvaluateAsync("testDpiButton===document.querySelector('[data-power=dpi]')")!="true")throw new Exception("A status update replaced a pressed button");
                    await BrowserInterface.EvaluateAsync("testDpiButton.dispatchEvent(new PointerEvent('pointerup',{bubbles:true}));testDpiButton.click()");await System.Threading.Tasks.Task.Delay(240);
                    if(!previewDpi)throw new Exception("The first DPI click was lost");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=dpi]').click()");await System.Threading.Tasks.Task.Delay(240);
                    if(previewDpi)throw new Exception("The second DPI click was lost");
                    using(var splash=new UiLoadingPanel{Size=new Size(1440,1000)})using(var splashImage=new Bitmap(1440,1000)){splash.DrawToBitmap(splashImage,new Rectangle(0,0,1440,1000));splashImage.Save(Path.Combine(folder,"loading.png"));}
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('.rx-source-row [data-combo=source]')&&!!document.querySelector('.rx-source-row [data-combo=backupConfig]')&&!!document.querySelector('.rx-settings-grid [data-check=backupEnabled]')")!="true")throw new Exception("Routing source/backup layout regression");
                    foreach(string page in new[]{"routing","subscriptions","monitor","configs","dpi","tests","sites","logs","settings","tg"}){
                        await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav="+page+"]').click()");
                        if(page=="routing")await BrowserInterface.EvaluateAsync("document.querySelector('[data-entry=\"chrome.exe\"]').click()");
                        await System.Threading.Tasks.Task.Delay(120);
                        string missingIcons=await BrowserInterface.EvaluateAsync("JSON.stringify([...document.querySelectorAll('i[data-lucide]')].map(e=>e.dataset.lucide))");
                        if(missingIcons!="\"[]\"")throw new Exception("Missing icons on "+page+": "+missingIcons);
                        await BrowserInterface.CaptureAsync(Path.Combine(folder,page+".png"));
                    }
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=subscriptions]').click();document.querySelector('[data-action=open-autoswitch]').click()");
                    BrowserInterface.UpdateSnapshot(BrowserSnapshot());await System.Threading.Tasks.Task.Delay(150);
                    if(await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open]')")!="true")throw new Exception("Snapshot closed the Autoswitch dialog");
                    await BrowserInterface.CaptureAsync(Path.Combine(folder,"autoswitch.png"));
                    await BrowserInterface.EvaluateAsync("document.querySelector('dialog [data-action=close-dialog]').click()");
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('.r-subs-status [data-combo=source]')&&[...document.querySelectorAll('.r-subs-table th')].some(e=>e.textContent==='Total ↓')&&!!document.querySelector('.r-subs-toolbar [data-action=server-columns]')&&!!document.querySelector('.r-subs-tabsrow [data-server-search]')")!="true")throw new Exception("Subscription layout regression");
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('[data-action^=\"connect-row:\"]')&&document.querySelector('.r-subs-toolbar').lastElementChild.classList.contains('r-core-label')")!="true")throw new Exception("Subscription Connect buttons remain");
                    await BrowserInterface.EvaluateAsync("document.querySelectorAll('[data-server-row]')[1].dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,clientX:600,clientY:430}))");
                    if(await BrowserInterface.EvaluateAsync("document.querySelectorAll('.r-server-menu [data-server-action]').length===7&&document.querySelector('.r-server-menu').dataset.serverId===document.querySelectorAll('[data-server-row]')[1].dataset.serverRow")!="true")throw new Exception("Per-server context menu targets the wrong row");
                    await BrowserInterface.CaptureAsync(Path.Combine(folder,"server-context-menu.png"));
                    await BrowserInterface.EvaluateAsync("document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=settings]').click();document.querySelector('[data-settings-tab=window]').click()");
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('.r-maincontent [data-check=sidePing]')&&!document.querySelector('.r-maincontent [data-check=sideSpeed]')&&document.querySelectorAll('.r-maincontent [data-check^=\"visible.\"]').length===4")!="true")throw new Exception("Duplicate sidebar controls remain");
                    await BrowserInterface.CaptureAsync(Path.Combine(folder,"window-settings.png"));
                    if(await BrowserInterface.EvaluateAsync("document.querySelector('[data-settings-tab=window]').textContent==='App'&&!!document.querySelector('[data-app-startup] [data-check=autorun]')")!="true")throw new Exception("App startup controls are missing");
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('[data-action=show-widget]')")!="true")throw new Exception("Show widget preview button remains");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-check=widget]').click()");await System.Threading.Tasks.Task.Delay(250);
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=routing]').click()");
                    await BrowserInterface.CaptureWidgetAsync(Path.Combine(folder,"widget.png"));
                    if(await BrowserInterface.EvaluateAsync("!document.querySelector('.r-widget [data-action=hide-widget]')")!="true")throw new Exception("Widget close button remains");
                    st.ShowWidget=false;st.Save();BrowserInterface.UpdateSnapshot(BrowserSnapshot());await System.Threading.Tasks.Task.Delay(150);
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=settings]').click()");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-settings-tab=engine]').click()");
                    if(await BrowserInterface.EvaluateAsync("!!document.querySelector('.r-maincontent [data-check=speedOn]')")!="true")throw new Exception("Sources is missing Speed monitor");
                    string savedDpiFolder=st.ZapretDir;st.ZapretDir=@"C:\RouteX\zapret";BrowserInterface.UpdateSnapshot(BrowserSnapshot());await System.Threading.Tasks.Task.Delay(150);await BrowserInterface.CaptureAsync(Path.Combine(folder,"engine-settings.png"));st.ZapretDir=savedDpiFolder;BrowserInterface.UpdateSnapshot(BrowserSnapshot());
                    if(!previewVpn)throw new Exception("Preview did not start with simulated VPN enabled");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-nav=routing]').click()");
                    if(await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').getAttribute('aria-label')==='Tunnel'&&document.querySelector('[data-power=vpn] .rx-powertext').textContent==='Tunnel ON'")!="true")throw new Exception("Tunnel switch label regression");
                    await BrowserInterface.CaptureElementAsync(Path.Combine(folder,"tunnel-controls.png"),".rx-topactions");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').click()");await System.Threading.Tasks.Task.Delay(250);
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').click()");await System.Threading.Tasks.Task.Delay(150);
                    if(previewVpn||await BrowserInterface.EvaluateAsync("!!document.querySelector('dialog[open] select[name=source]')")!="true")throw new Exception("Hybrid source selection is missing");
                    await BrowserInterface.CaptureAsync(Path.Combine(folder,"connection-source.png"));
                    await BrowserInterface.EvaluateAsync("document.querySelector('dialog [data-action=close-dialog]').click()");await System.Threading.Tasks.Task.Delay(150);if(previewVpn)throw new Exception("Cancel connected the tunnel");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').click();document.querySelector('dialog select[name=source]').value='subscriptions';document.querySelector('dialog form').requestSubmit()");await System.Threading.Tasks.Task.Delay(250);
                    if(!previewVpn||st.Source!=1)throw new Exception("Hybrid subscription selection failed");
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').click()");await System.Threading.Tasks.Task.Delay(250);
                    await BrowserInterface.EvaluateAsync("document.querySelector('[data-power=vpn]').click();document.querySelector('dialog select[name=source]').value='configs';document.querySelector('dialog form').requestSubmit()");await System.Threading.Tasks.Task.Delay(250);
                    if(!previewVpn||st.Source!=0)throw new Exception("Hybrid primary config selection failed");
                    File.WriteAllText(Path.Combine(Settings.Root,"smoke-pass.txt"),"PASS real Program.Main startup; 10 pages; status updates preserve pressed buttons; each DPI click works; Autoswitch stays open; Total column and toolbar placement; no duplicate sidebar controls; simulated VPN ON; no VPN engine started.");
                }catch(Exception ex){Environment.ExitCode=1;string details="";try{details=await BrowserInterface.EvaluateAsync("JSON.stringify({url:location.href,error:window.ROUTEX_UI_ERROR,bootError:window.ROUTEX_BOOT_ERROR,settled:window.routexStartupSettled,body:document.body.innerText.slice(0,700)})");}catch{}File.WriteAllText(Path.Combine(Settings.Root,"smoke-error.txt"),ex+"\n"+details);}
                finally{smoke.Dispose();Close();}
            };smoke.Start();
        }
        public static void PrepareUiPreview()
        {
            var settings=Settings.Load();if(!settings.UiPreviewProfile&&settings.LastRunVersion=="")settings.LastRunVersion="3.6.0";settings.UiPreviewProfile=true;settings.AutoConnect=false;settings.XrayAutoConnect=false;settings.Autorun=false;settings.AutoStartMonitor=false;settings.LogProbes=false;settings.ShowWidget=false;settings.SpeedOn=true;
            settings.LogDir="";settings.MinimizeToTray=false;settings.CloseToTray=false;settings.StartMinimized=false;settings.HybridMode=true;settings.HybridWarned=true;settings.SocksPort=15808;settings.HttpPort=15809;settings.MonDefaults312=settings.SpeedDefaults=settings.LockDefault321=true;settings.CheckUpdates=false;settings.UpdNotify=false;settings.Save();
            if(Tunnels.Names().Count==0){
                TunnelProto.Save("Personal config",new ServerEntry{Protocol="vless",Address="192.0.2.10",Port=443,UserId="00000000-0000-4000-8000-000000000001",Security="reality",Network="xhttp",Sni="example.com",Pbk="preview-public-key",Sid="0123456789abcdef"});
                TunnelProto.Save("Backup config",new ServerEntry{Protocol="vless",Address="192.0.2.20",Port=443,UserId="00000000-0000-4000-8000-000000000002",Security="tls",Network="tcp",Sni="example.com"});
                settings.PrimaryTunnel="Personal config";settings.BackupTunnel="Backup config";settings.BackupEnabled=true;settings.Save();
                AppLists.Set(AppLists.HybridApps,new[]{ListCard.BuiltIn,"chrome.exe","Discord.exe","ChatGPT.exe"});AppLists.Set(AppLists.HybridExcl,new[]{"brave.exe","steam.exe"});
            }
            if(DpiConfigs.Names().Count==0)for(int i=0;i<22;i++)DpiConfigs.Write(i==0?"general":"general (ALT"+i+")","--wf-tcp=80,443 --wf-udp=443\r\n--filter-tcp=443 --dpi-desync=fake,multisplit");
            ServerStore.Load();if(ServerStore.Snapshot().Count==0){
                var entries=new[]{"vless://00000000-0000-4000-8000-000000000011@192.0.2.50:443?security=reality&type=xhttp&sni=example.com&pbk=preview#Germany","vless://00000000-0000-4000-8000-000000000012@192.0.2.51:443?security=reality&type=tcp&sni=example.com&pbk=preview#Finland"}.Select(ServerEntry.Parse).ToArray();
                entries[0].Delay=52;entries[0].Speed=64;entries[0].Favorite=true;entries[0].Country="DE";entries[1].Delay=68;entries[1].Speed=81;entries[1].Country="FI";ServerStore.AddMany(entries,"");
            }
        }
        void SeedUiPreviewMeasurements()
        {
            cardVpn.Sub="VLESS · REALITY · XHTTP · Preview";cardVpn.Value="52";cardVpn.Level=Level.Good;cardVpn.Line1="IP 198.51.100.20 · DE · HTTP 200";cardVpn.Line2="DNS 17 · TCP 35 · TLS 56 · total 143 ms";cardVpn.Line3="Preview data";
            cardDir.Sub="Ethernet · Preview";cardDir.Value="12";cardDir.Level=Level.Good;cardDir.Line1="IP 192.0.2.15 · RU · HTTP 200";cardDir.Line2="DNS 9 · TCP 12 · TLS 22 · total 43 ms";cardDir.Line3="Preview data";
            chart.Clear();speedChart.Down.Clear();speedChart.Up.Clear();
            for(int i=0;i<100;i++){chart.A.Add(52+Math.Sin(i*.4)*4);chart.B.Add(12+Math.Sin(i*.25)*2);chart.TA.Add(DateTime.Now.AddSeconds((i-100)*3));chart.TB.Add(DateTime.Now.AddSeconds((i-100)*3));speedChart.Down.Add(38.4+Math.Sin(i*.3)*8);speedChart.Up.Add(4.8+Math.Sin(i*.2));}
        }
    }
}
#endif
