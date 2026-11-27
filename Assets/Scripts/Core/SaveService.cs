using System;
using System.IO;
using UnityEngine;

namespace TrainManager.Core
{
    /// <summary>
    /// Сохранение GameState в JSON-файл на устройстве (Application.persistentDataPath/save.json).
    /// Запись через временный файл: если игра упадёт посреди записи, старое сохранение не испортится.
    /// </summary>
    public class SaveService
    {
        public const string FileName = "save.json";

        private readonly IGameClock clock;

        public SaveService(IGameClock clock)
        {
            this.clock = clock;
        }

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(SavePath);

        public void Save(GameState state)
        {
            if (state == null) return;

            string path = SavePath;
            string tmp = path + ".tmp";
            try
            {
                state.version = GameState.CurrentVersion;
                state.savedUnixMs = clock.NowUnixMs;
                File.WriteAllText(tmp, JsonUtility.ToJson(state, true));

                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Не удалось сохранить в {path}: {e.Message}");
            }
        }

        /// <summary>Загрузить сохранение. null — сохранения нет или файл повреждён.</summary>
        public GameState Load()
        {
            string path = SavePath;
            if (!File.Exists(path)) return null;

            try
            {
                GameState state = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                if (state == null) throw new Exception("пустой файл");

                if (state.version > GameState.CurrentVersion)
                    Debug.LogWarning($"[Save] Сохранение новее игры (версия {state.version}), часть данных может потеряться");

                Debug.Log($"[Save] Загружено: {path}");
                return state;
            }
            catch (Exception e)
            {
                // Битый файл не удаляем, а откладываем в сторону — чтобы можно было разобраться.
                string broken = path + ".broken";
                Debug.LogError($"[Save] Сохранение повреждено ({e.Message}). Файл перемещён в {broken}, начинается новая игра");
                try
                {
                    if (File.Exists(broken)) File.Delete(broken);
                    File.Move(path, broken);
                }
                catch (Exception moveError)
                {
                    Debug.LogError($"[Save] Не удалось переместить файл: {moveError.Message}");
                }
                return null;
            }
        }

        /// <summary>Отложить сохранение в сторону (save.json + suffix), чтобы начать заново, не теряя файл.</summary>
        public void MoveAside(string suffix)
        {
            string path = SavePath;
            if (!File.Exists(path)) return;
            try
            {
                string target = path + suffix;
                if (File.Exists(target)) File.Delete(target);
                File.Move(path, target);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Не удалось отложить сохранение: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
