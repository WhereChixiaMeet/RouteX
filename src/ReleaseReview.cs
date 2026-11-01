using System;
namespace NetProbe {
    public static class ReleaseReview {
        public static bool Prepare(Settings settings,string version,bool existingProfile,bool updated){
            System.Version previous,current;bool newer=System.Version.TryParse(settings.LastRunVersion,out previous)&&System.Version.TryParse(version,out current)&&current>previous;
            bool legacy=string.IsNullOrEmpty(settings.LastRunVersion)&&existingProfile;
            if((newer||legacy||updated)&&settings.WhatsNewShownFor!=version)settings.WhatsNewPending=version;
            settings.LastRunVersion=version;
            return settings.WhatsNewPending==version&&settings.WhatsNewShownFor!=version;
        }
        public static void MarkShown(Settings settings,string version){settings.WhatsNewShownFor=version;settings.WhatsNewPending="";}
    }
}
