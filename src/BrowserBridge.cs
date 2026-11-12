#if WEB_UI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using static NetProbe.Tr;

namespace NetProbe
{
    public partial class MainForm
    {
        BrowserSurface browser;
        Task browserInitialization;
        readonly System.Windows.Forms.Timer browserUpdates = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly Lazy<List<string>> browserFonts=new Lazy<List<string>>(FontChoices);
        static readonly string[] BrowserPages = { "routing", "subscriptions", "monitor", "configs", "dpi", "tests", "sites", "logs", "settings", "tg" };
        public Task EnableBrowserInterfaceAsync(){return browserInitialization??(browserInitialization=InitializeBrowserInterfaceAsync());}
        async Task InitializeBrowserInterfaceAsync()
        {
            if (browser != null) return;
            EmbeddedBrowser.Initialize();
            browser = new BrowserSurface{BackgroundStartup=hiddenToTray};
#if UI_PREVIEW
            tray.Visible=false;Text="RouteX — UI preview";
#endif
            browser.Command += BrowserCommand;
            Controls.Add(browser);browser.BringToFront();
            side.Visible = content.Visible = false;
            RefreshChrome();
            StartBrowserWatchdog();
            var init=browser.InitializeAsync(BrowserSnapshot());
            if(await Task.WhenAny(init,Task.Delay(BrowserInitTimeoutMs))!=init){
                init.ContinueWith(t=>{var unused=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("WebView2 did not start within "+BrowserInitTimeoutMs/1000+" s ("+browser.Diagnostics+")");
            }
            await init;
            browserUpdates.Tick += (s, e) => { if (!IsDisposed && Visible && WindowState != FormWindowState.Minimized) browser.UpdateSnapshot(BrowserSnapshot()); };
            browserUpdates.Start();
            FormClosed += (s, e) => browserUpdates.Dispose();
        }
        async void StartHiddenBrowser()
        {
            HideToTray(true);
#if !UI_PREVIEW
            Startup();
#endif
            try{await EnableBrowserInterfaceAsync();}catch(Exception ex){UseNativeInterface("Background interface initialization: "+ex.Message);}
        }
        async void ActivateBrowserInterface()
        {
            if(nativeFallback)return;
            try{await EnableBrowserInterfaceAsync();browser.UpdateSnapshot(BrowserSnapshot());await browser.ActivateInterfaceAsync();}catch(Exception ex){UseNativeInterface("Interface activation: "+ex.Message);}
        }
        // --- startup safety: the window must never stay on "Loading" forever ---
        const int BrowserInitTimeoutMs=30000,BrowserRevealTimeoutMs=12000;
        bool nativeFallback;
        bool WebUiShown{get{return browser!=null&&!nativeFallback;}}
        System.Windows.Forms.Timer browserWatchdog;
        void StartBrowserWatchdog()
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();long loadedAt=-1;bool busy=false;
            browserWatchdog=new System.Windows.Forms.Timer{Interval=1000};
            browserWatchdog.Tick+=async(s,e)=>{
                if(busy)return;
                if(nativeFallback||browser==null||browser.IsDisposed||browser.Revealed){browserWatchdog.Stop();return;}
                busy=true;
                try{
                    if(!browser.Loaded){
                        if(clock.ElapsedMilliseconds>BrowserInitTimeoutMs+5000){browserWatchdog.Stop();UseNativeInterface("Interface did not load ("+browser.Diagnostics+")");}
                        return;
                    }
                    if(loadedAt<0)loadedAt=clock.ElapsedMilliseconds;
                    if(clock.ElapsedMilliseconds-loadedAt<BrowserRevealTimeoutMs)return;
                    browserWatchdog.Stop();
                    string state="?";try{state=await browser.ScriptStateAsync();}catch(Exception ex){state=ex.Message;}
                    Logger.Write(LogLevel.Warn,"Interface loading screen timed out, showing the interface anyway · "+state+" · "+UiStartupTiming.Text());
                    UiStartupTiming.Save();
                    await browser.ForceRevealAsync();
                }catch(Exception ex){Logger.Write(LogLevel.Error,"Interface watchdog: "+ex.Message);}
                finally{busy=false;}
            };
            browserWatchdog.Start();
            FormClosed+=(s,e)=>browserWatchdog.Dispose();
        }
        // WebView2 could not start: keep the app usable with the classic built-in interface.
        void UseNativeInterface(string reason)
        {
            if(nativeFallback||IsDisposed)return;
            nativeFallback=true;
            Logger.Write(LogLevel.Error,reason+" · switched to the classic interface · "+UiStartupTiming.Text());
            UiStartupTiming.Save();
            try{browserUpdates.Stop();}catch{}
            try{if(browser!=null){browser.Visible=false;Controls.Remove(browser);}}catch{}
            side.Visible=content.Visible=true;
            RefreshChrome(); // the classic interface has no title bar of its own: give the system caption back
            try{PageFrame pf;if(frames.TryGetValue(curPage,out pf))LayFrame(pf);}catch{}
        }
        public BrowserSurface BrowserInterface { get { return browser; } }
        object BrowserConfig(string name)
        {
            var e = TunnelProto.Load(name);
            bool wg = !TunnelProto.IsXray(name);
            if (!wg && e != null && e.Protocol == "awg")
                return new {
                    id=name,name,protocol="AmneziaWG",transport="UDP",endpoint=e.Endpoint,
                    address=e.Address,port=e.Port,uuid="",flow="",cipher="auto",security="none",sni="",fingerprint="",publicKey="",shortId="",
                    network="udp",path="",host="",alpn="",spx="",xhttpMode="auto",insecure=false,raw=e.Awg
                };
            return new {
                id=name,name,protocol=wg?"WireGuard":(e?.Protocol??"vless").ToUpperInvariant(),
                transport=wg?"UDP":((e?.Security??"none").ToUpperInvariant()+" · "+(e?.Network??"tcp").ToUpperInvariant()),endpoint=wg?Tunnels.Endpoint(name):e?.Endpoint,
                address=e?.Address??"",port=e?.Port??443,uuid=e?.UserId??"",flow=e?.Flow??"",cipher=e?.Method??"auto",
                security=e?.Security??"none",sni=e?.Sni??"",fingerprint=e?.Fp??"",publicKey=e?.Pbk??"",shortId=e?.Sid??"",
                network=e?.Network??"tcp",path=e?.Path??"",host=e?.Host??"",alpn=e?.Alpn??"",spx=e?.Spx??"",xhttpMode=e?.Mode??"auto",
                insecure=e?.Insecure??false,raw=wg?Tunnels.Read(name):""
            };
        }
        object BrowserGrid(DataGridView grid)
        {
            return grid.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow).Select(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value)).ToArray()).ToArray();
        }
        public object BrowserSnapshot()
        {
            bool shownActive=AnyActive,shownBusy=router.ConnectPending||xm.ConnectPending||pendingXConnect!=null,shownDpi=router.ManualDpi||router.Dpi.Running,shownMonitoring=MonOn,review=false;
            string shownVersion=Version,shownStatus=ActiveSource()==Source.Subs?xBoard.State:board.State;
#if UI_PREVIEW
            review=true;shownActive=previewVpn;shownBusy=false;shownDpi=previewDpi;shownMonitoring=previewMonitoring;shownVersion=Version+" · UI PREVIEW";shownStatus=previewVpn?"Connected":"Disconnected";
            SeedUiPreviewMeasurements();
            if(!previewVpn||!previewMonitoring){cardVpn.Value="—";cardVpn.Level=Level.Off;}
            if(!previewMonitoring){cardDir.Value="—";cardDir.Level=Level.Off;}
#endif
            var settings = typeof(Settings).GetFields(BindingFlags.Public|BindingFlags.Instance).ToDictionary(f=>f.Name,f=>f.GetValue(st));
            Func<IEnumerable<string>,object> entries = list=>list.Select(x=>new {id=x,name=x==ListCard.BuiltIn?"RouteX.Checker":x,builtin=x==ListCard.BuiltIn,icon=BrowserEntryIcon(x)}).ToArray();
            Func<IndicatorCard,object> indicator = c=>new {title=c.Title,sub=c.Sub,value=c.Value,unit=c.Unit,line1=c.Line1,line2=c.Line2,line3=c.Line3,level=(int)c.Level,badge=c.Badge};
            Func<List<double>,double?[]> samples = a=>a.Select(v=>double.IsNaN(v)||double.IsInfinity(v)?(double?)null:v).ToArray();
            // latency: a single failed probe (LatencyChart.Blip) is -1, not a gap; only a confirmed outage (NaN) breaks the line
            Func<List<double>,double?[]> latency = a=>a.Select(v=>double.IsNegativeInfinity(v)?(double?)-1:double.IsNaN(v)||double.IsInfinity(v)?(double?)null:v).ToArray();
            return new {
                version=shownVersion,maximized=WindowState==FormWindowState.Maximized,settings,uiPreview=review,paths=new {dpi=st.ZapretPath},activeConfig=router.Active?router.Wg.TunnelName:st.PrimaryTunnel,whatsNew=st.WhatsNewPending==Version&&st.WhatsNewShownFor!=Version,
                fonts=browserFonts.Value,languages=Tr.Codes.Select((code,i)=>new {id=code,name=Tr.Names[i]}).ToArray(),
                labels=BrowserLabels(),
                adapters=adapters.Select(a=>new {id=a.Id,name=a.Name+" · "+a.Ip}).ToArray(),artwork=BrowserArtwork(),
                cores=new {xray=review||File.Exists(XrayProcess.Exe(st)),singbox=review||File.Exists(SingBoxProcess.Exe(st))},
                active=shownActive,busy=shownBusy,locked=TunSettingsLocked||review&&shownActive,
                dpi=shownDpi,monitoring=shownMonitoring,
                source=ActiveSource()==Source.Subs?"subscriptions":"configs",
                page=curPage>=0&&curPage<BrowserPages.Length?BrowserPages[curPage]:"routing",
                configs=Tunnels.Names().Select(BrowserConfig).ToArray(),
                servers=ServerStore.Snapshot().Select(s=>new {id=s.Id,name=s.Name,group=ServerStore.GroupName(s.Group),groupId=s.Group,protocol=s.Protocol.ToUpperInvariant(),transport=s.Security.ToUpperInvariant()+" · "+s.Network.ToUpperInvariant(),address=s.Address,port=s.Port,country=s.Country==""?"—":s.Country,latency=s.Delay<0?(object)"—":s.Delay,speed=s.Speed<0?0:Math.Round(s.Speed/8,s.Speed<80?2:1),total=Bytes(s.TotalDown),favorite=s.Favorite,manual=!ServerStore.Subs.Any(sub=>sub.Id==s.Group)}).ToArray(),
                subscriptions=ServerStore.Subs.Select(s=>new {id=s.Id,name=s.Name,url=s.Url}).ToArray(),
                lists=new {include=entries(AppLists.Get(AppLists.HybridApps)),strict=entries(AppLists.Get(AppLists.VpnOnlyApps)),exclude=entries(AppLists.Get(Router.TunExclKey(st)))},
                profiles=BrowserProfiles(),
                dpiDomains=string.Join("\n",AppLists.Get(AppLists.DpiDomains)),dpiExclude=string.Join("\n",AppLists.DpiSitesExcl()),
                channels=new {vpn=indicator(cardVpn),direct=indicator(cardDir)},
                chart=new {vpn=latency(chart.HA??chart.A),direct=latency(chart.HB??chart.B),timeVpn=chart.TA.Select(t=>new DateTimeOffset(t).ToUnixTimeMilliseconds()).ToArray(),timeDirect=chart.TB.Select(t=>new DateTimeOffset(t).ToUnixTimeMilliseconds()).ToArray()},
                speed=new {down=samples(speedChart.Down),up=samples(speedChart.Up),adapter=speedChart.Adapter},
                currentSpeed=new {download=st.SpeedOn&&speedChart.Down.Count>0?(speedChart.Down.Last()/8).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" MB/s":"—",upload=st.SpeedOn&&speedChart.Up.Count>0?(speedChart.Up.Last()/8).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" MB/s":"—"},
                tests=BrowserGrid(grid),sites=BrowserGrid(siteGrid),testRunning=runner!=null,testProgress=prog.Value,progress=progLabel.Text,
                logs=ViewItems().Select(e=>new {time=e.Item1.ToString("HH:mm:ss"),level=e.Item2.ToString().ToUpperInvariant(),text=e.Item3}).ToArray(),
                logging=new {errorsOnly=chkErrOnly.Checked,routesOnly=chkRouteOnly.Checked,days=cbLogDay.Items.OfType<DayItem>().Select(d=>new {id=d.Day.Date==DateTime.Today?"today":d.Day.ToString("yyyy-MM-dd"),name=d.ToString()}).ToArray()},
                monitorInfo=review?"UI preview · simulated values · no VPN connection":infoLabel.Text,routeStatus=shownStatus,routeDetail=review?"xray · Personal config · TUN · Preview":board.Line1,routeDetail2=review?"VPN 52 ms · Direct 12 ms · Switches: 0 · Preview":board.Line2,engine=EngineText(),engineShort=EngineShort(),subscriptionState=review?(shownActive&&st.Source==1?"Connected":"Disconnected"):xBoard.State,subscriptionActive=review?(shownActive&&st.Source==1):xm.State==XState.Connected,
                tg=new {running=TgProxy.Running,on=st.TgOn,error=TgProxy.Running?"":(TgProxy.LastError??""),state=tgpState.Text,address=tgpAddr.Text,link=tgpLink.Text,connections=TgStats.Active,total=TgStats.Total,ws=TgStats.Ws,cf=TgStats.Cf,sent=TgConst.Human(TgStats.Up),received=TgConst.Human(TgStats.Down)},
                updates=new {available=appRelease!=null,text=appRelease==null?"":btnAppUpd!=null&&!btnAppUpd.Enabled?btnAppUpd.Text:"Update to "+appRelease.Tag,app=LabelText(lAppVer),zapret=LabelText(lZVer),xray=LabelText(lXVer),singbox=LabelText(lSVer)}
            };
        }
        // The snapshot is rebuilt every second while the window is visible. The translation table (over a thousand
        // entries) and every DPI strategy file were rebuilt / re-read from disk each time.
        string browserLabelsLang;Dictionary<string,string> browserLabelsCache;
        Dictionary<string,string> BrowserLabels()
        {
            if(browserLabelsCache==null||browserLabelsLang!=st.Lang){browserLabelsCache=Tr.Dict("en").GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>Tr.Tx(g.First().Key));browserLabelsLang=st.Lang;}
            return browserLabelsCache;
        }
        string browserProfilesKey;object browserProfilesCache;
        object BrowserProfiles()
        {
            var names=DpiConfigs.Names();
            string key=string.Join("|",names.Select(n=>{try{return n+":"+File.GetLastWriteTimeUtc(DpiConfigs.PathOf(n)).Ticks;}catch{return n;}}));
            if(browserProfilesCache==null||key!=browserProfilesKey){browserProfilesCache=names.Select(n=>new {id=n,name=n,args=DpiConfigs.Read(n)}).ToArray();browserProfilesKey=key;}
            return browserProfilesCache;
        }
        static string LabelText(Label label){return label==null||label.IsDisposed?"":label.Text;}
        static string BrowserString(Dictionary<string,object> args,string name,string fallback="") { object value;return args.TryGetValue(name,out value)?Convert.ToString(value):fallback; }
        static readonly string[] DialogActions={"choose-background","app-icon-pick","character-pick","character-inside-pick","import-theme","import-settings","export-settings","import-bat","import-zapret","change-log-folder","save-logs","export-tests","export-sites","check-update","install-core","entry-icon","pick-dpi-folder","export-theme"};
        void BrowserCommand(Dictionary<string,object> message)
        {
            int id=0;try {
                if(message.ContainsKey("id"))id=Convert.ToInt32(message["id"]);
                string command=BrowserString(message,"command");
                var args=message.ContainsKey("args")?message["args"] as Dictionary<string,object>:null;args=args??new Dictionary<string,object>();
                string action=command=="action"?BrowserString(args,"name"):"";
                if(command=="appPicker"){BrowserAppPicker(id,args);return;}
                if(command=="window"){BrowserWindowCommand(BrowserString(args,"op"));return;}
                if(Array.IndexOf(DialogActions,action)>=0){
                    // A file / message dialog must not run inside the WebView2 message handler: it can open
                    // behind the window or block the browser until closed. Answer now, run it right after.
                    browser.Reply(id,null);
                    BeginInvoke((Action)(()=>{
                        try{DispatchBrowserCommand(command,args);}
                        catch(Exception ex){Logger.Write(LogLevel.Error,"Interface: "+ex.Message);MessageBox.Show(this,ex.Message,"RouteX",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                        if(!IsDisposed&&WebUiShown)browser.UpdateSnapshot(BrowserSnapshot());
                    }));
                    return;
                }
                object result=DispatchBrowserCommand(command,args);
                browser.Reply(id,result);if(command!="navigate"&&command!="clipboard"&&command!="interface-ready"&&command!="whats-new-shown")browser.UpdateSnapshot(BrowserSnapshot());
            }catch(Exception ex){browser.Reply(id,null,ex.Message);Logger.Write(LogLevel.Error,"Interface: "+ex.Message);}
        }
        string BrowserListKey(string key)
        {
            if(key=="include")return AppLists.HybridApps;if(key=="strict")return AppLists.VpnOnlyApps;if(key=="exclude")return Router.TunExclKey(st);
            throw new ArgumentException("Unknown application list");
        }
        object DispatchBrowserCommand(string command,Dictionary<string,object> args)
        {
            string name=BrowserString(args,"name"),value=BrowserString(args,"value");
            bool editingLocked=TunSettingsLocked;
#if UI_PREVIEW
            editingLocked|=previewVpn;
#endif
            if(command.StartsWith("list",StringComparison.Ordinal)&&editingLocked)throw new InvalidOperationException(TunLockMessage);
            switch(command){
                case "interface-ready":UiStartupTiming.Finish();return null;
                case "whats-new-shown":if(st.WhatsNewPending==Version){ReleaseReview.MarkShown(st,Version);st.Save();}return null;
                case "snapshot":return BrowserSnapshot();
                case "navigate":{int page=Array.IndexOf(BrowserPages,name);if(page<0)throw new ArgumentException("Unknown page");SelectPage(page);return null;}
                case "power":
                    bool stopping=AnyActive;
#if UI_PREVIEW
                    stopping=previewVpn;
#endif
                    if(name=="vpn"&&!stopping){
                        string chosenSource=BrowserString(args,"source");
                        if(chosenSource!=""&&chosenSource!="configs"&&chosenSource!="subscriptions")throw new ArgumentException("Unknown connection source");
                        if(st.RunMode!=RouteMode.DpiOnly&&ConnectionSources.NeedsChoice(st.HybridMode,TunnelCount,HasSubs?1:0)&&chosenSource=="")throw new InvalidOperationException("Choose a connection source first");
                        if(chosenSource!=""&&st.HybridMode)st.Source=chosenSource=="subscriptions"?1:0;
                        else if(curPage==PgServers)st.Source=1;
                        else if(curPage==PgConnect)st.Source=0;
                        st.Source=ConnectionSources.Resolve(st.HybridMode,TunnelCount,HasSubs?1:0,st.Source)==Source.Subs?1:0;
                        if(st.Source==1){string selected=BrowserString(args,"selectedServer");if(ServerStore.Find(selected)!=null)st.ActiveServer=selected;}
                        st.Save();LoadConnectUi();
                    }
#if UI_PREVIEW
                    if(name=="vpn"){if(!previewVpn)NotifySubscriptionPriority();previewVpn=!previewVpn;}else if(name=="dpi")previewDpi=!previewDpi;else throw new ArgumentException("Unknown switch");return null;
#else
                    if(name=="vpn")VpnButtonClick(null,true);else if(name=="dpi")ToggleDpi();else throw new ArgumentException("Unknown switch");return null;
#endif
                case "clipboard":if(value.Length>1048576)throw new ArgumentException("Text is too large");if(value.Length>0)Clipboard.SetText(value);return null;
                case "listAdd":if(!CanEditList(BrowserListKey(name)))throw new InvalidOperationException(TunLockMessage);foreach(string item in AppLists.ParseClipboard(value))AddToList(BrowserListKey(name),item);return null;
                case "listRemove":if(!CanEditList(BrowserListKey(name)))throw new InvalidOperationException(TunLockMessage);if(value!=ListCard.BuiltIn)RemoveFromList(BrowserListKey(name),new List<string>{value});return null;
                case "listRename":{
                    string key=BrowserListKey(name),renamed=BrowserString(args,"renamed").Trim();
                    if(value==ListCard.BuiltIn||renamed==""||renamed.IndexOf(AppLists.Sep)>=0||renamed.Equals(ListCard.BuiltIn,StringComparison.OrdinalIgnoreCase)||renamed.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Invalid entry name");
                    if(!AppLists.Get(key).Contains(value))throw new ArgumentException("Entry not found");
                    if(!renamed.Equals(value,StringComparison.OrdinalIgnoreCase)&&AppLists.Get(key).Contains(renamed))throw new ArgumentException("Entry already exists");
                    AppLists.Rename(key,value,renamed);AppIcons.RenameCustom(value,renamed);return null;
                }
                case "listMove":{string from=BrowserListKey(BrowserString(args,"from")),to=BrowserListKey(name);if(!CanEditList(from)||!CanEditList(to))throw new InvalidOperationException(TunLockMessage);if(value==ListCard.BuiltIn)throw new InvalidOperationException("Built-in probe cannot be moved");AddToList(to,value);AppLists.Remove(from,new[]{value});return null;}
                case "action":return BrowserAction(name,args);
                case "setting":return BrowserSetting(name,args.ContainsKey("value")?args["value"]:null);
                case "config":return BrowserEditConfig(name,args);
                case "dpi":DpiConfigs.Write(name,value);FillStrategies(name);if(name==st.DpiStrategy&&router.Dpi.Running)router.RestartDpi();return null;
                default:throw new ArgumentException("Unknown interface command");
            }
        }
    }
}
#endif
