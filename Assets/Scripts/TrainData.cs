using UnityEngine;

/// <summary>
/// ScriptableObject, описывающий поезд
/// </summary>
[CreateAssetMenu(fileName = "TrainData", menuName = "Game/Train Data")]
public class TrainData : ScriptableObject
{
    // ===== ENUM =====

    public enum TrainRarity
    {
        Common,
        Rare,
        Epic
    }

    // ===== ОСНОВНЫЕ ДАННЫЕ =====

    [Header("ID")]
    public string trainId; // уникальный ID (например: train_standard)

    [Header("Info")]
    public string trainName;
    public TrainRarity rarity;

    [Header("Stats")]
    [Range(50, 200)]
    public float baseSpeed;

    [Range(8, 16)]
    public int maxWagons;

    [Range(25, 400)]
    public int maxFuel;

    [Range(1, 4)]
    public int maxFuelWagons;

    [Header("Assembly")]
    public int partsRequiredForAssembly;

    [Header("Visual")]
    public Sprite sprite;

    // ===== МЕТОДЫ =====

    /// <summary>
    /// Возвращает красивую строку со статами для UI
    /// </summary>
    public string GetStatsString()
    {
        return
            $"<b>{trainName}</b>\n" +
            $"Редкость: {rarity}\n" +
            $"Скорость: {baseSpeed}\n" +
            $"Вагонов: {maxWagons}\n" +
            $"Топливо: {maxFuel}\n" +
            $"Топливные вагоны: {maxFuelWagons}\n" +
            $"Сборка: {partsRequiredForAssembly} частей";
    }
}