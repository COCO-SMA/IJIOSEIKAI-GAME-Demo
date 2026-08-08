using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using KunchengRPG.Game;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Exercises every branch of the ending gate in batchmode, including the
    /// free_bird / last_native split and the becoming_local override that lets a
    /// deliberately single player still win.
    /// </summary>
    public static class BatchEndingTest
    {
        private static readonly List<string> Failures = new List<string>();

        public static void RunEndingTests()
        {
            Failures.Clear();

            var system = new EndingSystem();
            system.LoadData();

            VerifyCopyExists(system);

            // Ships two districts in v1.0, so the rooted gate must scale to those.
            var districts = new List<string> { "jinyong", "jiuxu" };

            Case(system, districts, "childless on purpose", "free_bird",
                 children: false, celibate: true, rooted: false);

            Case(system, districts, "childless without choosing", "last_native",
                 children: false, celibate: false, rooted: false);

            Case(system, districts, "heir, gate not met", "legacy_continues",
                 children: true, celibate: false, rooted: false);

            Case(system, districts, "single but rooted", "becoming_local",
                 children: false, celibate: true, rooted: true);

            Case(system, districts, "devoured outranks all", "devoured",
                 children: false, celibate: true, rooted: true, cause: DeathCause.Devoured);

            Report();
        }

        private static void VerifyCopyExists(EndingSystem system)
        {
            string[] required =
            {
                "free_bird", "last_native", "legacy_continues", "becoming_local", "devoured"
            };

            foreach (var id in required)
            {
                var data = system.Get(id);
                if (data == null) { Fail($"ending copy missing: {id}"); continue; }
                if (string.IsNullOrEmpty(data.title)) Fail($"ending '{id}' has no title");
                if (data.body == null || data.body.Count == 0) Fail($"ending '{id}' has no body");
            }
        }

        private static void Case(EndingSystem system, List<string> districts, string label,
                                 string expected, bool children, bool celibate, bool rooted,
                                 DeathCause cause = DeathCause.OldAge)
        {
            var player = new Player();
            player.age = 62;
            if (player.flags == null) player.flags = new Dictionary<string, int>();
            if (children) player.flags["has_children"] = 1;
            player.SetCelibacyChoice(celibate);

            var city = BuildCity(districts, rooted);

            EndingResult result;
            try
            {
                result = system.Resolve(player, city, 2, cause, districts);
            }
            catch (Exception e)
            {
                Fail($"{label}: Resolve threw: {e.Message}");
                return;
            }

            if (result.endingId != expected)
                Fail($"{label}: expected '{expected}', got '{result.endingId}' ({result.reason})");
            else
                Log($"{label} -> {result.endingId} \"{result.Title}\"");
        }

        /// <summary>
        /// Build a city either below or above the rooted gate, so the test drives the
        /// real CitySystem check instead of a stand-in.
        /// </summary>
        private static CitySystem BuildCity(List<string> districts, bool rooted)
        {
            var city = new CitySystem();
            if (!rooted) return city;

            foreach (var id in districts)
                city.AddDistrictAffinity(id, 100);
            city.SetRooted(true);

            return city;
        }

        private static void Fail(string message)
        {
            Failures.Add(message);
            Debug.LogError($"[EndingTest] {message}");
        }

        private static void Log(string message) => Debug.Log($"[EndingTest] {message}");

        private static void Report()
        {
            if (Failures.Count == 0)
            {
                Debug.Log("[EndingTest] PASS — all ending branches resolve correctly");
                EditorApplication.Exit(0);
                return;
            }

            var sb = new StringBuilder($"[EndingTest] FAIL — {Failures.Count} problem(s):\n");
            foreach (var f in Failures) sb.AppendLine("  - " + f);
            Debug.LogError(sb.ToString());
            EditorApplication.Exit(1);
        }
    }
}
