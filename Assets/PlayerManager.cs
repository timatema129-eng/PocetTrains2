using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Главный менеджер игрока.
/// Хранит валюту, поезда и отвечает за сохранение.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    // ===== ДАННЫЕ ИГРОКА =====

    [Header("Currencies")]
    public int Coins;
    public int Dollars;

    [Header("Trains")]
    public List<string> TrainIds = new List<string>();

    // ===== КЛЮЧИ СОХРАНЕНИЯ =====

    private const string COINS_KEY = "PLAYER_COINS";
    private const string DOLLARS_KEY = "PLAYER_DOLLARS";
    private const string TRAINS_KEY = "PLAYER_TRAINS";

    // ===== UNITY =====

    private void Awake()
    {
        // Singleton логика
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // не уничтожается при смене сцен
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== МЕТОДЫ ВАЛЮТЫ =====

    /// <summary>
    /// Добавить монеты
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        Coins += amount;
        Save();
    }

    /// <summary>
    /// Добавить доллары
    /// </summary>
    public void AddDollars(int amount)
    {
        if (amount <= 0) return;

        Dollars += amount;
        Save();
    }

    /// <summary>
    /// Потратить монеты
    /// </summary>
    /// <returns>Хватает ли монет</returns>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return false;

        if (Coins >= amount)
        {
            Coins -= amount;
            Save();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Потратить доллары
    /// </summary>
    public bool SpendDollars(int amount)
    {
        if (amount <= 0) return false;

        if (Dollars >= amount)
        {
            Dollars -= amount;
            Save();
            return true;
        }

        return false;
    }

    // ===== СОХРАНЕНИЕ =====

    /// <summary>
    /// Сохранение всех данных
    /// </summary>
    private void Save()
    {
        PlayerPrefs.SetInt(COINS_KEY, Coins);
        PlayerPrefs.SetInt(DOLLARS_KEY, Dollars);

        // Сериализация списка поездов в JSON
        string trainsJson = JsonUtility.ToJson(new TrainListWrapper(TrainIds));
        PlayerPrefs.SetString(TRAINS_KEY, trainsJson);

        PlayerPrefs.Save();

        Debug.Log("Player data saved");
    }

    /// <summary>
    /// Загрузка данных
    /// </summary>
    private void Load()
    {
        Coins = PlayerPrefs.GetInt(COINS_KEY, 0);
        Dollars = PlayerPrefs.GetInt(DOLLARS_KEY, 0);

        string trainsJson = PlayerPrefs.GetString(TRAINS_KEY, "");

        if (!string.IsNullOrEmpty(trainsJson))
        {
            TrainListWrapper wrapper = JsonUtility.FromJson<TrainListWrapper>(trainsJson);
            TrainIds = wrapper.ids;
        }
        else
        {
            TrainIds = new List<string>();
        }

        Debug.Log("Player data loaded");
    }

    // ===== ВСПОМОГАТЕЛЬНЫЙ КЛАСС =====

    /// <summary>
    /// Обёртка для сериализации списка в JSON
    /// (JsonUtility не умеет напрямую List)
    /// </summary>
    [System.Serializable]
    private class TrainListWrapper
    {
        public List<string> ids;

        public TrainListWrapper(List<string> ids)
        {
            this.ids = ids;
        }
    }
}