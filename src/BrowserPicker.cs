#if WEB_UI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NetProbe
{
    public partial class MainForm
    {
        // "+" in the Routing lists: the recently added apps first, then the running processes
        // (as ProcPicker did in the old interface). The process snapshot runs off the UI thread.
        void BrowserAppPicker(int id, Dictionary<string, object> args)
        {
            string key = BrowserListKey(BrowserString(args, "name"));
            if (TunSettingsLocked) throw new InvalidOperationException(TunLockMessage);
            var cmp = StringComparer.OrdinalIgnoreCase;
            var have = new HashSet<string>(AppLists.Get(key), cmp);
            var recent = AppLists.Recent().Where(r => !have.Contains(r)).ToList();
            bool icons = st.ShowAppIcons;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<ProcRow> rows = null;
                try { rows = ProcInfo.Snapshot(); } catch { }
                rows = rows ?? new List<ProcRow>();
                var groups = rows.GroupBy(x => x.Name, cmp)
                    .Where(g => !ProcPicker.Hidden.Contains(g.Key, cmp) && !have.Contains(g.Key))
                    .Select(g => new { Name = g.First().Name, Count = g.Count(), Conns = g.Sum(x => x.Conns), Path = g.Select(x => x.Path).FirstOrDefault(x => x != "") ?? "" })
                    .OrderByDescending(g => g.Conns > 0).ThenBy(g => g.Name, cmp).ToList();
                if (icons)
                {
                    foreach (var g in groups) if (g.Path != "") try { AppIcons.Seed(g.Name, g.Path); } catch { }
                    foreach (var r in recent)
                        if (!groups.Any(g => cmp.Equals(g.Name, r))) try { string p = AppIcons.PathOf(r); if (p != null) AppIcons.Seed(r, p); } catch { }
                }
                Action reply = () =>
                {
                    try
                    {
                        var byName = groups.ToDictionary(g => g.Name, cmp);
                        browser.Reply(id, new
                        {
                            recent = recent.Select(r => { bool run = byName.ContainsKey(r); return new { name = r, path = run ? byName[r].Path : "", count = run ? byName[r].Count : 0, conns = run ? byName[r].Conns : 0, icon = BrowserEntryIcon(r) }; }).ToArray(),
                            running = groups.Where(g => !recent.Contains(g.Name, cmp)).Select(g => new { name = g.Name, path = g.Path, count = g.Count, conns = g.Conns, icon = BrowserEntryIcon(g.Name) }).ToArray()
                        });
                    }
                    catch (Exception ex) { browser.Reply(id, null, ex.Message); }
                };
                try { if (!IsDisposed) BeginInvoke(reply); } catch { }
            });
        }
    }
}
#endif
