﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MagFilter
{
    public class LaunchControl
    {
        public class LaunchInfo
        {
            public bool IsValid;
            public DateTime LaunchTime;
            public string ServerName;
            public string AccountName;
            public string CharacterName;
        }
        public class LaunchResponse
        {
            public bool IsValid;
            public DateTime ResponseTime;
            public int ProcessId;
            public string MagFilterVersion;
        }
        public class HeartbeatResponse
        {
            public bool IsValid;
            public HeartbeatGameStatus Status = new HeartbeatGameStatus();
            public string LogFilepath;
        }
        public class MagFilterInfo
        {
            public string MagFilterPath;
            public string MagFilterVersion;
        }
        /// <summary>
        /// Called by ThwargLauncher
        /// </summary>
        /// <returns></returns>
        public static MagFilterInfo GetMagFilterInfo()
        {
            var info = new MagFilterInfo();
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            info.MagFilterVersion = assembly.GetName().Version.ToString();
            info.MagFilterPath = assembly.Location;
            return info;
        }
        /// <summary>
        /// Called by ThwargLauncher
        /// </summary>
        public static void RecordLaunchInfo(string serverName, string accountName, string characterName, DateTime timestampUtc)
        {
            string filepath = FileLocations.GetCurrentLaunchFilePath();
            using (var file = new StreamWriter(filepath, append: false))
            {
                file.WriteLine("Timestamp=TimeUtc:'{0}'", timestampUtc); // Format :O converted back out of UTC
                file.WriteLine("GameInstance=ServerName:'{0}' AccountName:'{1}' CharacterName:'{2}'", serverName, accountName, characterName);
            }
        }
        public static LaunchInfo DebugGetLaunchInfo()
        {
            return GetLaunchInfo();
        }
        /// <summary>
        /// Called by Mag-Filter
        /// </summary>
        internal static LaunchInfo GetLaunchInfo()
        {
            var info = new LaunchInfo();
            try
            {
                string filepath = FileLocations.GetCurrentLaunchFilePath();

                if (!File.Exists(filepath))
                {
                    log.WriteLogMsg(string.Format("No launch file found: '{0}'", filepath));
                    return info;
                }
                var settings = (new SettingsFileParser()).ReadSettingsFile(filepath);

                info.LaunchTime = settings.GetValue("Timestamp").GetDateParam("TimeUtc");
                TimeSpan maxLatency = new TimeSpan(0, 0, 0, 30); // 30 seconds max latency from exe call to game launch
                if (DateTime.UtcNow - info.LaunchTime >= maxLatency)
                {
                    log.WriteLogMsg(string.Format("DateTime.UtcNow-'{0}', info.LaunchTime='{1}', maxLatency='{2}'", DateTime.UtcNow, info.LaunchTime, maxLatency));
                    log.WriteLogMsg("Launch file TimeUtc too old");
                    return info;
                }

                var gameInstance = settings.GetValue("GameInstance");
                info.ServerName = gameInstance.GetStringParam("ServerName");
                info.AccountName = gameInstance.GetStringParam("AccountName");
                info.CharacterName = gameInstance.GetStringParam("CharacterName");

                info.IsValid = true;
            }
            catch (Exception exc)
            {
                log.WriteLogMsg(string.Format("GetLaunchInfo exception: {0}", exc));
            }
            return info;
        }
        /// <summary>
        /// Called by Mag-Filter
        /// </summary>
        internal static void RecordLaunchResponse(DateTime timestampUtc)
        {
            string filepath = FileLocations.GetCurrentLaunchResponseFilePath();
            using (var file = new StreamWriter(filepath, append: false))
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                file.WriteLine("TimeUtc:" + timestampUtc);
                file.WriteLine("ProcessId:{0}", pid);
                file.WriteLine("MagFilterVersion:{0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            }
        }
        /// <summary>
        /// Called by ThwargLauncher
        /// </summary>
        public static LaunchResponse GetLaunchResponse(TimeSpan maxLatency)
        {
            var info = new LaunchResponse();
            string filepath = FileLocations.GetCurrentLaunchResponseFilePath();

            if (!File.Exists(filepath)) { return info; }
            using (var file = new StreamReader(filepath))
            {
                string contents = file.ReadToEnd();
                string[] stringSeps = new string[] { "\r\n" };
                string[] lines = contents.Split(stringSeps, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length != 3) { return info; }
                int index = 0;

                // Parse TimeUtc & validate
                var lr1 = ParseDateTimeSetting(lines[index], "TimeUtc:");
                if (!lr1.IsValid) { return info; }
                info.ResponseTime = lr1.Value;
                if (DateTime.UtcNow - info.ResponseTime >= maxLatency)
                {
                    return info;
                }

                // Parse ProcessId
                ++index;
                var lr2 = ParseIntSetting(lines[index], "ProcessId:");
                if (!lr2.IsValid) { return info; }
                info.ProcessId = lr2.Value;

                // Parse ProcessId
                ++index;
                var lsv = ParseStringSetting(lines[index], "MagFilterVersion:");
                if (!lsv.IsValid) { return info; }
                info.MagFilterVersion = lsv.Value;

                info.IsValid = true;
            }
            return info;
        }
        internal static void RecordHeartbeatStatus(string filepath, HeartbeatGameStatus status, string key, string value)
        {
            using (var file = new System.IO.StreamWriter(filepath, append: false))
            {
                TimeSpan span = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
                file.WriteLine("FileVersion:{0}", HeartbeatGameStatus.MASTER_FILE_VERSION);
                file.WriteLine("UptimeSeconds:{0}", (int)span.TotalSeconds);
                file.WriteLine("ServerName:{0}", status.ServerName);
                file.WriteLine("AccountName:{0}", status.AccountName);
                file.WriteLine("CharacterName:{0}", status.CharacterName);
                file.WriteLine("LogFilepath:{0}", log.GetLogFilepath());
                file.WriteLine("ProcessId:{0}", System.Diagnostics.Process.GetCurrentProcess().Id);
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                file.WriteLine("MagFilterVersion:{0}", assembly.GetName().Version);
                file.WriteLine("MagFilterFilePath:{0}", assembly.Location);
                if (key != null)
                {
                    file.WriteLine("{0}:{1}", key, value);
                }
            }
        }
        /// <summary>
        /// Called by ThwargLauncher
        /// </summary>
        public static string GetHeartbeatStatusFileVersion() { return HeartbeatGameStatus.MASTER_FILE_VERSION; }
        public static HeartbeatResponse GetHeartbeatStatus(string filepath)
        {
            var info = new HeartbeatResponse();
            try
            {
                if (string.IsNullOrEmpty(filepath)) { return info; }
                if (!File.Exists(filepath)) { return info; }

                var settings = (new SettingsFileParser()).ReadSettingsFile(filepath);

                info.Status.FileVersion = settings.GetValue("FileVersion").GetSingleParam();
                if (!info.Status.FileVersion.StartsWith(HeartbeatGameStatus.MASTER_FILE_VERSION_COMPAT))
                {
                    throw new Exception(string.Format(
                        "Incompatible heartbeat status file version: {0}",
                        info.Status.FileVersion));
                }
                info.Status.UptimeSeconds = settings.GetValue("UptimeSeconds").GetSingleIntParam();
                info.Status.ServerName = settings.GetValue("ServerName").GetSingleParam();
                info.Status.AccountName = settings.GetValue("AccountName").GetSingleParam();
                info.Status.CharacterName = settings.GetValue("CharacterName").GetSingleParam();
                info.LogFilepath = settings.GetValue("LogFilepath").GetSingleParam();
                info.Status.ProcessId = settings.GetValue("ProcessId").GetSingleIntParam();
                info.Status.MagFilterVersion = settings.GetValue("MagFilterVersion").GetSingleParam();
                info.Status.MagFilterFilePath = settings.GetValue("MagFilterFilePath").GetSingleParam();

                info.IsValid = true;
            }
            catch (Exception exc)
            {
                log.WriteLogMsg(string.Format("GetHeartbeatStatus exception: {0}", exc));
            }
            return info;
        }
        private class StringSetting { public bool IsValid; public string Value; }
        private static StringSetting ParseStringSetting(string line, string prefix)
        {
            var result = new StringSetting();
            if (BeginsWith(line, prefix))
            {
                string text = line.Substring(prefix.Length);
                result.IsValid = true;
                result.Value = text;
            }
            return result;
        }
        private class IntSetting { public bool IsValid; public int Value; }
        private static IntSetting ParseIntSetting(string line, string prefix)
        {
            var result = new IntSetting();
            var strret = ParseStringSetting(line, prefix);
            if (strret.IsValid)
            {
                string text = strret.Value;
                int value = 0;
                if (int.TryParse(text, out value))
                {
                    result.IsValid = true;
                    result.Value = value;
                }
            }
            return result;
        }
        private class DateTimeSetting { public bool IsValid; public DateTime Value; }
        private static DateTimeSetting ParseDateTimeSetting(string line, string prefix)
        {
            var result = new DateTimeSetting();
            var strret = ParseStringSetting(line, prefix);
            if (strret.IsValid)
            {
                string text = strret.Value;
                DateTime value = DateTime.MinValue;
                if (DateTime.TryParse(text, out value))
                {
                    result.IsValid = true;
                    result.Value = value;
                }
            }
            return result;
        }
        /// <summary>
        /// Line starts with specified prefix and has at least one character beyond it
        ///  (primarily used to Substring(prefix.Length) will not fail
        /// </summary>
        private static bool BeginsWith(string line, string prefix)
        {
            return line != null && line.StartsWith(prefix) && line.Length > prefix.Length;
        }
    }
}
