using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.Core
{
    /// <summary>
    /// Топливо и толчок.
    /// Поездка тратит: толчок (на каждое НОВОЕ отправление) + расстояние × fuelPerKm.
    /// Толчок = fuelPushBase + fuelPushPerWagon × грузовых вагонов.
    /// Проезд промежуточной станции с выгрузкой нового толчка не требует, поэтому сквозной маршрут
    /// Минск → Берлин → Амстердам дешевле, чем два отдельных рейса.
    /// Топливо списывается целиком при отправлении. Запас = maxFuel модели + топливные вагоны.
    /// </summary>
    public class FuelService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly WalletService wallet;

        public FuelService(GameState state, GameDatabase db, WalletService wallet)
        {
            this.state = state;
            this.db = db;
            this.wallet = wallet;
        }

        public int CountWagons(TrainInstance train, WagonKind kind)
        {
            int count = 0;
            if (train == null) return 0;
            foreach (string id in train.wagonIds)
            {
                WagonInstance w = state.FindWagon(id);
                WagonData type = w != null ? db.GetWagonType(w.wagonTypeId) : null;
                if (type != null && type.kind == kind) count++;
            }
            return count;
        }

        /// <summary>Максимальный запас топлива: модель + топливные вагоны в составе.</summary>
        public float GetCapacity(TrainInstance train)
        {
            TrainData model = train != null ? db.GetTrain(train.modelId) : null;
            if (model == null) return 0f;
            return model.maxFuel + CountWagons(train, WagonKind.Fuel) * model.fuelPerFuelWagon;
        }

        /// <summary>Толчок при отправлении этого поезда (с текущим составом).</summary>
        public float GetPushFuel(TrainInstance train)
        {
            return db.fuelPushBase + db.fuelPushPerWagon * CountWagons(train, WagonKind.Cargo);
        }

        /// <summary>Сколько топлива уйдёт на поездку по цепочке (один толчок + расстояние). -1 — нет линии.</summary>
        public float GetTripFuel(TrainInstance train, float distanceKm)
        {
            if (distanceKm < 0f) return -1f;
            return GetPushFuel(train) + distanceKm * db.fuelPerKm;
        }

        /// <summary>
        /// То же с учётом электричества: электропоезд не тратит топливо на электрифицированных километрах,
        /// а если электрифицирован весь маршрут — не тратит и толчок. Обычному поезду электричество ничего не даёт.
        /// </summary>
        public float GetTripFuel(TrainInstance train, float distanceKm, float electrifiedKm)
        {
            if (distanceKm < 0f) return -1f;
            TrainData model = train != null ? db.GetTrain(train.modelId) : null;
            if (model == null || !model.isElectric || electrifiedKm <= 0f) return GetTripFuel(train, distanceKm);
            float dieselKm = Mathf.Max(0f, distanceKm - electrifiedKm);
            if (dieselKm <= 0.001f) return 0f;
            return GetPushFuel(train) + dieselKm * db.fuelPerKm;
        }

        public bool HasFuelFor(TrainInstance train, float tripFuel) =>
            train != null && tripFuel >= 0f && train.fuel + 0.0001f >= tripFuel;

        public void Consume(TrainInstance train, float amount)
        {
            train.fuel = Mathf.Max(0f, train.fuel - amount);
        }

        /// <summary>Если запас уменьшился (отцепили топливный вагон) — лишнее топливо пропадает.</summary>
        public void ClampToCapacity(TrainInstance train)
        {
            float cap = GetCapacity(train);
            if (train.fuel > cap)
            {
                Debug.Log($"[Fuel] Запас поезда {train.instanceId} уменьшился, топливо {train.fuel:0.#} → {cap:0.#}");
                train.fuel = cap;
            }
        }

        /// <summary>Поезду не хватает топлива даже на толчок — он «без топлива».</summary>
        public void UpdateOutOfFuel(TrainInstance train)
        {
            if (train == null || train.IsMoving || train.state == TrainState.Stranded) return;

            bool empty = train.fuel + 0.0001f < GetPushFuel(train);
            if (empty && train.state != TrainState.OutOfFuel)
            {
                train.state = TrainState.OutOfFuel;
                Debug.Log($"[Fuel] Поезд {train.instanceId}: не хватает топлива даже на толчок — заправь его на станции");
            }
            else if (!empty && train.state == TrainState.OutOfFuel)
            {
                train.state = TrainState.Idle;
            }
        }

        // ================= Заправка =================

        /// <summary>Сколько топлива можно долить и за сколько монет (до полного бака).</summary>
        public void GetRefuelQuote(TrainInstance train, out float amount, out long price)
        {
            amount = train != null ? Mathf.Max(0f, GetCapacity(train) - train.fuel) : 0f;
            price = (long)Mathf.Ceil(amount * db.fuelPriceCoins);
        }

        /// <summary>Заправить стоящий поезд за монеты. Если монет не хватает на полный бак — сколько хватит.</summary>
        public bool TryRefuel(TrainInstance train)
        {
            if (train == null || train.IsMoving || string.IsNullOrEmpty(train.currentCityId))
            {
                Debug.LogWarning("[Fuel] Заправить можно только стоящий на станции поезд");
                return false;
            }

            GetRefuelQuote(train, out float amount, out long price);
            if (amount <= 0.0001f) return false;

            if (!wallet.CanAfford(Currency.Coins, price) && db.fuelPriceCoins > 0f)
            {
                amount = Mathf.Floor(wallet.Coins / db.fuelPriceCoins);
                price = (long)Mathf.Ceil(amount * db.fuelPriceCoins);
                if (amount <= 0f) return false;
            }

            if (!wallet.TrySpend(Currency.Coins, price, $"заправка {amount:0.#} топлива")) return false;

            train.fuel = Mathf.Min(GetCapacity(train), train.fuel + amount);
            UpdateOutOfFuel(train);
            Debug.Log($"[Fuel] Поезд {train.instanceId} заправлен: {train.fuel:0.#}/{GetCapacity(train):0.#}");
            return true;
        }
    }
}
