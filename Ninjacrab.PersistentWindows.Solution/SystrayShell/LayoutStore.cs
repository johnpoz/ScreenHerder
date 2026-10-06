using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // One named layout: a friendly name pinned to one of the engine's
    // snapshot slots (0-9, a-z) for one specific monitor set.
    // =====================================================================
    [DataContract]
    public class NamedLayout
    {
        [DataMember] public string Name;
        [DataMember] public string DisplayKey;   // engine key describing the monitor set
        [DataMember] public int Slot;            // engine snapshot id 0..35
        [DataMember] public DateTime SavedAt;
        [DataMember] public DateTime LastUsed;   // last save or restore

        public char SlotChar
        {
            get { return Program.SnapshotIdToChar(Slot); }
        }
    }

    // =====================================================================
    // Persistent list of named layouts (layouts.json in the app data folder)
    // =====================================================================
    public class LayoutStore
    {
        public const string FileName = "layouts.json";
        public const int MaxSlot = 35;            // engine slots 0..35 are user snapshots

        private readonly string path;
        private List<NamedLayout> layouts = new List<NamedLayout>();
        private readonly object sync = new object();

        public LayoutStore(string folder)
        {
            path = Path.Combine(folder, FileName);
            Load();
        }

        // -----------------------------------------------------------------
        // Section 1: queries
        // -----------------------------------------------------------------
        public List<NamedLayout> All()
        {
            lock (sync)
                return layouts.OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // layouts for one monitor set, most recently used first
        public List<NamedLayout> ForDisplay(string displayKey)
        {
            lock (sync)
                return layouts.Where(l => l.DisplayKey == displayKey)
                              .OrderByDescending(l => l.LastUsed)
                              .ToList();
        }

        public NamedLayout FindByName(string displayKey, string name)
        {
            lock (sync)
                return layouts.FirstOrDefault(l => l.DisplayKey == displayKey &&
                    string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase));
        }

        public NamedLayout FindBySlot(string displayKey, int slot)
        {
            lock (sync)
                return layouts.FirstOrDefault(l => l.DisplayKey == displayKey && l.Slot == slot);
        }

        // first free slot for a monitor set: a-z, then 1-9, then 0
        public int AllocateSlot(string displayKey)
        {
            var used = new HashSet<int>(ForDisplay(displayKey).Select(l => l.Slot));
            var order = Enumerable.Range(10, 26).Concat(Enumerable.Range(1, 9)).Concat(new[] { 0 });
            foreach (int id in order)
                if (!used.Contains(id))
                    return id;
            return -1; // all 36 slots taken for this monitor set
        }

        // -----------------------------------------------------------------
        // Section 2: changes (every change is written to disk immediately)
        // -----------------------------------------------------------------
        public NamedLayout Upsert(string displayKey, int slot, string name)
        {
            lock (sync)
            {
                // a slot holds one layout; a name is unique per monitor set
                layouts.RemoveAll(l => l.DisplayKey == displayKey &&
                    (l.Slot == slot || string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)));
                var now = DateTime.Now;
                var layout = new NamedLayout { Name = name, DisplayKey = displayKey, Slot = slot, SavedAt = now, LastUsed = now };
                layouts.Add(layout);
                Save();
                return layout;
            }
        }

        public void Touch(NamedLayout layout)
        {
            lock (sync)
            {
                layout.LastUsed = DateTime.Now;
                Save();
            }
        }

        public bool Rename(NamedLayout layout, string newName)
        {
            lock (sync)
            {
                if (layouts.Any(l => l != layout && l.DisplayKey == layout.DisplayKey &&
                    string.Equals(l.Name, newName, StringComparison.CurrentCultureIgnoreCase)))
                    return false;
                layout.Name = newName;
                Save();
                return true;
            }
        }

        public void Remove(NamedLayout layout)
        {
            lock (sync)
            {
                layouts.Remove(layout);
                Save();
            }
        }

        // -----------------------------------------------------------------
        // Section 3: persistence
        // -----------------------------------------------------------------
        private void Load()
        {
            try
            {
                if (!File.Exists(path))
                    return;
                var ser = new DataContractJsonSerializer(typeof(List<NamedLayout>));
                using (var fs = File.OpenRead(path))
                    layouts = (List<NamedLayout>)ser.ReadObject(fs) ?? new List<NamedLayout>();
            }
            catch (Exception ex)
            {
                Log.Error("layouts load failed: " + ex.Message);
                layouts = new List<NamedLayout>();
            }
        }

        private void Save()
        {
            try
            {
                string tmp = path + ".tmp";
                var ser = new DataContractJsonSerializer(typeof(List<NamedLayout>));
                using (var ms = new MemoryStream())
                {
                    using (var w = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false, true, "  "))
                        ser.WriteObject(w, layouts);
                    File.WriteAllBytes(tmp, ms.ToArray());
                }
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Log.Error("layouts save failed: " + ex.Message);
            }
        }

        // -----------------------------------------------------------------
        // Section 4: human description of a monitor set
        // e.g. "3 monitors: 2560x1440, 1920x1080, 1920x1080"
        // -----------------------------------------------------------------
        public static string DescribeMonitors(string displayKey)
        {
            if (string.IsNullOrEmpty(displayKey))
                return "Unknown monitors";
            var parts = displayKey.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            var res = new List<string>();
            foreach (var p in parts)
            {
                int i = p.LastIndexOf("_Res", StringComparison.Ordinal);
                if (i >= 0)
                    res.Add(p.Substring(i + 4).Replace('M', '-'));
            }
            if (res.Count == 0)
                return "Unknown monitors";
            string count = res.Count == 1 ? "1 monitor" : res.Count + " monitors";
            return count + ": " + string.Join(", ", res);
        }
    }
}
