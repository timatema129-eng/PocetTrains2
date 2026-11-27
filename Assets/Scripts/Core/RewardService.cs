using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>Выдача универсальной награды (задания, достижения, позже реклама).</summary>
    public class RewardService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;
        private readonly PartsService parts;
        private readonly GameEvents events;

        public RewardService(GameState state, GameDatabase db, WalletService wallet, PartsService parts, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
            this.parts = parts;
            this.events = events;
        }

        public void Grant(RewardData reward, string reason)
        {
            if (reward == null) return;
            if (reward.coins > 0) wallet.Add(Currency.Coins, reward.coins, reason);
            if (reward.dollars > 0) wallet.Add(Currency.Dollars, reward.dollars, reason);
            bool partsChanged = false;
            if (reward.scrap > 0)
            {
                state.wallet.scrap += reward.scrap;
                partsChanged = true;
            }
            if (reward.boxIds != null)
            {
                foreach (string boxId in reward.boxIds)
                {
                    if (db.GetBox(boxId) == null)
                    {
                        Debug.LogWarning($"[Reward] Нет бокса «{boxId}» в GameDatabase ({reason})");
                        continue;
                    }
                    state.wallet.boxes.Add(boxId);
                    partsChanged = true;
                }
            }
            if (!string.IsNullOrEmpty(reward.partsTrainId) && reward.partsCount > 0 && db.GetTrain(reward.partsTrainId) != null)
            {
                parts.AddParts(reward.partsTrainId, reward.partsCount);
                partsChanged = true;
            }
            if (partsChanged) events.RaisePartsChanged();
            Debug.Log($"[Reward] {reason}: {Describe(reward)}");
        }

        /// <summary>«200 монет, 1 × Эпический бокс».</summary>
        public string Describe(RewardData reward)
        {
            if (reward == null || reward.IsEmpty) return "—";
            var items = new System.Collections.Generic.List<string>();
            if (reward.coins > 0) items.Add($"{reward.coins} монет");
            if (reward.dollars > 0) items.Add($"{reward.dollars} $");
            if (reward.scrap > 0) items.Add($"{reward.scrap} лома");
            if (reward.boxIds != null)
                foreach (string id in reward.boxIds)
                {
                    BoxConfig box = db.GetBox(id);
                    items.Add(box != null ? box.displayName : id);
                }
            if (!string.IsNullOrEmpty(reward.partsTrainId) && reward.partsCount > 0)
            {
                TrainData t = db.GetTrain(reward.partsTrainId);
                items.Add($"{reward.partsCount} дет. «{(t != null ? t.displayName : reward.partsTrainId)}»");
            }
            return string.Join(", ", items);
        }
    }
}
