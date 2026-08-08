using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KunchengRPG.Core;
using KunchengRPG.Game;
using KunchengRPG.Scenes;
using KunchengRPG.UI;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Command-line entry points so scene generation and wiring can be checked without
    /// opening the editor:
    ///
    ///   Unity.exe -batchmode -quit -projectPath unity -executeMethod \
    ///       KunchengRPG.EditorTools.BatchVerify.BuildAndVerify
    ///
    /// Exits non-zero on any failure, so it slots into a build script as-is.
    /// </summary>
    public static class BatchVerify
    {
        private static readonly List<string> Failures = new List<string>();

        public static void BuildAndVerify()
        {
            Failures.Clear();

            try
            {
                SceneBuilder.BuildEverything();
            }
            catch (Exception e)
            {
                Fail($"Build threw: {e}");
                Report();
                return;
            }

            VerifyTileAssets();
            VerifyTitleScene();
            VerifyExploreScene();
            Report();
        }

        /// <summary>Verify only, assuming assets already exist.</summary>
        public static void VerifyOnly()
        {
            Failures.Clear();
            VerifyTileAssets();
            VerifyTitleScene();
            VerifyExploreScene();
            Report();
        }

        private static void VerifyTileAssets()
        {
            var tiles = TilesetBuilder.LoadTilesInOrder();
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] == null) Fail($"Tile asset {i} missing");

            Log($"Tile assets: {tiles.Length - CountNull(tiles)}/{tiles.Length}");
        }

        private static void VerifyTitleScene()
        {
            EditorSceneManager.OpenScene(SceneBuilder.TitleScenePath, OpenSceneMode.Single);

            var c = UnityEngine.Object.FindObjectOfType<TitleSceneController>();
            if (c == null) { Fail("TitleSceneController not found"); return; }

            Require(c.titlePanel, "titlePanel");
            Require(c.districtPanel, "districtPanel");
            Require(c.originPanel, "originPanel");
            Require(c.nameInputPanel, "nameInputPanel");
            Require(c.titleText, "titleText");
            Require(c.subtitleText, "subtitleText");
            Require(c.pressStartText, "pressStartText");
            Require(c.districtPromptText, "districtPromptText");
            Require(c.districtListContainer, "districtListContainer");
            Require(c.originPromptText, "originPromptText");
            Require(c.originDetailText, "originDetailText");
            Require(c.originListContainer, "originListContainer");
            Require(c.choiceItemPrefab, "choiceItemPrefab");
            Require(c.nameInputField, "nameInputField");
            Require(c.namePromptText, "namePromptText");
            Require(c.confirmButton, "confirmButton");

            if (UnityEngine.Object.FindObjectOfType<GameManager>() == null)
                Fail("TitleScene has no GameManager");

            Log("TitleScene wiring checked");
        }

        private static void VerifyExploreScene()
        {
            EditorSceneManager.OpenScene(SceneBuilder.ExploreScenePath, OpenSceneMode.Single);

            var c = UnityEngine.Object.FindObjectOfType<ExploreSceneController>();
            if (c == null) { Fail("ExploreSceneController not found"); return; }

            Require(c.mapController, "mapController");
            Require(c.playerController, "playerController");
            Require(c.hud, "hud");
            Require(c.eventPanel, "eventPanel");
            Require(c.dialoguePanel, "dialoguePanel");
            Require(c.mainCamera, "mainCamera");

            if (c.mapController != null)
            {
                Require(c.mapController.groundTilemap, "map.groundTilemap");
                Require(c.mapController.buildingTilemap, "map.buildingTilemap");
                Require(c.mapController.decorationTilemap, "map.decorationTilemap");

                var tiles = c.mapController.tiles;
                if (tiles == null || tiles.Length != TilesetBuilder.TileCount)
                    Fail($"map.tiles should have {TilesetBuilder.TileCount} entries, has {tiles?.Length ?? 0}");
                else if (CountNull(tiles) > 0)
                    Fail($"map.tiles has {CountNull(tiles)} null entries");
            }

            if (c.playerController != null)
            {
                Require(c.playerController.spriteRenderer, "player.spriteRenderer");
                Require(c.playerController.mapController, "player.mapController");
            }

            if (c.hud != null)
            {
                Require(c.hud.nameText, "hud.nameText");
                Require(c.hud.apText, "hud.apText");
                Require(c.hud.hpText, "hud.hpText");
                Require(c.hud.districtText, "hud.districtText");
                Require(c.hud.promptPanel, "hud.promptPanel");
                Require(c.hud.promptText, "hud.promptText");
                Require(c.hud.messagePanel, "hud.messagePanel");
                Require(c.hud.messageText, "hud.messageText");
            }

            if (c.eventPanel != null)
            {
                Require(c.eventPanel.panel, "eventPanel.panel");
                Require(c.eventPanel.titleText, "eventPanel.titleText");
                Require(c.eventPanel.descriptionText, "eventPanel.descriptionText");
                Require(c.eventPanel.choiceListContainer, "eventPanel.choiceListContainer");
                Require(c.eventPanel.choiceItemPrefab, "eventPanel.choiceItemPrefab");
                Require(c.eventPanel.resultText, "eventPanel.resultText");
            }

            if (c.dialoguePanel != null)
            {
                Require(c.dialoguePanel.panel, "dialoguePanel.panel");
                Require(c.dialoguePanel.speakerText, "dialoguePanel.speakerText");
                Require(c.dialoguePanel.dialogueText, "dialoguePanel.dialogueText");
                Require(c.dialoguePanel.choiceListContainer, "dialoguePanel.choiceListContainer");
                Require(c.dialoguePanel.choiceItemPrefab, "dialoguePanel.choiceItemPrefab");
            }

            Log("ExploreScene wiring checked");
        }

        private static int CountNull<T>(T[] arr) where T : class
        {
            if (arr == null) return 0;
            int n = 0;
            foreach (var x in arr) if (x == null) n++;
            return n;
        }

        private static void Require(object value, string field)
        {
            // Unity null-equality also catches destroyed objects, which a plain
            // reference check would miss.
            bool empty = value is UnityEngine.Object obj ? obj == null : value == null;
            if (empty) Fail($"unwired: {field}");
        }

        private static void Fail(string message)
        {
            Failures.Add(message);
            Debug.LogError($"[Verify] {message}");
        }

        private static void Log(string message) => Debug.Log($"[Verify] {message}");

        private static void Report()
        {
            if (Failures.Count == 0)
            {
                Debug.Log("[Verify] PASS — scenes built and fully wired");
                EditorApplication.Exit(0);
                return;
            }

            var sb = new StringBuilder($"[Verify] FAIL — {Failures.Count} problem(s):\n");
            foreach (var f in Failures) sb.AppendLine("  - " + f);
            Debug.LogError(sb.ToString());
            EditorApplication.Exit(1);
        }
    }
}
