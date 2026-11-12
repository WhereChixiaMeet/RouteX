#if WEB_UI
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static NetProbe.Tr;

namespace NetProbe
{
    public partial class MainForm
    {
        ServerEntry BrowserSelectedServer(Dictionary<string,object> args)
        {
            string id=BrowserString(args,"id");if(string.IsNullOrEmpty(id))id=BrowserString(args,"selectedServer");
            var server=ServerStore.Find(id);if(server==null)throw new ArgumentException("Select a server first");return server;
        }
        string BrowserSelectedTunnel(Dictionary<string,object> args)
        {
            string name=BrowserString(args,"selectedConfig");if(!Tunnels.Exists(name))throw new ArgumentException("Select a tunnel first");return name;
        }
        IEnumerable<Control> BrowserControls(Control root)
        {
            foreach(Control child in root.Controls){yield return child;foreach(var descendant in BrowserControls(child))yield return descendant;}
        }
        void BrowserNativeButton(int page,string text=null,string glyph=null)
        {
            SelectPage(page);
            var button=BrowserControls(pages[page]).OfType<FlatButton>().FirstOrDefault(b=>(text==null||b.Text==Tx(text))&&(glyph==null||b.Glyph==glyph));
            if(button==null)throw new InvalidOperationException("Control unavailable: "+text);
            button.PerformClick2();
        }
        object BrowserAction(string action,Dictionary<string,object> args)
        {
            string id=BrowserString(args,"id");
#if UI_PREVIEW
            if(action=="connect-selected"){var selected=BrowserSelectedServer(args);if(!st.HybridMode&&TunnelCount>0){NotifySubscriptionPriority();return null;}st.ActiveServer=selected.Id;st.Source=1;st.Save();previewVpn=true;return null;}
            if(action=="monitor-toggle"){previewMonitoring=!previewMonitoring;return null;}
            if(action=="show-widget"||action=="hide-widget"){st.ShowWidget=action=="show-widget";st.Save();return null;}
            if(new[]{"install-core","update-subs","test-servers","speed-servers","test-server","speed-server","run-test","check-sites","demo-service","restart-tg","tg-test","check-update"}.Contains(action))throw new InvalidOperationException("This action is disabled in the UI preview. Your running VPN is unchanged.");
#endif
            switch(action){
                case "read-clipboard":return SafeClip();
                case "route-inspect":ShowRoute(null,null);return null;
                case "server-paste":case "server-import":{
                    string text=BrowserString(args,"text",SafeClip());var parsed=SubParse.Parse(text);
                    int count=ServerStore.AddMany(parsed.Servers,"");FillServers();return new {added=count};}
                case "manage-subs":BrowserNativeButton(PgServers,"Подписки");return null;
                case "update-subs":UpdateSubs(null);return null;
                case "test-servers":TestServers(ServerStore.Snapshot());return null;
                case "speed-servers":SpeedServers(ServerStore.Snapshot());return null;
                case "test-server":TestServers(new List<ServerEntry>{BrowserSelectedServer(args)});return null;
                case "speed-server":SpeedServers(new List<ServerEntry>{BrowserSelectedServer(args)});return null;
                case "connect-selected":{var server=BrowserSelectedServer(args);if(!st.HybridMode&&TunnelCount>0){NotifySubscriptionPriority();return null;}st.Source=1;st.ActiveServer=server.Id;st.Save();ConnectCurrent(true);UpdateModeUi();return null;}
                case "favorite":case "favorite-selected":{var server=BrowserSelectedServer(args);server.Favorite=!server.Favorite;ServerStore.Save();FillServers();return null;}
                case "copy-server":Clipboard.SetText(BrowserSelectedServer(args).Link);return null;
                case "delete-server":ServerStore.Remove(new[]{BrowserSelectedServer(args).Id});FillServers();return null;
                case "reset-server-traffic":{var server=BrowserSelectedServer(args);server.TotalDown=server.TotalUp=0;ServerStore.Save();FillServers();return null;}
                case "sort-all":SortAllLikeThis();return null;
                case "reset-table":ResetSrvColumns();return null;
                case "new-tunnel":case "add-config":{
                    string name="tunnel";int count=2;while(Tunnels.Exists(name))name="tunnel "+count++;
                    Tunnels.Write(name,"[Interface]\r\nPrivateKey = \r\nAddress = \r\nMTU = 1420\r\n\r\n[Peer]\r\nPublicKey = \r\nEndpoint = \r\nAllowedIPs = 0.0.0.0/0, ::/0\r\nPersistentKeepalive = 25\r\n");FillTunnels(name);return new {selectedConfig=name};}
                case "new-link-tunnel":NewTunnelFromLink();return new {selectedConfig=lbTun.SelectedItem};
                case "config-from-link":{
                    // vpn:// (Amnezia) gives an AmneziaWG tunnel, the other links work as before
                    string linkErr;var entry=TunnelProto.FromLink(BrowserString(args,"link"),out linkErr);if(entry==null)throw new ArgumentException(linkErr??"Invalid VLESS / VMess / Trojan link");
                    string name=BrowserString(args,"selectedConfig");
                    if(Tunnels.Exists(name)){
                        // "From link" on a WireGuard tunnel wrote <name>.json next to <name>.conf: the .conf wins, nothing changed and nothing was said
                        if(!TunnelProto.IsXray(name))throw new ArgumentException("This tunnel is WireGuard. Use \"New tunnel from link\" for a VLESS / VMess / Trojan / vpn:// link.");
                    }else{
                        // a new tunnel never replaces an existing one with the same name
                        string wanted=Tunnels.Safe(entry.Name==""?"Imported config":entry.Name);if(wanted=="")wanted="Imported config";
                        name=wanted;int copy=2;while(Tunnels.Exists(name))name=wanted+" "+copy++;
                    }
                    if(entry.Protocol=="awg"){Logger.Write(LogLevel.Info,"Tunnel \""+name+"\" added from an Amnezia key: AmneziaWG · "+entry.Endpoint);MihomoCore.EnsureAsync(st);}
                    TunnelProto.Save(name,entry);FillTunnels(name);LoadConnectUi();return new {selectedConfig=name};}
                case "fill-config-link":{
                    string name=BrowserSelectedTunnel(args);lbTun.SelectedItem=name;XrFromLink(SafeClip());return null;}
                case "import-conf":case "import-config":ImportConf();return new {selectedConfig=lbTun.SelectedItem};
                case "delete-config":DeleteTunnel(BrowserSelectedTunnel(args));return new {selectedConfig=lbTun.SelectedItem};
                case "add-dpi":CreateStrategy("new","--wf-tcp=80,443 --wf-udp=443\r\n--filter-tcp=443 --dpi-desync=multisplit");return new {selectedDpi=lbStrat.SelectedItem};
                case "copy-dpi":{string name=BrowserString(args,"selectedDpi");if(!DpiConfigs.Names().Contains(name))throw new ArgumentException("Select a strategy first");CreateStrategy(name+" (copy)",DpiConfigs.Read(name));return new {selectedDpi=lbStrat.SelectedItem};}
                case "delete-dpi":{string name=BrowserString(args,"selectedDpi");if(!DpiConfigs.Names().Contains(name))throw new ArgumentException("Select a strategy first");DpiConfigs.Delete(name);StrategyDeleted(name);FillStrategies(null);return new {selectedDpi=lbStrat.SelectedItem};}
                case "activate-dpi":st.DpiStrategy=BrowserString(args,"selectedDpi");st.Save();if(router.Dpi.Running)router.RestartDpi();FillStrategies(st.DpiStrategy);return null;
                case "dpi-rename":{string old=BrowserString(args,"selectedDpi"),renamed=Tunnels.Safe(BrowserString(args,"text"));if(!DpiConfigs.Names().Contains(old)||renamed=="")throw new ArgumentException("Unknown strategy or empty name");if(renamed!=old&&DpiConfigs.Names().Contains(renamed))throw new ArgumentException("Strategy already exists");lbStrat.SelectedItem=old;tbStratName.Text=renamed;RenameStrategy();return new {selectedDpi=renamed};}
                case "import-bat":BrowserNativeButton(PgDpi,null,"\uE8E5");return null;
                case "import-zapret":BrowserNativeButton(PgDpi,null,"\uE896");return null;
                case "dpi-command":return "\""+Path.Combine(st.ZapretPath,"bin","winws.exe")+"\" "+ZapretRunner.BuildArgs(st,DpiConfigs.Read(BrowserString(args,"selectedDpi")));
                case "dpi-read-list":if(!ZLists.Contains(id))throw new ArgumentException("Unknown zapret list");return File.Exists(Path.Combine(AppLists.ZapretLists,id))?File.ReadAllText(Path.Combine(AppLists.ZapretLists,id)):"";
                case "dpi-write-list":if(!ZLists.Contains(id))throw new ArgumentException("Unknown zapret list");Directory.CreateDirectory(AppLists.ZapretLists);File.WriteAllText(Path.Combine(AppLists.ZapretLists,id),BrowserString(args,"text"));return null;
                case "dpi-sites":if(id=="dpiDomains")AppLists.Set(AppLists.DpiDomains,AppLists.ParseClipboard(BrowserString(args,"text")));else if(id=="dpiNoTouch")AppLists.SetDpiSitesExcl(AppLists.ParseClipboard(BrowserString(args,"text")));else throw new ArgumentException("Unknown DPI site list");return null;
                case "monitor-toggle":if(MonOn)StopMonitoring();else StartMonitoring();return null;
                case "reset-monitor":monVpn.Stats.Reset();monDir.Stats.Reset();chart.Clear();return null;
                case "monitor-period":LoadPeriod(id=="Day"?"d":id=="Week"?"w":"s");return null;
                case "show-widget":st.ShowWidget=true;st.Save();ApplyMonitorParts();widget.Show();UpdateChars();return null;
                case "hide-widget":st.ShowWidget=false;st.Save();widget.Hide();widget2?.Hide();return null;
                case "run-test":{
                    var keys=new[]{"full","ping","dns","down","up","tls","mtu","ip"};int test=Array.IndexOf(keys,id);if(test<0)throw new ArgumentException("Unknown test");
                    chkTVpn.Checked=!args.ContainsKey("vpn")||Convert.ToBoolean(args["vpn"]);chkTDir.Checked=!args.ContainsKey("direct")||Convert.ToBoolean(args["direct"]);
                    testButtons[test].PerformClick2();return null;}
                case "stop-tests":btnStop.PerformClick2();return null;
                case "clear-tests":grid.Rows.Clear();rows.Clear();return null;
                case "copy-tests":Clipboard.SetText(GridText(grid,"\t"));return null;
                case "export-tests":ExportCsv(grid,"routex-tests");return null;
                case "test-history":BrowserNativeButton(PgTests,"История");return null;
                case "check-sites":btnSites.PerformClick2();return null;
                case "stop-site-check":btnSitesStop.PerformClick2();return null;
                case "sites-save":{var next=st.Clone();next.Sites=BrowserString(args,"text");ApplySettings(next);LoadSettingsUi();return null;}
                case "export-sites":ExportCsv(siteGrid,"routex-sites");return null;
                case "clear-logs":logItems.Clear();RedrawLog();return null;
                case "copy-logs":Clipboard.SetText(logBox.Text);return null;
                case "save-logs":BrowserNativeButton(PgLog,"Сохранить…");return null;
                case "change-log-folder":ChangeLogDir();return null;
                case "default-log-folder":SetLogDir("");return null;
                case "refresh-adapters":RefreshAdapters(false);return null;
                case "demo-folder":case "open-folder":{
                    string path=id=="log"?st.LogPath:id=="zapret"?st.ZapretPath:id=="probes"?Path.Combine(CurlProber.WorkDir,"bin"):Settings.Dir;
                    if(Directory.Exists(path))Process.Start("explorer.exe","\""+path+"\"");return null;}
                case "pick-dpi-folder":{
                    using(var dialog=new FolderBrowserDialog{Description="Select the DPI source folder",SelectedPath=st.ZapretPath,ShowNewFolderButton=true})if(dialog.ShowDialog(this)==DialogResult.OK)BrowserSetting("zapretDir",dialog.SelectedPath);return null;
                }
                case "default-dpi-folder":return BrowserSetting("zapretDir","");
                case "check-update":
                    if(id=="zapret")UpdateZapretUi(new FlatButton(Tx("Обновить zapret")));
                    else if(id=="xray")UpdateCore(true);
                    else if(id=="singbox")UpdateSingBoxUi(true);
                    else if(id=="app"){if(appRelease!=null)DoAppUpdate();else CheckAppUpdate(true);}
                    else{CheckUpdates(true);CheckAppUpdate(false);}
                    return null;
                case "install-core":if(id=="xray")UpdateCore(true);else if(id=="singbox")UpdateSingBoxUi(true);else if(id=="zapret")BrowserNativeButton(PgSettings,"Обновить zapret");else throw new ArgumentException("Unknown core");return null;
                case "export-theme":{
                    using(var dialog=new SaveFileDialog{Filter="JSON (*.json)|*.json",FileName="routex-theme.json"})if(dialog.ShowDialog(this)==DialogResult.OK){
                        var js=new System.Web.Script.Serialization.JavaScriptSerializer();File.WriteAllText(dialog.FileName,js.Serialize(new {name="Custom",palette=args.ContainsKey("palette")?args["palette"]:new object(),accent=BrowserString(args,"accent")}),new System.Text.UTF8Encoding(false));
                    }return null;}
                case "import-theme":{
                    using(var dialog=new OpenFileDialog{Filter="JSON (*.json)|*.json"})if(dialog.ShowDialog(this)==DialogResult.OK){
                        if(new FileInfo(dialog.FileName).Length>65536)throw new ArgumentException("Theme file is too large");
                        var js=new System.Web.Script.Serialization.JavaScriptSerializer();var data=js.Deserialize<Dictionary<string,object>>(File.ReadAllText(dialog.FileName));
                        object palette;if(!data.TryGetValue("palette",out palette))throw new ArgumentException("Theme has no palette");
                        var entries=palette as Dictionary<string,object>;if(entries==null)throw new ArgumentException("Invalid theme palette");
                        var validated=new Dictionary<string,string>();foreach(var pair in entries)if(new[]{"bg","side","panel","soft","line","field","text","muted","accent2","vpn","direct"}.Contains(pair.Key)&&System.Text.RegularExpressions.Regex.IsMatch(Convert.ToString(pair.Value),"^#[0-9a-fA-F]{6}$"))validated[pair.Key]=Convert.ToString(pair.Value);
                        var next=st.Clone();next.UiPalette=js.Serialize(validated);next.UiCustomTheme=true;next.Theme=0;ApplySettings(next);
                        if(data.ContainsKey("accent"))BrowserSetting("accent",data["accent"]);
                    }return null;}
                case "export-demo":BrowserNativeButton(PgSettings,"Экспорт");return null;
                case "export-settings":BrowserNativeButton(PgSettings,"Экспорт");return null;
                case "import-settings":if(TunSettingsLocked)throw new InvalidOperationException(TunLockMessage);BrowserNativeButton(PgSettings,"Импорт");return null;
                case "entry-icon":{
                    string item=BrowserString(args,"value");if(item==ListCard.BuiltIn)throw new ArgumentException("The built-in checker uses the app icon");
                    if(!AppLists.Get(BrowserListKey(BrowserString(args,"list"))).Contains(item))throw new ArgumentException("Entry not found");
                    using(var dialog=new OpenFileDialog{Filter="Icons and applications (*.exe;*.ico;*.png;*.jpg;*.bmp)|*.exe;*.ico;*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)AppIcons.SetCustom(item,dialog.FileName);return null;
                }
                case "entry-icon-reset":{string item=BrowserString(args,"value");if(item==ListCard.BuiltIn)throw new ArgumentException("The built-in checker uses the app icon");AppIcons.SetCustom(item,null);return null;}
                case "entry-path":return AppIcons.PathOf(BrowserString(args,"value"))??"";
                case "entry-location":{string path=AppIcons.PathOf(BrowserString(args,"value"));if(path==null)throw new InvalidOperationException("Executable path is not available");Process.Start("explorer.exe","/select,\""+path+"\"");return null;}
                case "entry-route":ShowRouteFor(BrowserString(args,"value"));return null;
                case "reset-fonts":{var next=st.Clone();next.FontBtnFamily=next.FontLargeFamily=next.FontMedFamily=next.FontSmallFamily="Segoe UI";next.FontBtnSize=9;next.FontLargeSize=11;next.FontMedSize=9;next.FontSmallSize=8;ApplySettings(next);PushFonts();Theme.MakeFonts();RebuildUi();return null;}
                case "choose-background":return BrowserPickImage("background");
                case "remove-background":st.BgImage="";st.Save();ResetPageBackground();return null;
                case "app-icon-pick":return BrowserPickImage("icon");
                case "reset-app-icon":st.AppIconPath="";st.Save();ApplyAppIcon();return null;
                case "character-pick":return BrowserPickImage("character",id);
                case "character-inside-pick":return BrowserPickImage("inside");
                case "character-delete":{int n=int.Parse(id);if(n<0||n>=CharOverlay.Slots.Length)throw new ArgumentException("Unknown slot");string path=Path.Combine(CharOverlay.Folder(Theme.Kind),CharOverlay.Slots[n]+".png");if(File.Exists(path))File.Delete(path);UpdateChars();return null;}
                case "character-inside-delete":{string path=Path.Combine(CharOverlay.Folder(Theme.Kind),"window.png");if(File.Exists(path))File.Delete(path);ResetPageBackground();UpdateChars();return null;}
                case "character-edit":SetCharEdit(!charEdit);return null;
                case "character-reset":foreach(var slot in CharOverlay.Slots)st.SetCharLayout((Theme.Kurumi?"kurumi.":"chisa.")+slot,null);st.Save();UpdateChars();return null;
                case "tg-new-secret":st.TgSecret=TgConst.NewSecret();st.Save();TgApplySoon();TgUpdateUi();return null;
                case "tg-default-dc":st.TgDcIp=TgConst.DefaultDcIp;st.Save();TgApplySoon();return null;
                case "restart-tg":TgRestart();return null;
                case "copy-tg":if(tgpLink.Text!="")Clipboard.SetText(tgpLink.Text);return null;
                case "open-tg":TgOpenInTelegram();return null;
                case "tg-test":BrowserNativeButton(PgTg,id=="dc"?"Проверить":id=="cf"?"Проверить CF-прокси":"Проверить Worker");return null;
                default:throw new ArgumentException("Unknown action: "+action);
            }
        }
        object BrowserPickImage(string target,string slot=null)
        {
            using(var dialog=new OpenFileDialog{Filter=target=="character"||target=="inside"?"PNG (*.png)|*.png":target=="background"?"Images (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif":"Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico"}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return null;
                if(target=="background"){if(BrowserImage(dialog.FileName,true)=="")throw new InvalidOperationException("This image could not be read. Use PNG, JPG or WebP up to 6 MB.");st.BgImage=dialog.FileName;st.Save();ResetPageBackground();}
                else if(target=="icon"){st.AppIconPath=dialog.FileName;st.Save();ApplyAppIcon();UpdateTray();}
                else if(target=="inside"){Directory.CreateDirectory(CharOverlay.Folder(Theme.Kind));File.Copy(dialog.FileName,Path.Combine(CharOverlay.Folder(Theme.Kind),"window.png"),true);ResetPageBackground();UpdateChars();}
                else{int index=int.Parse(slot);if(index<0||index>=CharOverlay.Slots.Length)throw new ArgumentException("Unknown slot");SetCharPicture(CharOverlay.Slots[index],dialog.FileName);}
                return null;
            }
        }
        object BrowserEditConfig(string name,Dictionary<string,object> args)
        {
            if(!Tunnels.Exists(name))throw new ArgumentException("Unknown tunnel");string field=BrowserString(args,"field"),text=BrowserString(args,"value");
            if(field=="name"){
                string renamed=Tunnels.Safe(text);if(renamed==""||Tunnels.Exists(renamed)&&renamed!=name)throw new ArgumentException("Tunnel name is empty or already exists");
                lbTun.SelectedItem=name;tbTunName.Text=renamed;RenameTunnel();return new {selectedConfig=renamed};
            }
            if(field=="protocol"){lbTun.SelectedItem=name;SwitchProto(text.ToLowerInvariant());return null;}
            if(field=="raw"&&TunnelProto.IsAwg(name)){
                // AmneziaWG: the same text editor; the AWG parameters stay as written
                var awgEntry=TunnelProto.Load(name);string awgErr=null;
                if(awgEntry==null||!AmneziaLink.SetText(awgEntry,text,out awgErr))throw new ArgumentException(awgEntry==null?"Unknown tunnel protocol":awgErr);
                TunnelProto.Save(name,awgEntry);return null;
            }
            if(field=="raw"){
                if(TunnelProto.IsXray(name))throw new ArgumentException("Text editor is for WireGuard");
                if(text.IndexOf("[Interface]",StringComparison.OrdinalIgnoreCase)<0||text.IndexOf("[Peer]",StringComparison.OrdinalIgnoreCase)<0)throw new ArgumentException("WireGuard requires [Interface] and [Peer]");Tunnels.Write(name,text);return null;
            }
            var entry=TunnelProto.Load(name);if(entry==null)throw new ArgumentException("Unknown tunnel protocol");
            if(entry.Protocol=="awg")throw new ArgumentException("AmneziaWG tunnels are edited as text");
            var fields=new Dictionary<string,string>{{"address","Address"},{"port","Port"},{"uuid","UserId"},{"flow","Flow"},{"cipher","Method"},{"security","Security"},{"sni","Sni"},{"fingerprint","Fp"},{"publicKey","Pbk"},{"shortId","Sid"},{"network","Network"},{"path","Path"},{"host","Host"},{"alpn","Alpn"},{"spx","Spx"},{"xhttpMode","Mode"},{"insecure","Insecure"}};
            string property;if(!fields.TryGetValue(field,out property))throw new ArgumentException("Unknown config field");
            if(field=="port"){int port;if(!int.TryParse(text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out port)||port<1||port>65535)throw new ArgumentException("Port must be a number from 1 to 65535");args["value"]=port;}
            var info=typeof(ServerEntry).GetField(property);info.SetValue(entry,Convert.ChangeType(args["value"],info.FieldType,CultureInfo.InvariantCulture));
            if(field=="path"&&entry.Network=="grpc")entry.ServiceName=text;
            TunnelProto.Save(name,entry);FillTunnels(name);LoadConnectUi();return null;
        }
    }
}
#endif
