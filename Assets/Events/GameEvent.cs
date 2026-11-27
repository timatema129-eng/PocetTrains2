using UnityEngine;

/// <summary>
/// Ѕазовый класс игрового событи€
/// </summary>
public class GameEvent : MonoBehaviour
{
    public string eventId;
    [TextArea] public string description;

    public float timeRemaining = -1f; // -1 = бесконечный

    public RewardData reward;

    protected bool isCompleted = false;

    protected virtual void Update()
    {
        if (timeRemaining < 0) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0)
        {
            OnExpired();
        }
    }

    public virtual void Complete()
    {
        if (isCompleted) return;

        isCompleted = true;

        reward?.GiveReward();

        Debug.Log($"Event completed: {eventId}");
    }

    protected virtual void OnExpired()
    {
        Debug.Log($"Event expired: {eventId}");
        Destroy(this);
    }
}