using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrainManager.EditorTools
{
    /// <summary>
    /// Сборка APK без участия человека (GitHub Actions вызывает CIBuild.BuildAndroid).
    /// Можно запустить и руками: Tools → Train Manager → Build Android APK.
    /// Сцена генерируется сборщиком (её нет в репозитории), поэтому сначала собираем её.
    /// </summary>
    public static class CIBuild
    {
        private const string ApkPath = "Build/Android/TrainManager.apk";

        [MenuItem("Tools/Train Manager/Build Android APK")]
        public static void BuildAndroidMenu() => BuildAndroid();

        public static void BuildAndroid()
        {
            if (!ParisPrototypeBuilder.Build(false))
            {
                Debug.LogError("[Train Manager] Сцена не собралась — APK не собираем");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            PlayerSettings.productName = "Train Manager";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.trainmanager.game");
            // Игра в альбомной ориентации (поворот только между двумя альбомными)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false; // APK, чтобы ставить на телефон напрямую

            string number = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (int.TryParse(number, out int code)) PlayerSettings.Android.bundleVersionCode = code;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/ParisPrototype.unity" },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Train Manager] Сборка APK: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} МБ → {ApkPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
