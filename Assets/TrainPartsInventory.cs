using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Хранит части поездов и управляет сборкой/разборкой
/// </summary>
public class TrainPartsInventory : MonoBehaviour
{
    public static TrainPartsInventory Instance;

    // ===== ДАННЫЕ =====

    private Dictionary<string, int> parts = new Dictionary<string, int>();

    private const string SAVE_KEY = "TRAIN_PARTS";

    // ===== UNITY =====

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== ОСНОВНЫЕ МЕТОДЫ =====

    /// <summary>
    /// Добавить части поезда
    /// </summary>
    public void AddPart(string trainId, int count)
    {
        if (count <= 0) return;

        if (!parts.ContainsKey(trainId))
            parts[trainId] = 0;

        parts[trainId] += count;

        Save();
    }

    /// <summary>
    /// Удалить части
    /// </summary>
    public bool RemovePart(string trainId, int count)
    {
        if (count <= 0) return false;

        if (!parts.ContainsKey(trainId)) return false;

        if (parts[trainId] >= count)
        {
            parts[trainId] -= count;

            if (parts[trainId] <= 0)
                parts.Remove(trainId);

            Save();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Получить количество частей
    /// </summary>
    public int GetPartCount(string trainId)
    {
        if (parts.ContainsKey(trainId))
            return parts[trainId];

        return 0;
    }

    /// <summary>
    /// Проверка — можно ли собрать поезд
    /// </summary>
    public bool CanAssembleTrain(TrainData train)
    {
        int current = GetPartCount(train.trainId);
        return current >= train.partsRequiredForAssembly;
    }

    /// <summary>
    /// Собрать поезд
    /// </summary>
    public bool AssembleTrain(TrainData train)
    {
        if (!CanAssembleTrain(train))
            return false;

        // списываем части
        RemovePart(train.trainId, train.partsRequiredForAssembly);

        // добавляем поезд игроку
        if (!PlayerManager.Instance.TrainIds.Contains(train.trainId))
        {
            PlayerManager.Instance.TrainIds.Add(train.trainId);
        }

        GameEventsHub.OnTrainAssembled?.Invoke(train.trainId);

        Debug.Log($"Train assembled: {train.trainName}");

        return true;
    }

    /// <summary>
    /// Разобрать поезд
    /// </summary>
    public bool DisassembleTrain(string trainId, TrainData train)
    {
        // есть ли поезд у игрока
        if (!PlayerManager.Instance.TrainIds.Contains(trainId))
            return false;

        // удаляем поезд
        PlayerManager.Instance.TrainIds.Remove(trainId);

        // возвращаем половину частей
        int returnedParts = Mathf.FloorToInt(train.partsRequiredForAssembly * 0.5f);

        AddPart(trainId, returnedParts);

        Debug.Log($"Train disassembled: {trainId}");

        return true;
    }

    // ===== СОХРАНЕНИЕ =====

    private void Save()
    {
        SaveWrapper wrapper = new SaveWrapper(parts);

        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log("Train parts saved");
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");

        if (string.IsNullOrEmpty(json))
        {
            parts = new Dictionary<string, int>();
            return;
        }

        SaveWrapper wrapper = JsonUtility.FromJson<SaveWrapper>(json);
        parts = wrapper.ToDictionary();

        Debug.Log("Train parts loaded");
    }

    // ===== СЕРИАЛИЗАЦИЯ =====

    [System.Serializable]
    private class SaveWrapper
    {
        public List<string> keys = new List<string>();
        public List<int> values = new List<int>();

        public SaveWrapper(Dictionary<string, int> dict)
        {
            foreach (var pair in dict)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public Dictionary<string, int> ToDictionary()
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();

            for (int i = 0; i < keys.Count; i++)
            {
                dict[keys[i]] = values[i];
            }

            return dict;
        }
    }
}