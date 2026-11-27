using TMPro;
using UnityEngine;
using TrainManager.Core;

namespace TrainManager.UI
{
    /// <summary>Баланс игрока на экране: монеты и доллары.</summary>
    public class WalletText : MonoBehaviour
    {
        public GameRoot root;
        public TMP_Text text;

        private long shownCoins = -1, shownDollars = -1;

        private void Update()
        {
            if (root == null || text == null || !root.IsReady) return;

            long coins = root.Wallet.Coins, dollars = root.Wallet.Dollars;
            if (coins == shownCoins && dollars == shownDollars) return;

            shownCoins = coins;
            shownDollars = dollars;
            text.text = $"Монеты: {coins}    Доллары: {dollars}";
        }
    }
}
