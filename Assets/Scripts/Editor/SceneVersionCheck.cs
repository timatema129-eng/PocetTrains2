using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.EditorTools
{
    /// <summary>
    /// Сцена ParisPrototype собирается сама, без вопросов:
    /// - после открытия Unity / обновления кода, если сцена устарела или сломана;
    /// - при нажатии Play — то же самое, и Play всегда запускает ParisPrototype.
    /// «Устарела» — другая версия сборщика (ParisPrototypeBuilder.LayoutBuild) или поменялись файлы карты.
    /// «Сломана» — в сцене есть ссылки на ассеты, которых больше нет (например, потерялась база игры).
    /// Сохранение игры при этом не трогается.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneVersionCheck
    {
        private const string ScenePath = ParisPrototypeBuilder.ScenePath;

        /// <summary>Только что пересобрали перед Play — второй раз не проверяем (защита от зацикливания).</summary>
        private static bool justRebuilt;

        static SceneVersionCheck()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += OnEditorLoaded;
        }

        private static void OnEditorLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OnEditorLoaded;
                return;
            }

            string reason = WhyRebuild();
            if (reason != null)
            {
                Debug.Log("[Train Manager] Сцена ParisPrototype пересобирается автоматически: " + reason);
                SaveOpenScenesWithPath();
                ParisPrototypeBuilder.Build(false);
            }
            SetPlayStartScene();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            if (justRebuilt)
            {
                justRebuilt = false;
                SetPlayStartScene();
                return;
            }

            string reason = WhyRebuild();
            if (reason == null)
            {
                SetPlayStartScene();
                return;
            }

            // Останавливаем запуск, пересобираем сцену и запускаем Play снова
            EditorApplication.isPlaying = false;
            Debug.Log("[Train Manager] Перед запуском сцена пересобирается: " + reason);
            EditorApplication.delayCall += () =>
            {
                SaveOpenScenesWithPath();
                if (!ParisPrototypeBuilder.Build(false))
                {
                    Debug.LogError("[Train Manager] Сцена не собралась — пришли Claude скриншот Console");
                    return;
                }
                SetPlayStartScene();
                justRebuilt = true;
                EditorApplication.isPlaying = true;
            };
        }

        /// <summary>Почему сцену надо пересобрать; null — сцена в порядке.</summary>
        public static string WhyRebuild()
        {
            if (!File.Exists(ScenePath)) return "сцены ещё нет";

            string text;
            try { text = File.ReadAllText(ScenePath); }
            catch (IOException e) { return "сцену не прочитать (" + e.Message + ")"; }

            Match build = Regex.Match(text, @"sceneLayoutBuild: (\d+)");
            if (!build.Success || build.Groups[1].Value != ParisPrototypeBuilder.LayoutBuild.ToString())
                return "новая версия интерфейса " + ParisPrototypeBuilder.LayoutVersion;

            Match hash = Regex.Match(text, @"mapSourceHash: '?([0-9a-f]*)'?");
            if (!hash.Success || hash.Groups[1].Value != MapDataImporter.SourceHash())
                return "изменились файлы карты (Assets/Data/MapSource)";

            Match db = Regex.Match(text, @"database: \{fileID: \d+, guid: ([0-9a-f]{32})");
            if (!db.Success || AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(db.Groups[1].Value)) == null)
                return "в сцене потерялась база игры (GameDatabase)";

            // Все ассеты, на которые ссылается сцена (города, линии, скрипты, картинки), должны существовать
            var seen = new HashSet<string>();
            foreach (Match m in Regex.Matches(text, @"guid: ([0-9a-f]{32})"))
            {
                string guid = m.Groups[1].Value;
                if (guid.StartsWith("0000000000000000") || !seen.Add(guid)) continue; // встроенные ресурсы Unity
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets/")) continue; // пакеты Unity не меняются
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                    return "в сцене есть ссылка на пропавший ассет (" + (string.IsNullOrEmpty(path) ? guid : path) + ")";
            }
            return null;
        }

        private static void SetPlayStartScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene != null) EditorSceneManager.playModeStartScene = scene;
        }

        /// <summary>Сохранить изменённые открытые сцены (у которых есть файл), чтобы пересборка их не потеряла.</summary>
        private static void SaveOpenScenesWithPath()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path) && scene.path != ScenePath)
                    EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
