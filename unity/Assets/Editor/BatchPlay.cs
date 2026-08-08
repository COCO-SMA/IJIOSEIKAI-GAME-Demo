using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KunchengRPG.Core;
using KunchengRPG.Scenes;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Drives an actual play session from the command line and fails on any runtime
    /// error, so "does it run" is answerable without opening the editor:
    ///
    ///   Unity.exe -batchmode -projectPath unity -executeMethod \
    ///       KunchengRPG.EditorTools.BatchPlay.RunSmokeTest
    ///
    /// Domain reload is disabled for the session; without that, entering play mode
    /// wipes the statics below and the step machine never resumes.
    /// </summary>
    public static class BatchPlay
    {
        private const int SettleFrames = 30;
        private const int ExploreFrames = 90;
        private const int TimeoutFrames = 1200;

        private static readonly List<string> Errors = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        private enum Step { EnteringPlay, AtTitle, StartingGame, Exploring, Done }

        private static Step step;
        private static int frame;
        private static int stepFrame;
        private static bool finished;
        private static TitleSceneController title;

        public static void RunSmokeTest()
        {
            Errors.Clear();
            Notes.Clear();
            step = Step.EnteringPlay;
            frame = 0;
            stepFrame = 0;
            finished = false;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            Application.logMessageReceived += OnLog;
            EditorApplication.update += OnUpdate;

            EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
            Debug.Log("[Play] Entering play mode from TitleScene");
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Errors.Add($"{type}: {message}");
        }

        private static void OnUpdate()
        {
            if (finished) return;

            frame++;
            stepFrame++;

            if (frame > TimeoutFrames)
            {
                Errors.Add($"Timed out at step {step} after {frame} editor frames");
                Finish();
                return;
            }

            switch (step)
            {
                case Step.EnteringPlay:
                    if (EditorApplication.isPlaying) Advance(Step.AtTitle);
                    break;

                case Step.AtTitle:
                    if (stepFrame >= SettleFrames) CheckTitleThenStart();
                    break;

                case Step.StartingGame:
                    // LoadScene is deferred to the end of the frame, so wait for the
                    // swap rather than assuming it already happened.
                    var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (active.name == "ExploreScene" && active.isLoaded)
                    {
                        Notes.Add($"scene transition: TitleScene -> {active.name}");
                        Advance(Step.Exploring);
                    }
                    else if (stepFrame >= SettleFrames * 4)
                    {
                        Errors.Add($"ExploreScene never became active (still on '{active.name}')");
                        Finish();
                    }
                    break;

                case Step.Exploring:
                    if (stepFrame >= ExploreFrames)
                    {
                        CheckExploring();
                        Finish();
                    }
                    break;
            }
        }

        private static void Advance(Step next)
        {
            step = next;
            stepFrame = 0;
        }

        /// <summary>
        /// Confirm the title scene came up with data loaded, then start a run. Ids come
        /// from the loaded tables rather than being hardcoded, so renaming a district
        /// file does not silently turn this into a no-op.
        /// </summary>
        private static void CheckTitleThenStart()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Errors.Add("GameManager.Instance is null after entering play mode");
                Finish();
                return;
            }

            title = UnityEngine.Object.FindObjectOfType<TitleSceneController>();
            if (title == null)
            {
                Errors.Add("TitleSceneController not present in TitleScene at runtime");
                Finish();
                return;
            }

            if (gm.districts == null || gm.districts.Count == 0)
            {
                Errors.Add("No districts loaded from Resources");
                Finish();
                return;
            }

            if (gm.origins == null || gm.origins.Count == 0)
            {
                Errors.Add("No origins loaded from Resources");
                Finish();
                return;
            }

            Notes.Add($"districts loaded: {gm.districts.Count} ({string.Join(", ", gm.districts.Keys.OrderBy(k => k))})");
            Notes.Add($"origins loaded: {gm.origins.Count}");

            if (gm.Save == null) Errors.Add("SaveManager was not constructed");
            if (gm.City == null) Errors.Add("CitySystem was not constructed");
            if (gm.Inheritance == null) Errors.Add("InheritanceSystem was not constructed");

            string districtId = gm.districts.Keys.OrderBy(k => k).First();
            string originId = gm.origins
                .Where(kv => kv.Value != null && (kv.Value.district == districtId || kv.Value.district == "common"))
                .Select(kv => kv.Key)
                .OrderBy(k => k)
                .FirstOrDefault() ?? gm.origins.Keys.OrderBy(k => k).First();

            Notes.Add($"starting run: district={districtId} origin={originId}");

            // Go through TitleSceneController.ConfirmName rather than calling
            // StartNewGame directly: the scene transition lives there, so the direct
            // call would leave us sitting in TitleScene. The two selection fields are
            // private and normally set by keyboard navigation, hence the reflection.
            if (!SetPrivateField(title, "selectedDistrictId", districtId) ||
                !SetPrivateField(title, "selectedOriginId", originId))
            {
                Finish();
                return;
            }

            try
            {
                title.ConfirmName();
            }
            catch (Exception e)
            {
                Errors.Add($"ConfirmName threw: {e}");
                Finish();
                return;
            }

            Advance(Step.StartingGame);
        }

        private static bool SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                Errors.Add($"{target.GetType().Name}.{fieldName} no longer exists — update BatchPlay");
                return false;
            }
            field.SetValue(target, value);
            return true;
        }

        /// <summary>
        /// After the run has been going for a while, confirm the world actually exists:
        /// a live player, a map, and a HUD.
        /// </summary>
        private static void CheckExploring()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Errors.Add("GameManager.Instance went null during play");
                return;
            }

            Notes.Add($"state after {ExploreFrames} frames: {gm.state}, generation {gm.generation}");

            if (gm.Player == null)
            {
                Errors.Add("GameManager.Player is null while exploring");
                return;
            }

            var p = gm.Player;
            Notes.Add($"player: {p.name} age={p.age} hp={p.hp}/{p.maxHp} money={p.money} ap={p.actionPoints}");

            if (p.maxHp <= 0) Errors.Add($"Player.maxHp is {p.maxHp}");
            if (p.stats == null) Errors.Add("Player.stats is null");

            var map = UnityEngine.Object.FindObjectOfType<Game.MapController>();
            if (map == null) Errors.Add("No MapController in the running scene");

            var pc = UnityEngine.Object.FindObjectOfType<Game.PlayerController>();
            if (pc == null) Errors.Add("No PlayerController in the running scene");
            else Notes.Add($"player world position: {pc.transform.position}");

            if (UnityEngine.Object.FindObjectOfType<UI.HUDController>() == null)
                Errors.Add("No HUDController in the running scene");
        }

        private static void Finish()
        {
            if (finished) return;
            finished = true;

            EditorApplication.update -= OnUpdate;
            Application.logMessageReceived -= OnLog;

            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();

            // Report on a later tick: leaving play mode takes a frame and can still log.
            EditorApplication.delayCall += Report;
        }

        private static void Report()
        {
            foreach (var note in Notes) Debug.Log($"[Play] {note}");

            if (Errors.Count == 0)
            {
                Debug.Log("[Play] PASS — play session ran with no runtime errors");
                EditorApplication.Exit(0);
                return;
            }

            foreach (var err in Errors) Debug.Log($"[Play] FAIL {err}");
            Debug.Log($"[Play] FAIL — {Errors.Count} runtime problem(s)");
            EditorApplication.Exit(1);
        }
    }
}
