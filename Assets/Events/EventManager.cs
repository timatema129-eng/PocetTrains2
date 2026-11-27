using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет ивентами в городах
/// </summary>
public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    [Header("Available Events")]
    public List<GameEvent> eventPrefabs;

    // cityId → список ивентов
    private Dictionary<string, List<GameEvent>> activeEvents = new Dictionary<string, List<GameEvent>>();

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

    // ===== СПАВН =====

    public void SpawnRandomEvent(string cityId)
    {
        if (eventPrefabs == null || eventPrefabs.Count == 0)
        {
            Debug.LogWarning("No event prefabs");
            return;
        }

        int index = Random.Range(0, eventPrefabs.Count);
        GameEvent prefab = eventPrefabs[index];

        GameEvent newEvent = Instantiate(prefab, transform);

        if (!activeEvents.ContainsKey(cityId))
            activeEvents[cityId] = new List<GameEvent>();

        activeEvents[cityId].Add(newEvent);

        Debug.Log($"Event spawned in {cityId}: {newEvent.eventId}");
    }

    // ===== ЗАВЕРШЕНИЕ =====

    public void CompleteEvent(string eventId, string cityId)
    {
        if (!activeEvents.ContainsKey(cityId)) return;

        var list = activeEvents[cityId];

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].eventId == eventId)
            {
                list[i].Complete();
                list.RemoveAt(i);

                Debug.Log($"Event completed in {cityId}");
                return;
            }
        }
    }
}