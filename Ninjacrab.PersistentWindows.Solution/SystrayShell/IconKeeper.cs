using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Forms;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    // =====================================================================
    // Keeps desktop icons in step with window restores.
    //  - Every few seconds, records where the icons are for the monitors
    //    connected right now ("live" set).
    //  - The moment monitors start changing, recording pauses so Windows'
    //    reshuffle is never recorded; once the change settles, the icons
    //    for the new monitor set are put back (twice, because Explorer
    //    sometimes rearranges again a few seconds later).
    //  - Saving or restoring a named layout saves or restores its icons.
    // All work runs on the UI thread (shell objects need an STA thread).
    // =====================================================================
    public class IconKeeper : IDisposable
    {
        private const int TrackIntervalMs = 5000;
        private const int SettleExtraMs = 3000;   // added to the restore delay
        private const int SecondPassMs = 5000;
        private const int ResumeAfterMs = 5000;

        private readonly Control ui;
        private readonly IconStore store;
        private readonly Timer trackTimer = new Timer();
        private readonly Timer firstPass = new Timer();
        private readonly Timer secondPass = new Timer();
        private readonly Timer resume = new Timer();
        private bool suspended;
        private List<IconPos> lastRecorded;
        private bool enabled;

        public IconKeeper(Control uiThreadControl, string dataFolder)
        {
            ui = uiThreadControl;
            store = new IconStore(dataFolder);

            trackTimer.Interval = TrackIntervalMs;
            trackTimer.Tick += (s, e) => Track();
            firstPass.Tick += (s, e) => { firstPass.Stop(); RestoreLiveForCurrentMonitors(); secondPass.Start(); };
            secondPass.Interval = SecondPassMs;
            secondPass.Tick += (s, e) => { secondPass.Stop(); RestoreLiveForCurrentMonitors(); resume.Start(); };
            resume.Interval = ResumeAfterMs;
            resume.Tick += (s, e) => { resume.Stop(); suspended = false; lastRecorded = null; };

            SystemEvents.DisplaySettingsChanging += OnDisplayChanging;
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        // ---------------- Section 1: on/off from Settings ----------------
        public void SetEnabled(bool on)
        {
            enabled = on;
            if (on)
            {
                lastRecorded = null;
                trackTimer.Start();
            }
            else
            {
                trackTimer.Stop();
                firstPass.Stop();
                secondPass.Stop();
                resume.Stop();
                suspended = false;
            }
        }

        // ---------------- Section 2: continuous tracking ----------------
        private void Track()
        {
            if (!enabled || suspended)
                return;
            string key = Program.pwp.CurrentDisplayKey;
            if (string.IsNullOrEmpty(key) || key != Program.pwp.GetDisplayKey())
                return;  // monitors in flux; don't record
            try
            {
                var icons = DesktopIcons.Capture();
                if (icons == null || icons.Count == 0 || DesktopIcons.Same(icons, lastRecorded))
                    return;
                lastRecorded = icons;
                store.Put(key, IconStore.Live, icons);
            }
            catch (Exception ex)
            {
                Log.Error("desktop icon capture failed: " + ex.Message);
            }
        }

        // ---------------- Section 3: monitor changes ----------------
        private void OnDisplayChanging(object sender, EventArgs e)
        {
            Marshal(() => { if (enabled) suspended = true; });
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            Marshal(() =>
            {
                if (!enabled)
                    return;
                suspended = true;
                firstPass.Stop(); secondPass.Stop(); resume.Stop();
                firstPass.Interval = (int)(Program.Settings.RestoreDelaySeconds * 1000) + SettleExtraMs;
                firstPass.Start();
            });
        }

        private void RestoreLiveForCurrentMonitors()
        {
            try
            {
                string key = Program.pwp.GetDisplayKey();
                int moved = DesktopIcons.Restore(store.Get(key, IconStore.Live));
                if (moved > 0)
                    Log.Event("restored {0} desktop icons for {1}", moved, key);
            }
            catch (Exception ex)
            {
                Log.Error("desktop icon restore failed: " + ex.Message);
            }
        }

        // ---------------- Section 4: named layouts ----------------
        public void SaveForLayout(string key, int slot)
        {
            if (!enabled)
                return;
            try
            {
                var icons = DesktopIcons.Capture();
                if (icons != null)
                    store.Put(key, IconStore.SlotTag(slot), icons);
            }
            catch (Exception ex)
            {
                Log.Error("desktop icon save failed: " + ex.Message);
            }
        }

        public void RestoreForLayout(string key, int slot)
        {
            if (!enabled)
                return;
            try
            {
                // remember the current icons so Undo Last Restore can bring them back
                var now = DesktopIcons.Capture();
                if (now != null)
                    store.Put(key, IconStore.Undo, now);
                DesktopIcons.Restore(store.Get(key, IconStore.SlotTag(slot)));
            }
            catch (Exception ex)
            {
                Log.Error("desktop icon restore failed: " + ex.Message);
            }
        }

        public void RestoreUndo(string key)
        {
            if (!enabled)
                return;
            try
            {
                DesktopIcons.Restore(store.Get(key, IconStore.Undo));
            }
            catch (Exception ex)
            {
                Log.Error("desktop icon undo failed: " + ex.Message);
            }
        }

        public void ForgetLayout(string key, int slot)
        {
            store.Remove(key, IconStore.SlotTag(slot));
        }

        // ---------------- Section 5: plumbing ----------------
        private void Marshal(Action a)
        {
            try
            {
                if (ui.IsHandleCreated)
                    ui.BeginInvoke(a);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanging -= OnDisplayChanging;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            trackTimer.Dispose(); firstPass.Dispose(); secondPass.Dispose(); resume.Dispose();
        }
    }
}
