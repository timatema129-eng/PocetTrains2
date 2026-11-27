using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainManager.EditorTools
{
    /// <summary>
    /// После обновления проекта (Pull) и компиляции: если сцена собрана старой версией сборщика,
    /// предлагает пересобрать её одной кнопкой. Иначе в игре остаётся старый интерфейс.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneVersionCheck
    {
        private const string ScenePath = "Assets/Scenes/ParisPrototype.unity";

        static SceneVersionCheck()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string key = ParisPrototypeBuilder.LayoutVersionPref + "." + Application.dataPath;
            if (EditorPrefs.GetString(key, "") == ParisPrototypeBuilder.LayoutVersion) return;
            // Запоминаем сразу: если откажется, не спрашиваем при каждой компиляции (пересобрать можно из меню)
            EditorPrefs.SetString(key, ParisPrototypeBuilder.LayoutVersion);

            bool hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            string text = hasScene
                ? "Код обновился: новая версия интерфейса — " + ParisPrototypeBuilder.LayoutVersion +
                  ".\nПересобрать сцену ParisPrototype сейчас? Без этого в игре останется старый вид.\n(Сохранение игры не удаляется.)"
                : "Сцены ParisPrototype ещё нет. Собрать её сейчас?";
            if (EditorUtility.DisplayDialog("Train Manager", text, "Пересобрать", "Позже"))
            {
                ParisPrototypeBuilder.Build(true, false);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                    EditorSceneManager.OpenScene(ScenePath);
            }
        }
    }
}
