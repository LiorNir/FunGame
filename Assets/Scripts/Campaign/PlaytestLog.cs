using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Campaign
{
    /// <summary>
    /// Writes playtest files for Claude to analyse: one folder per run of the game (in the player's data folder, under
    /// PlaytestLogs) with a session file (map actions: encounters chosen, upgrades, resets) and one file per battle
    /// (the encounter in the lab's text form, then the battle log with every player action and its resolution).
    /// </summary>
    public static class PlaytestLog
    {
        private static string _sessionFolder;
        private static int _battleCount;

        /// <summary>The folder holding every run's logs (what a playtester zips and sends).</summary>
        public static string RootFolder => Path.Combine(Application.persistentDataPath, "PlaytestLogs");

        private static string SessionFolder
        {
            get
            {
                if (_sessionFolder == null)
                {
                    _sessionFolder = Path.Combine(RootFolder, "session-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                    Directory.CreateDirectory(_sessionFolder);
                }

                return _sessionFolder;
            }
        }

        /// <summary>Adds a timestamped line to this run's session file.</summary>
        public static void Note(string line)
        {
            Safely(() => File.AppendAllText(Path.Combine(SessionFolder, "session.txt"), $"{DateTime.Now:HH:mm:ss}  {line}\n"));
        }

        /// <summary>Writes one battle's file: a header, the encounter in the lab's text form and the battle log.</summary>
        public static void WriteBattle(string encounterName, string result, string setupText, IEnumerable<string> log)
        {
            Safely(() =>
            {
                _battleCount++;
                var safeName = string.Join("_", encounterName.Split(Path.GetInvalidFileNameChars()));
                // the result has "stars 2/3" in it: no part of the name may hold a character a file name cannot
                var safeResult = string.Join("_", result.Split(Path.GetInvalidFileNameChars()));
                var path = Path.Combine(SessionFolder, $"{_battleCount:00} {safeName} - {safeResult}.txt");
                var lines = new List<string> { $"# {encounterName}: {result} ({DateTime.Now:yyyy-MM-dd HH:mm:ss})", string.Empty, setupText, "# battle log" };
                lines.AddRange(log);
                File.WriteAllLines(path, lines);
            });
            Note($"battle {_battleCount:00}: {encounterName} - {result}");
        }

        public static void OpenFolder()
        {
            Directory.CreateDirectory(RootFolder);
            Application.OpenURL("file:///" + RootFolder.Replace('\\', '/'));
        }

        // logging must never break the game
        private static void Safely(Action write)
        {
            try
            {
                write();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not write the playtest log: {exception.Message}");
            }
        }
    }
}
