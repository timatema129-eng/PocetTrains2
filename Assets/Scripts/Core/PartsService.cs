using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Боксы, детали, сборка и разборка поездов (Этап 8).
    /// Бокс открывается только за доллары и даёт 1 деталь случайной модели своей редкости.
    /// Детали копятся в бесконечном хранилище (Wallet.parts). Когда деталей хватает — поезд можно собрать.
    /// Разборка превращает поезд в лом (Wallet.scrap). Поломки и ремонт — не сделаны (правила не описаны).
    /// </summary>
    public class PartsService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;
        private readonly TrainService trains;
        private readonly GameEvents events;
        private readonly System.Random random = new System.Random();

        public PartsService(GameState state, GameDatabase db, WalletService wallet, TrainService trains, GameEvents events)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
            this.trains = trains;
            this.events = events;
            if (state.wallet.parts == null) state.wallet.parts = new List<PartStack>();
        }

        public long Scrap => state.wallet.scrap;

        // ================= Детали =================

        public int GetParts(string trainId)
        {
            PartStack stack = FindStack(trainId);
            return stack != null ? stack.count : 0;
        }

        public IReadOnlyList<PartStack> AllParts => state.wallet.parts;

        private PartStack FindStack(string trainId)
        {
            foreach (PartStack s in state.wallet.parts)
                if (s != null && s.trainId == trainId) return s;
            return null;
        }

        private void AddParts(string trainId, int count)
        {
            PartStack stack = FindStack(trainId);
            if (stack == null)
            {
                stack = new PartStack { trainId = trainId };
                state.wallet.parts.Add(stack);
            }
            stack.count += count;
        }

        // ================= Боксы =================

        public bool CanOpenBox(BoxConfig box, out string reason)
        {
            if (box == null)
            {
                reason = "бокс не найден";
                return false;
            }
            if (db.GetTrainsOfRarity(box.rarity).Count == 0)
            {
                reason = "нет моделей поездов этой редкости";
                return false;
            }
            if (!wallet.CanAfford(Currency.Dollars, box.priceDollars))
            {
                reason = $"нужно {box.priceDollars} $";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Открыть бокс. dropped — модель, деталь которой выпала.</summary>
        public bool TryOpenBox(BoxConfig box, out TrainData dropped)
        {
            dropped = null;
            if (!CanOpenBox(box, out string reason))
            {
                Debug.LogWarning($"[Parts] Открыть бокс нельзя: {reason}");
                return false;
            }
            if (!wallet.TrySpend(Currency.Dollars, box.priceDollars, box.displayName))
                return false;

            List<TrainData> pool = db.GetTrainsOfRarity(box.rarity);
            dropped = pool[random.Next(pool.Count)];
            AddParts(dropped.trainId, 1);
            Debug.Log($"[Parts] {box.displayName}: деталь «{dropped.displayName}», теперь " +
                      $"{GetParts(dropped.trainId)}/{dropped.partsRequiredForAssembly}");
            events.RaisePartsChanged();
            return true;
        }

        // ================= Сборка =================

        public bool CanAssemble(TrainData model, out string reason)
        {
            if (model == null)
            {
                reason = "модель не найдена";
                return false;
            }
            int have = GetParts(model.trainId);
            if (have < model.partsRequiredForAssembly)
            {
                reason = $"деталей {have}/{model.partsRequiredForAssembly}";
                return false;
            }
            if (db.startCity == null)
            {
                reason = "не задан стартовый город";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Собрать поезд из деталей. Новый поезд появляется в стартовом городе (рабочее правило).
        /// </summary>
        public TrainInstance TryAssemble(TrainData model)
        {
            if (!CanAssemble(model, out string reason))
            {
                Debug.LogWarning($"[Parts] Собрать нельзя: {reason}");
                return null;
            }
            FindStack(model.trainId).count -= model.partsRequiredForAssembly;
            TrainInstance train = trains.CreateTrain(model, db.startCity);
            Debug.Log($"[Parts] Собран поезд «{model.displayName}» в городе {db.startCity.displayName}");
            events.RaisePartsChanged();
            events.RaiseTrainAssembled(train);
            return train;
        }

        // ================= Разборка =================

        /// <summary>Сколько лома даст разборка.</summary>
        public int GetScrapFor(TrainInstance train)
        {
            TrainData model = trains.GetModel(train);
            return model != null ? model.partsRequiredForAssembly * db.scrapPerPart : 0;
        }

        public bool CanDisassemble(TrainInstance train, out string reason)
        {
            if (train == null)
            {
                reason = "поезд не найден";
                return false;
            }
            if (train.IsMoving)
            {
                reason = "поезд в пути";
                return false;
            }
            if (train.state == TrainManager.Trains.TrainState.Stranded)
            {
                reason = "поезд стоит на линии — сначала буксировка";
                return false;
            }
            if (train.wagonIds != null && train.wagonIds.Count > 0)
            {
                reason = "сначала отцепи вагоны";
                return false;
            }
            if (state.trains.Count <= 1)
            {
                reason = "это последний поезд";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryDisassemble(TrainInstance train)
        {
            if (!CanDisassemble(train, out string reason))
            {
                Debug.LogWarning($"[Parts] Разобрать нельзя: {reason}");
                return false;
            }
            int scrap = GetScrapFor(train);
            state.trains.Remove(train);
            state.wallet.scrap += scrap;
            Debug.Log($"[Parts] Поезд «{trains.GetDisplayName(train)}» {train.instanceId} разобран: +{scrap} лома, всего {Scrap}");
            events.RaisePartsChanged();
            events.RaiseTrainDisassembled(train);
            return true;
        }
    }
}
