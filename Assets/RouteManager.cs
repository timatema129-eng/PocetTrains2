using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Управляет маршрутами поездов
/// </summary>
public class RouteManager : MonoBehaviour
{
    public static RouteManager Instance;

    private Dictionary<string, TrainRoute> activeRoutes = new Dictionary<string, TrainRoute>();

    private const float MOVE_INTERVAL = 2f; // каждые N секунд

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

    // ===== НАЗНАЧЕНИЕ МАРШРУТА =====

    public void AssignRoute(string trainId, List<string> waypoints, Dictionary<string, int> deliveries)
    {
        TrainRoute route = new TrainRoute
        {
            trainId = trainId,
            waypoints = waypoints,
            wagonsToDeliver = deliveries
        };

        activeRoutes[trainId] = route;

        Debug.Log($"Route assigned to train: {trainId}");
    }

    // ===== РАСЧЁТ ТОПЛИВА =====

    public int CalculateFuelCost(string trainId, List<string> waypoints, Dictionary<string, int> deliveries)
    {
        float totalDistance = 0f;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            var cityA = GetCity(waypoints[i]);
            totalDistance += cityA.baseDistance;
        }

        int totalWagons = deliveries.Values.Sum();

        int firstCityWagons = deliveries.ContainsKey(waypoints[0])
            ? deliveries[waypoints[0]]
            : 0;

        int extraWagons = totalWagons - firstCityWagons;

        float multiplier = 1f + 0.1f * extraWagons;

        float fuel = totalDistance * multiplier;

        return Mathf.RoundToInt(fuel);
    }

    // ===== СТАРТ ДВИЖЕНИЯ =====

    public void StartJourney(string trainId)
    {
        if (!activeRoutes.ContainsKey(trainId))
        {
            Debug.LogError("Route not found for train: " + trainId);
            return;
        }

        TrainRoute route = activeRoutes[trainId];

        // 🔥 ВАЖНО: пока без реального топлива у поезда
        int fuelCost = CalculateFuelCost(trainId, route.waypoints, route.wagonsToDeliver);

        // временно считаем что всегда хватает
        Debug.Log($"Train {trainId} started. Fuel cost: {fuelCost}");

        StartCoroutine(MoveTrain(route));
    }

    // ===== ДВИЖЕНИЕ =====

    private IEnumerator MoveTrain(TrainRoute route)
    {
        for (int i = 0; i < route.waypoints.Count - 1; i++)
        {
            string fromCity = route.waypoints[i];
            string toCity = route.waypoints[i + 1];

            CityData city = GetCity(fromCity);

            float distance = city.baseDistance;
            float traveled = 0f;

            Debug.Log($"Moving from {fromCity} to {toCity}");

            while (traveled < distance)
            {
                yield return new WaitForSeconds(MOVE_INTERVAL);

                float traveledStep = distance * 0.2f;
                traveled += traveledStep;

                GameEventsHub.OnDistanceTraveled?.Invoke(traveledStep);

                Debug.Log($"Train {route.trainId} progress: {traveled}/{distance}");
            }

            // Прибытие
            OnArrive(route, toCity);
        }

        Debug.Log($"Train {route.trainId} finished route");
    }

    // ===== ПРИБЫТИЕ =====

    private void OnArrive(TrainRoute route, string cityId)
    {
        Debug.Log($"Arrived at {cityId}");

        // выгрузка вагонов
        if (route.wagonsToDeliver.ContainsKey(cityId))
        {
            int delivered = route.wagonsToDeliver[cityId];

            // награда (временно простая формула)
            int coinsReward = delivered * 50;
            int dollarsReward = delivered > 3 ? 1 : 0;

            PlayerManager.Instance.AddCoins(coinsReward);

            if (dollarsReward > 0)
                PlayerManager.Instance.AddDollars(dollarsReward);

            Debug.Log($"Delivered {delivered} wagons. Reward: {coinsReward} coins");

            // удалить из списка доставок
            route.wagonsToDeliver.Remove(cityId);
        }

        // TODO: ивенты (в будущем)
        CheckEvents(route, cityId);
    }

    private void CheckEvents(TrainRoute route, string cityId)
    {
        // Заглушка под будущую систему ивентов
        Debug.Log($"Checking events at {cityId}");
    }

    // ===== ВСПОМОГАТЕЛЬНОЕ =====

    private CityData GetCity(string cityId)
    {
        return StationManager.Instance.GetCityById(cityId);
    }
}
