#if WEB_UI
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace NetProbe
{
    public partial class MainForm
    {
        static readonly Dictionary<string,string> BrowserFields = new Dictionary<string,string> {
            {"killSwitch","KillSwitch"},
            {"backupEnabled","BackupEnabled"},
            {"backupConfig","BackupTunnel"},
            {"config","PrimaryTunnel"},
            {"server","ActiveServer"},
            {"autoConnect","AutoConnect"},
            {"dns","DnsViaTunnel"},
            {"failover","FailoverSec"},
            {"recover","RecoverSec"},
            {"retry","RetryPrimaryMin"},
            {"ru","BypassRu"},
            {"ipv4","TunBlockIpv6"},
            {"stack","TunStack"},
            {"autoMonitor","AutoStartMonitor"},
            {"speedOn","SpeedOn"},
            {"widgetPing","WidgetPing"},
            {"widgetSpeed","WidgetSpeed"},
            {"widgetSplit","WidgetSplit"},
            {"sidePing","SidePing"},
            {"sideSpeed","SideSpeed"},
            {"speedChart","SpeedChart"},
            {"minTray","MinimizeToTray"},
            {"closeTray","CloseToTray"},
            {"widget","ShowWidget"},
            {"widgetTop","WidgetTopMost"},
            {"widgetOpacity","WidgetOpacity"},
            {"notifications","Notify"},
            {"autorun","Autorun"},
            {"startMin","StartMinimized"},
            {"systemProxy","SysProxy"},
            {"blockAds","BlockAds"},
            {"allowLan","AllowLan"},
            {"socksPort","SocksPort"},
            {"httpPort","HttpPort"},
            {"testUrl","TestUrl"},
            {"speedUrl","SpeedUrl"},
            {"speedSec","SpeedSec"},
            {"wireSock","WiresockPath"},
            {"wireLog","WiresockLogLevel"},
            {"wireAdapter","WiresockAdapter"},
            {"zapretDir","ZapretDir"},
            {"gameTcp","GameTcp"},
            {"gameUdp","GameUdp"},
            {"excludedRefresh","ExclRefreshSec"},
            {"hybridMode","HybridMode"},
            {"curlPath","CurlPath"},
            {"vpnProxy","VpnProxy"},
            {"directAdapter","DirectInterface"},
            {"probeUrl","ProbeUrl"},
            {"pollTimeout","TimeoutSec"},
            {"downAfter","DownAfterFails"},
            {"mtuHost","PingHost"},
            {"pingCount","PingCount"},
            {"testDown","DownloadMB"},
            {"testUp","UploadMB"},
            {"streams","Streams"},
            {"goodMs","GoodMs"},
            {"warnMs","WarnMs"},
            {"dnsDomains","DnsDomains"},
            {"testSites","Sites"},
            {"logDir","LogDir"},
            {"logToFile","LogToFile"},
            {"logProbes","LogProbes"},
            {"pollRate","PollRate"},
            {"ipNewOnly","IpNewOnly"},
            {"logEach","LogPolls"},
            {"asEnabled","AsEnabled"},
            {"asInterval","AsInterval"},
            {"asTimeout","AsTimeout"},
            {"asFails","AsFails"},
            {"asLatency","AsMaxLatency"},
            {"asSlowCount","AsSlowCount"},
            {"asPreTest","AsPreTest"},
            {"asBgTest","AsBgTestMin"},
            {"asReturn","AsReturnPreferred"},
            {"asReturnMin","AsReturnMin"},
            {"asCooldown","AsCooldown"},
            {"asMaxHour","AsMaxPerHour"},
            {"asSkip","AsSkipFailedMin"},
            {"asDpi","AsDpiFallback"},
            {"subTun","TunMode"},
            {"subStartup","XrayAutoConnect"},
            {"updateNotify","UpdNotify"},
            {"notifyDelayOn","AppNotifyDelayOn"},
            {"notifyDelay","AppNotifyDelayMin"},
            {"checkUpdates","CheckUpdates"},
            {"trackZapret","UpdZapret"},
            {"trackXray","UpdXray"},

            {"showAppIcons","ShowAppIcons"},
            {"bgDim","BgDim"},
            {"cardOpacity","CardOpacity"},
            {"characterWindow","CharWindow"},
            {"characterWidget","CharWidget"},
            {"charSize","CharInsideSize"},
            {"charOpacity","CharOpacity"},
            {"tgAddress","TgHost"},
            {"tgPort","TgPort"},
            {"tgSecret","TgSecret"},
            {"tgFakeTls","TgFakeTls"},
            {"tgProxyProtocol","TgProxyProto"},
            {"tgCf","TgCf"},
            {"tgH2","TgH2"},
            {"tgCustomCf","TgCfUserOn"},
            {"tgCfDomains","TgCfUser"},
            {"tgWorkerOn","TgWorkerOn"},
            {"tgWorkerDomains","TgWorker"},
            {"tgNoTls","TgNoSecure"},
            {"tgBuffer","TgBufKb"},
            {"tgPool","TgPool"},
            {"tgTestDc","TgTestDc"},
            {"tgVerbose","TgVerbose"}
        };
        static int Choice(object value,params string[] choices)
        {
            int result=Array.IndexOf(choices,Convert.ToString(value));if(result<0)throw new ArgumentException("Unknown setting value");return result;
        }
        object BrowserSetting(string key,object value)
        {
#if UI_PREVIEW
            if(new[]{"wireSock","logDir"}.Contains(key))throw new InvalidOperationException("Installation paths cannot be changed in the UI preview.");
#endif
            if(key=="errorsOnly"){chkErrOnly.Checked=Convert.ToBoolean(value);return null;}
            if(key=="routesOnly"){chkRouteOnly.Checked=Convert.ToBoolean(value);return null;}
            if(key=="logDay"){
                string day=Convert.ToString(value);DateTime date=day=="today"?DateTime.Today:day=="yesterday"?DateTime.Today.AddDays(-1):DateTime.ParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture);
                foreach(var entry in cbLogDay.Items.OfType<DayItem>())if(entry.Day.Date==date){cbLogDay.SelectedItem=entry;return null;}
                throw new ArgumentException("No log exists for that day");
            }
            if(key=="zFile"){if(!ZLists.Contains(Convert.ToString(value)))throw new ArgumentException("Unknown zapret list");return null;}
            if(key=="source"){if(TunSettingsLocked)throw new InvalidOperationException(TunLockMessage);PickSource(Convert.ToString(value)=="subscriptions"?1:0);return null;}
            if(key=="mode"){ChipClick(Choice(value,"auto","vpn","dpi"));return null;}
            if(key=="subTun"){SetTun(Convert.ToBoolean(value));return null;}
            if(key=="zapretDir"){
                string folder=Convert.ToString(value).Trim();if(folder!="")folder=Settings.ResolveDpiSourceFolder(folder);
#if UI_PREVIEW
                st.ZapretDir=folder;st.Save();LoadSettingsUi();return null;
#else
                value=folder;
#endif
            }
            var next=st.Clone();string field;
            if(key=="language"){
                if(!Tr.IsLang(Convert.ToString(value)))throw new ArgumentException("Unknown language");next.Lang=Convert.ToString(value);
            }else if(key=="theme"){
                string theme=Convert.ToString(value);next.UiCustomTheme=theme=="custom";
                if(!next.UiCustomTheme)next.Theme=Choice(theme,"dark","light","classic","chisa","kurumi");
                else next.Theme=0;
            }else if(key=="resetPalette"){
                next.UiPalette="{}";next.UiCustomTheme=true;next.Theme=0;
            }else if(key=="accent"){
                string hex=Convert.ToString(value);if(!Regex.IsMatch(hex,"^#[0-9a-fA-F]{6}$"))throw new ArgumentException("Invalid accent color");
                next.Accent=Theme.ColorStr(ColorTranslator.FromHtml(hex));
            }else if(key=="fontScale")next.UiZoom=Math.Max(1,Math.Min(1.15,Convert.ToDouble(value,CultureInfo.InvariantCulture)));
            else if(key=="font")next.UiFont=Convert.ToString(value);
            else if(key=="backgroundOpacity")next.BgDim=100-Convert.ToInt32(value);
            else if(key.StartsWith("palette.")){
                string part=key.Substring(8),hex=Convert.ToString(value);
                if(!new[]{"bg","side","panel","soft","line","field","text","muted","accent2","vpn","direct"}.Contains(part)||!Regex.IsMatch(hex,"^#[0-9a-fA-F]{6}$"))throw new ArgumentException("Invalid palette entry");
                var js=new JavaScriptSerializer();var palette=string.IsNullOrEmpty(next.UiPalette)?new Dictionary<string,string>():js.Deserialize<Dictionary<string,string>>(next.UiPalette);palette[part]=hex;next.UiPalette=js.Serialize(palette);next.UiCustomTheme=true;next.Theme=0;
            }else if(key.StartsWith("fontSize")||key.StartsWith("font")){
                bool size=key.StartsWith("fontSize");string group=key.Substring(size?8:4),prefix;
                switch(group){case "Buttons":prefix="FontBtn";break;case "Large":prefix="FontLarge";break;case "Medium":prefix="FontMed";break;case "Small":prefix="FontSmall";break;default:throw new ArgumentException("Unknown font group");}
                var property=typeof(Settings).GetField(prefix+(size?"Size":"Family"));
                double factor=group=="Large"?20.0/11:group=="Medium"?11.0/9:group=="Small"?9.0/8:1;
                property.SetValue(next,size?(object)(Convert.ToDouble(value,CultureInfo.InvariantCulture)/factor):Convert.ToString(value));
            }else if(key.StartsWith("visible.")){
                string suffix=key.Substring(8);string target=suffix=="vpn"?"SideVpn":suffix=="direct"?"SideDirect":suffix=="download"?"SideDownload":suffix=="upload"?"SideUpload":null;
                if(target==null)throw new ArgumentException("Unknown indicator");typeof(Settings).GetField(target).SetValue(next,Convert.ToBoolean(value));
                next.SidePing=next.SideVpn||next.SideDirect;next.SideSpeed=next.SideDownload||next.SideUpload;
            }else if(key=="probeFamily")next.ProbeIpFamily=Choice(value,"IPv4","IPv6","Auto");
            else if(key=="directMethod")next.DirectMode=Choice(value,"curl","powershell");
            else if(key=="asPool")next.AsPool=Choice(value,"all","group","favorites");
            else if(key=="asStrategy")next.AsStrategy=Choice(value,"fastest","next","random");
            else if(key=="gameFilter")next.GameFilter=Choice(value,"off","both","tcp","udp");
            else if(key=="charPlacement")next.CharFrame=Convert.ToString(value)=="frame";
            else if(key=="charAnchor")next.CharInsideAnchor=Choice(value,"bottom-left","bottom-right","top-right","top-left","center");
            else if(key.StartsWith("charHost")){int index;if(!int.TryParse(key.Substring(8),out index)||index<0||index>=CharOverlay.Slots.Length)throw new ArgumentException("Unknown character slot");SetSlotHost(CharOverlay.Slots[index],Convert.ToString(value)=="widget");UpdateChars();return null;}
            else if(key=="tg"){next.TgOn=Convert.ToBoolean(value);}
            else if(key=="tgDc")next.TgDcIp=Convert.ToString(value);
            else if(key=="dpiParallel"){router.SetManualDpiParallel(Convert.ToBoolean(value));next.DpiButton=Convert.ToBoolean(value)?0:2;}
            else if(key=="logRetention")next.LogRetention=Choice(value,"1 hour","1 day","1 week","1 month");
            else if(key=="notifyEvery")next.AppNotifyEvery=Choice(value,"Immediately","Day","Week","Month");
            else if(key=="coreUpdateEvery")next.UpdEvery=Choice(value,"At startup","Every 6 hours","Every 12 hours","Daily","Weekly");
            else if(BrowserFields.TryGetValue(key,out field)){
                var property=typeof(Settings).GetField(field,BindingFlags.Public|BindingFlags.Instance);
                if(property==null)throw new ArgumentException("Unsupported setting: "+key);
                if(new[]{"TunStack","TunBlockIpv6","DnsViaTunnel","BypassRu","PrimaryTunnel","BackupTunnel"}.Contains(field)&&TunSettingsLocked)throw new InvalidOperationException(TunLockMessage);
                if((field=="PrimaryTunnel"||field=="BackupTunnel")&&!Tunnels.Exists(Convert.ToString(value)))throw new ArgumentException("Unknown tunnel");
                property.SetValue(next,Convert.ChangeType(value,property.FieldType,CultureInfo.InvariantCulture));
            }else throw new ArgumentException("Unknown setting: "+key);
            if(key=="theme"||key=="accent"||key=="resetPalette"||key.StartsWith("palette.")){
                next.Clamp();st.CopyFrom(next);st.Save();Theme.AccentColor=Theme.ParseColor(st.Accent);Theme.Apply((ThemeKind)st.Theme);
                UpdateChars();widget?.Invalidate();widget2?.Invalidate();browser.BackColor=Theme.Bg;return null;
            }
            bool fonts=key.StartsWith("font"),appearance=fonts;
            if(key=="hybridMode"&&next.HybridMode&&!st.HybridMode&&!ConfirmHybrid())return null;
            if(key=="server"&&ServerStore.Find(next.ActiveServer)==null)throw new ArgumentException("Unknown server");
            bool proxyChanged=next.SysProxy!=st.SysProxy;
            ApplySettings(next);
            if(fonts){PushFonts();Theme.MakeFonts();}
            if(appearance||key=="language"){Tr.Set(st.Lang);Theme.AccentColor=Theme.ParseColor(st.Accent);Theme.Apply((ThemeKind)st.Theme);RebuildUi();}
            if(proxyChanged&&xm.Active&&!st.TunMode){if(st.SysProxy)SysProxy.Set("127.0.0.1:"+st.HttpPort);else SysProxy.Clear();}
#if !UI_PREVIEW
            if(!st.KillSwitch&&KillSwitch.NeedsClear)System.Threading.ThreadPool.QueueUserWorkItem(_=>KillSwitch.Clear());
#endif
            if(key=="tg"||key.StartsWith("tg")){TgApplySoon();TgUpdateUi();}
            if(key.StartsWith("character")||key.StartsWith("char")){UpdateChars();ResetPageBackground();}
            if(key.StartsWith("visible.")){miniMon.RowMask=SideRowMask;miniMon.SetParts(st.SidePing&&MonOn,st.SideSpeedOn);laySide?.Invoke();}
            LoadSettingsUi();return null;
        }
    }
}
#endif
