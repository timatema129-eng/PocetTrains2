using UnityEngine;

/// <summary>
/// Награды за события
/// </summary>
[CreateAssetMenu(fileName = "RewardData", menuName = "Game/Reward Data")]
public class RewardData : ScriptableObject
{
    public int coins;
    public int dollars;

    public void GiveReward()
    {
        PlayerManager.Instance.AddCoins(coins);
        PlayerManager.Instance.AddDollars(dollars);
    }
}