using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет открытием лутбоксов
/// </summary>
public class LootBoxManager : MonoBehaviour
{
    public static LootBoxManager Instance;

    // ===== ЛУТ ПУЛЫ =====

    [Header("Loot Pools")]

    public List<TrainData> commonTrains;
    public List<TrainData> rareTrains;
    public List<TrainData> epicTrains;

    // ===== ЦЕНЫ =====

    private const int COMMON_COST = 5;
    private const int RARE_COST = 20;
    private const int EPIC_COST = 100;

    // ===== UNITY =====

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== ОСНОВНОЙ МЕТОД =====

    /// <summary>
    /// Открыть бокс
    /// </summary>
    public void OpenBox(string boxType)
    {
        int cost = 0;
        List<TrainData> pool = null;

        // Определяем тип бокса
        switch (boxType.ToLower())
        {
            case "common":
                cost = COMMON_COST;
                pool = commonTrains;
                break;

            case "rare":
                cost = RARE_COST;
                pool = rareTrains;
                break;

            case "epic":
                cost = EPIC_COST;
                pool = epicTrains;
                break;

            default:
                Debug.LogError("Unknown box type: " + boxType);
                return;
        }

        // Проверка валюты
        if (!PlayerManager.Instance.SpendDollars(cost))
        {
            Debug.Log("Недостаточно долларов");
            return;
        }

        // Проверка пула
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError("Пул поездов пуст для типа: " + boxType);
            return;
        }

        // Рандомный поезд
        int index = Random.Range(0, pool.Count);
        TrainData rewardTrain = pool[index];

        // Выдаём 1 часть
        TrainPartsInventory.Instance.AddPart(rewardTrain.trainId, 1);

        // Показываем результат
        ShowRewardPopup(rewardTrain);
    }

    // ===== UI (временно через Debug) =====

    private void ShowRewardPopup(TrainData train)
    {
        Debug.Log($"Вам выпала часть поезда: {train.trainName}");
    }
}