#if WEB_UI
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
namespace NetProbe
{
    public partial class MainForm
    {
        readonly Dictionary<string,Tuple<DateTime,string>> browserImages=new Dictionary<string,Tuple<DateTime,string>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Tuple<Bitmap,string>> browserEntryImages=new Dictionary<string,Tuple<Bitmap,string>>(StringComparer.OrdinalIgnoreCase);
        string BrowserEntryIcon(string entry){
            if(!st.ShowAppIcons||entry==ListCard.BuiltIn)return "";var bitmap=AppIcons.Get(entry,this);if(bitmap==null)return "";
            Tuple<Bitmap,string> cached;if(browserEntryImages.TryGetValue(entry,out cached)&&ReferenceEquals(cached.Item1,bitmap))return cached.Item2;
            using(var data=new MemoryStream()){bitmap.Save(data,ImageFormat.Png);string value="data:image/png;base64,"+Convert.ToBase64String(data.ToArray());browserEntryImages[entry]=Tuple.Create(bitmap,value);return value;}
        }
        string BrowserImage(string path,bool background=false)
        {
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return "";
            DateTime modified=File.GetLastWriteTimeUtc(path);Tuple<DateTime,string> cached;
            if(browserImages.TryGetValue(path,out cached)&&cached.Item1==modified)return cached.Item2;
            try{
                using(var source=Image.FromFile(path)){
                    double scale=Math.Min(1,(background?1920.0:1024.0)/Math.Max(source.Width,source.Height));
                    using(var image=new Bitmap(source,Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale))))
                    using(var data=new MemoryStream()){
                        image.Save(data,ImageFormat.Png);
                        byte[] bytes=data.ToArray();int width=image.Width,height=image.Height;
                        while(bytes.Length>(background?3*1024*1024:768*1024)&&width>256&&height>256){width=width*3/4;height=height*3/4;using(var smaller=new Bitmap(source,width,height))using(var buffer=new MemoryStream()){smaller.Save(buffer,ImageFormat.Png);bytes=buffer.ToArray();}}
                        string encoded="data:image/png;base64,"+Convert.ToBase64String(bytes);browserImages[path]=Tuple.Create(modified,encoded);return encoded;
                    }
                }
            }catch{
                // GDI+ cannot decode WebP (and some JPEG variants): hand the original file to the browser, which can.
                try{
                    string mime;switch(Path.GetExtension(path).ToLowerInvariant()){case ".webp":mime="image/webp";break;case ".jpg":case ".jpeg":mime="image/jpeg";break;case ".png":mime="image/png";break;case ".gif":mime="image/gif";break;case ".bmp":mime="image/bmp";break;default:return "";}
                    var info=new FileInfo(path);if(info.Length>(background?6*1024*1024:1024*1024))return "";
                    string raw="data:"+mime+";base64,"+Convert.ToBase64String(File.ReadAllBytes(path));browserImages[path]=Tuple.Create(modified,raw);return raw;
                }catch{return "";}
            }
        }
        // Sidebar / brand logo of the web interface: the custom application icon, if one is set.
        string browserIconKey,browserIconData;
        string BrowserAppIcon()
        {
            string path=st.AppIconPath;
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||appIconBmp==null)return "";
            string key=path+"|"+File.GetLastWriteTimeUtc(path).Ticks;
            if(key==browserIconKey)return browserIconData;
            try{using(var data=new MemoryStream()){appIconBmp.Save(data,ImageFormat.Png);browserIconData="data:image/png;base64,"+Convert.ToBase64String(data.ToArray());browserIconKey=key;return browserIconData;}}
            catch{return "";}
        }
        object BrowserArtwork()
        {
            bool personal=Theme.Personal;string folder=personal?CharOverlay.Folder(Theme.Kind):"";
            bool preview=ShotsDir!=null;
#if UI_PREVIEW
            preview=true;
#endif
            return new {background=BrowserImage(st.BgImage,true),slots=CharOverlay.Slots.Select(slot=>new {id=slot,host=SlotOnWidget(slot)?"widget":"window",src=personal?BrowserImage(Path.Combine(folder,slot+".png")):""}).ToArray(),inside=personal?BrowserImage(Path.Combine(folder,"window.png")):"",editor=charEdit,preview,icon=BrowserAppIcon()};
        }
    }
}
#endif
