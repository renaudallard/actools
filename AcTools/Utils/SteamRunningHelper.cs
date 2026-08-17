using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AcTools.Utils.Helpers;
using Microsoft.Win32;

namespace AcTools.Utils {
    public static class SteamRunningHelper {
        private static string GetSteamDirectory() {
            var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var ret = regKey?.GetValue("SteamPath")?.ToString();
            regKey?.Close();
            return ret;
        }

        private static bool IsSteamRunning() {
            return Process.GetProcessesByName("steam").Length > 0;
        }

        private static void TryToRunSteam(string steamDirectory, bool launchAc) {
            try {
                Process.Start(Path.Combine(steamDirectory, "Steam.exe"), launchAc ?
                        $"-silent -applaunch {CommonAcConsts.AppId.ToInvariantString()}" : "-silent");
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return;
            }

            // Cold start takes a lot longer than a couple of seconds, especially in a Wine prefix
            for (var i = 0; i < 150 && !IsSteamRunning(); i++) {
                Thread.Sleep(200);
            }
        }

        public static void EnsureSteamIsRunning(bool tryToRun, bool launchAc) {
            if (IsSteamRunning()) return;

            var steamDirectory = GetSteamDirectory();
            if (steamDirectory == null) return;

            if (tryToRun) {
                TryToRunSteam(steamDirectory, launchAc);
                if (!IsSteamRunning()) {
                    throw new Exception("Couldn’t run Steam");
                }
            }

            // throw new Exception("Running Steam is required");
        }
    }
}