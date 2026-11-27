using UnityEngine;

/// <summary>
/// Система достижений
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    private float totalDistance = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        GameEventsHub.OnCityUnlocked += HandleCityUnlocked;
        GameEventsHub.OnTrainAssembled += HandleTrainAssembled;
        GameEventsHub.OnDistanceTraveled += HandleDistance;
    }

    private void OnDisable()
    {
        GameEventsHub.OnCityUnlocked -= HandleCityUnlocked;
        GameEventsHub.OnTrainAssembled -= HandleTrainAssembled;
        GameEventsHub.OnDistanceTraveled -= HandleDistance;
    }

    // ===== ОБРАБОТЧИКИ =====

    private void HandleCityUnlocked(string cityId)
    {
        PlayerManager.Instance.AddDollars(5);
        Debug.Log($"Achievement: City unlocked → {cityId}");
    }

    private void HandleTrainAssembled(string trainId)
    {
        PlayerManager.Instance.AddCoins(200);
        Debug.Log($"Achievement: Train assembled → {trainId}");
    }

    private void HandleDistance(float distance)
    {
        totalDistance += distance;

        if (totalDistance >= 1000f)
        {
            totalDistance = 0f;

            PlayerManager.Instance.AddDollars(2);
            Debug.Log("Achievement: 1000 km traveled");
        }
    }
}