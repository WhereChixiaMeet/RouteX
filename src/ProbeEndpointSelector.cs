using System;
using System.Collections.Generic;
using System.Linq;
namespace NetProbe {
    public sealed class ProbeEndpointSelector {
        readonly Dictionary<string,double> retryAfter=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        int cursor;
        public string Pick(IList<string> urls,bool rotate,double now){
            if(urls==null||urls.Count==0)throw new ArgumentException("No probe endpoints configured");
            int start=rotate?(cursor++&int.MaxValue)%urls.Count:0;
            for(int i=0;i<urls.Count;i++){string url=urls[(start+i)%urls.Count];double until;if(!retryAfter.TryGetValue(url,out until)||until<=now)return url;}
            return urls.OrderBy(url=>retryAfter[url]).First();
        }
        public void Report(string url,bool ok,double now){if(ok)retryAfter.Remove(url);else retryAfter[url]=now+60;}
    }
}
