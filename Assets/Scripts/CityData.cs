using UnityEngine;

/// <summary>
/// ScriptableObject города (станции)
/// </summary>
[CreateAssetMenu(fileName = "CityData", menuName = "Game/City Data")]
public class CityData : ScriptableObject
{
    [Header("ID")]
    public string cityId;

    [Header("Info")]
    public string cityName;
    public string region; // влияет на цвет линии и фон

    [Header("Distance")]
    public float baseDistance;

    [Header("Visual")]
    public Sprite backgroundSprite;

    [Header("Unlock")]
    public bool unlocked = false;
    public int unlockCost;

    [Header("Station")]
    public int maxWagonSlots = 5;
}