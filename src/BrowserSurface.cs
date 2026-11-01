#if WEB_UI
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NetProbe
{
    public sealed class BrowserSurface : Panel
    {
        readonly WebView2 view;
        readonly UiLoadingPanel loading;
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
        public event Action<Dictionary<string, object>> Command;
        public event Action Ready;
        public bool Loaded { get; private set; }
        public int DocumentNavigations { get; private set; }
        public string Diagnostics = "created";
        string lastNavigation;
        const string DocumentUri = "https://routex.internal/interface";
        MemoryStream documentStream;
        bool IsDocument(string uri) { return uri == "about:blank" || uri == DocumentUri; }
        public bool BackgroundStartup { get; set; }
        bool transitionReady,revealStarted;
        static bool loaderConfigured;
        async Task RevealWhenVisibleAsync()
        {
            if(!transitionReady||revealStarted||(!Visible&&!BackgroundStartup))return;
            revealStarted=true;loading.Visible=false;
            await view.CoreWebView2.ExecuteScriptAsync("window.routexBeginReveal&&window.routexBeginReveal("+(BackgroundStartup&&!Visible?"true":"false")+")");
        }
        public bool Revealed { get { return revealStarted; } }
        // Startup watchdog: never leave the loading screen up forever (GPU / occlusion stalls,
        // a script error, a lost message). Removes both loading layers and shows the page as is.
        public async Task ForceRevealAsync()
        {
            transitionReady=true;revealStarted=true;loading.Visible=false;
            if(Loaded&&!IsDisposed)await view.CoreWebView2.ExecuteScriptAsync("(()=>{try{window.routexBeginReveal&&window.routexBeginReveal(true);}catch(e){}const o=document.getElementById('rx-startup-overlay');if(o)o.remove();window.routexStartupSettled=true;})()");
        }
        public async Task<string> ScriptStateAsync()
        {
            if(!Loaded||IsDisposed)return "not loaded";
            return await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({ready:document.readyState,prepare:typeof window.routexPrepareReveal,settled:!!window.routexStartupSettled,uiError:window.ROUTEX_UI_ERROR||'',bootError:window.ROUTEX_BOOT_ERROR||'',hidden:document.hidden})");
        }
        public BrowserSurface()
        {
            Dock = DockStyle.Fill; BackColor = Theme.Bg;
            view = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Theme.Bg, AllowExternalDrop = false };
            Controls.Add(view);
            loading=new UiLoadingPanel();Controls.Add(loading);loading.BringToFront();
            VisibleChanged+=async(s,e)=>{try{await RevealWhenVisibleAsync();}catch(Exception ex){Logger.Write(LogLevel.Warn,"Interface reveal: "+ex.Message);}};
        }
        public async Task InitializeAsync(object snapshot)
        {
            UiStartupTiming.Mark("WebView initialization");
            var controlHandle=view.Handle; // A minimized launch still needs a real child HWND.
            if(!loaderConfigured){CoreWebView2Environment.SetLoaderDllFolderPath(EmbeddedBrowser.LoaderDirectory());loaderConfigured=true;}
            var options = new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update");
            Diagnostics="creating environment";
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Settings.Sub("runtime"), "webview-profile"), options);
            UiStartupTiming.Mark("WebView environment ready");Diagnostics="environment ready";
            Diagnostics="creating control";
            await view.EnsureCoreWebView2Async(environment);
            UiStartupTiming.Mark("WebView control ready");Diagnostics="control ready";
            var core = view.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            await core.AddScriptToExecuteOnDocumentCreatedAsync("window.addEventListener('error',event=>window.ROUTEX_BOOT_ERROR=event.message)");
            core.NavigationStarting += (s, e) => {
                bool allowed=IsDocument(e.Uri)&&!(e.Uri==DocumentUri&&DocumentNavigations>0);
                lastNavigation=allowed?"embedded document":"blocked document";Diagnostics="navigation: "+lastNavigation;
                if(!allowed){e.Cancel=true;return;}
                if(e.Uri==DocumentUri)DocumentNavigations++;
            };
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.PermissionRequested += (s, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (s, e) => e.Cancel = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (s, e) => {
                if (e.Request.Uri == DocumentUri) {
                    documentStream.Position = 0;
                    e.Response = environment.CreateWebResourceResponse(documentStream,200,"OK","Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
                } else {
                    e.Response = environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked","Content-Type: text/plain");
                }
            };
            core.WebMessageReceived += async (s, e) => {
                if (!IsDocument(e.Source) || !IsDocument(core.Source)) return;
                try {
                    var command = json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                    if(command!=null&&command.ContainsKey("command")&&Convert.ToString(command["command"])=="reveal-ready"){
                        transitionReady=true;await RevealWhenVisibleAsync();return;
                    }
                    if (command != null && command.ContainsKey("command")) Command?.Invoke(command);
                } catch (Exception ex) { Logger.Write(LogLevel.Error, "Interface command: " + ex.Message); }
            };
            core.NavigationCompleted += async (s, e) => {
                Diagnostics = "navigation completed: " + e.IsSuccess + " " + e.WebErrorStatus + " uri=" + lastNavigation;
                if(e.IsSuccess&&core.Source==DocumentUri){Loaded=true;
                    try{
                        string prepared=await core.ExecuteScriptAsync("typeof window.routexPrepareReveal==='function'?(window.routexPrepareReveal("+(BackgroundStartup?"true":"false")+"),'ok'):'missing'");
                        if(prepared!="\"ok\""){Logger.Write(LogLevel.Warn,"Interface script did not start ("+await ScriptStateAsync()+"), showing it without the transition");transitionReady=true;await RevealWhenVisibleAsync();}
                    }catch(Exception ex){transitionReady=true;Logger.Write(LogLevel.Warn,"Interface transition: "+ex.Message);try{await RevealWhenVisibleAsync();}catch{}}
                    Ready?.Invoke();
                }
            };
            string html = EmbeddedBrowser.Html().Replace("ROUTEX_HOST_INITIAL_JSON", json.Serialize(snapshot).Replace("<", "\\u003c"));
            documentStream = new MemoryStream(Encoding.UTF8.GetBytes(html),false);
            core.Navigate(DocumentUri);
        }
        public void UpdateSnapshot(object value)
        {
            if (Loaded && !IsDisposed) view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new { kind = "snapshot", value }));
        }
        public void Reply(int id, object value, string error = null)
        {
            if (Loaded && !IsDisposed) view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new { kind = "reply", id, value, error }));
        }
        public Task<string> EvaluateAsync(string script) { return view.CoreWebView2.ExecuteScriptAsync(script); }
        public async Task ActivateInterfaceAsync(){if(Loaded){await RevealWhenVisibleAsync();await view.CoreWebView2.ExecuteScriptAsync("window.routexActivate&&window.routexActivate()");}}
        public async Task CaptureAsync(string path)
        {
            using (var stream = File.Create(path)) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        }
        public Task CaptureWidgetAsync(string path){return CaptureElementAsync(path,".r-widget:not(.r-speed-widget)");}
        public async Task CaptureElementAsync(string path,string selector)
        {
            string result=await EvaluateAsync("(()=>{const r=document.querySelector("+json.Serialize(selector)+").getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height,viewport:innerWidth};})()");
            var bounds=json.Deserialize<Dictionary<string,double>>(result);
            using(var data=new MemoryStream()){
                await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,data);data.Position=0;
                using(var bitmap=new System.Drawing.Bitmap(data)){
                    double scale=bitmap.Width/bounds["viewport"];
                    var rect=System.Drawing.Rectangle.Intersect(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),new System.Drawing.Rectangle((int)(bounds["x"]*scale)-10,(int)(bounds["y"]*scale)-10,(int)Math.Ceiling(bounds["width"]*scale)+20,(int)Math.Ceiling(bounds["height"]*scale)+20));
                    using(var crop=bitmap.Clone(rect,bitmap.PixelFormat))crop.Save(path,System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { view.Dispose(); documentStream?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
#endif
