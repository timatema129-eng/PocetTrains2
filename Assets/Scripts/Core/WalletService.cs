using UnityEngine;

namespace TrainManager.Core
{
    public enum Currency
    {
        Coins,
        Dollars
    }

    /// <summary>
    /// Единственное место, где меняется баланс. Списание только если хватает (в минус не уходит).
    /// Каждое изменение пишется в Console и вызывает событие WalletChanged.
    /// Обмен — только доллары → монеты (Этап 9).
    /// </summary>
    public class WalletService
    {
        private readonly GameState state;
        private readonly GameEvents events;

        public WalletService(GameState state, GameEvents events)
        {
            this.state = state;
            this.events = events;
            if (state.wallet == null) state.wallet = new Wallet();
        }

        public long Coins => state.wallet.coins;
        public long Dollars => state.wallet.dollars;

        public long Get(Currency currency) => currency == Currency.Coins ? state.wallet.coins : state.wallet.dollars;

        public bool CanAfford(Currency currency, long amount) => amount >= 0 && Get(currency) >= amount;

        /// <summary>Начислить. amount должен быть ≥ 0.</summary>
        public void Add(Currency currency, long amount, string reason)
        {
            if (amount < 0)
            {
                Debug.LogError($"[Wallet] Отрицательное начисление {amount} ({reason}) — используй TrySpend");
                return;
            }
            if (amount == 0) return;

            Set(currency, Get(currency) + amount);
            Log(currency, amount, reason);
        }

        /// <summary>Списать, если хватает. false — не хватает (баланс не меняется).</summary>
        public bool TrySpend(Currency currency, long amount, string reason)
        {
            if (amount < 0)
            {
                Debug.LogError($"[Wallet] Отрицательное списание {amount} ({reason})");
                return false;
            }
            if (!CanAfford(currency, amount))
            {
                Debug.Log($"[Wallet] Не хватает: нужно {amount} {Name(currency)}, есть {Get(currency)} ({reason})");
                return false;
            }
            if (amount == 0) return true;

            Set(currency, Get(currency) - amount);
            Log(currency, -amount, reason);
            return true;
        }

        public static string Name(Currency currency) => currency == Currency.Coins ? "монет" : "$";

        private void Set(Currency currency, long value)
        {
            if (currency == Currency.Coins) state.wallet.coins = value;
            else state.wallet.dollars = value;
        }

        private void Log(Currency currency, long delta, string reason)
        {
            long balance = Get(currency);
            Debug.Log($"[Wallet] {(delta > 0 ? "+" : "")}{delta} {Name(currency)} ({reason}). Баланс: {balance}");
            events.RaiseWalletChanged(currency, balance, delta, reason);
        }
    }
}
