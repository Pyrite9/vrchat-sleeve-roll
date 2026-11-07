// Pyrite9 Sleeve Roll — last build per avatar (inspector section 'Last build'), after Orbit Ring's OrBuildRecord
//   The build plugin writes Library/Pyrite9SleeveRoll/last_builds.json keyed by avatar name.
//   Library is per project and outside git, so nothing is left in the user's assets. A failed read counts as 'no record'.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static class SrBuildRecord
    {
        public const string Dir = "Library/Pyrite9SleeveRoll";
        public const string FilePath = Dir + "/last_builds.json";
        const int MaxEntries = 32;

        /// <summary>one sleeve of one garment</summary>
        [System.Serializable]
        public class Side
        {
            public bool on;              // the component rolls this side
            public bool ok;              // the shape was built
            public float rolledTo;       // where the cuff ends up (0 elbow ~ 1 wrist)
            public bool stopped;         // stopped at the edge the outfit hides (D8)
            public int moved;            // sleeve vertices moved
            public int rebound;          // vertices moved to the wrist proxy
            public int unhide;           // hidden body vertices brought back while rolled (D10)
            public int physBones;        // PhysBones kept on this sleeve — the cloth follows them through shadow bones, still while rolled (10-38)
            public string message;       // English, why it failed
        }

        [System.Serializable]
        public class Garment
        {
            public string name;
            public string over;          // layered: the garment just inside this one (this one sits over its rolled shape), empty if none
            public Side left = new Side(), right = new Side();
        }

        [System.Serializable]
        public class Entry
        {
            public string avatar;
            public string time;          // yyyy-MM-dd HH:mm:ss
            public string version;
            public bool built;           // humanoid — the plugin ran
            public List<Garment> garments = new List<Garment>();
            public string menu;          // the toggle's name, empty = no menu
            public int syncedBits;
            public int fxLayers;
            public List<string> warnings = new List<string>();   // English, as in the build log
        }

        [System.Serializable]
        class Store { public List<Entry> entries = new List<Entry>(); }

        public static string CleanName(string n) => n == null ? "" : n.Replace("(Clone)", "").Trim();

        /// <summary>bumped by Put — the inspector's poll watches this, not the file. A domain reload rebuilds the inspector anyway</summary>
        public static int Revision { get; private set; }

        // read the file again only when it changed
        static Store cache;
        static long cacheStamp = -1;

        static Store Load()
        {
            long st = Stamp();
            if (cache != null && st == cacheStamp) return cache;
            var s = new Store();
            try
            {
                if (File.Exists(FilePath)) s = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
            }
            catch { }
            cache = s; cacheStamp = st;
            return s;
        }

        public static Entry Get(string avatar)
        {
            var n = CleanName(avatar);
            return Load().entries.FirstOrDefault(e => e.avatar == n);
        }

        static long Stamp() { try { return File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath).Ticks : 0; } catch { return 0; } }

        public static void Put(Entry e)
        {
            try
            {
                e.avatar = CleanName(e.avatar);
                var st = Load();
                st.entries.RemoveAll(x => x.avatar == e.avatar);
                st.entries.Insert(0, e);
                if (st.entries.Count > MaxEntries) st.entries.RemoveRange(MaxEntries, st.entries.Count - MaxEntries);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(st, true));
                cache = null;
                Revision++;
            }
            catch (System.Exception ex) { Debug.LogWarning("[Pyrite9 Sleeve Roll] build record not saved: " + ex.Message); }
        }
    }
}
