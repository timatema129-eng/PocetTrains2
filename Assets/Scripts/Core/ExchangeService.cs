using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Курс и обмен долларов на монеты (только $ → монеты, обратно нельзя).
    /// Модель (решение 36): время делится на циклы по 3 реальных дня, отсчёт от Unix-эпохи — курс одинаковый
    /// у всех игроков и совпадает после перезапуска. В начале цикла по его номеру «случайно» выбирается пик
    /// в диапазоне [min..max]. Первую половину цикла курс растёт от минимума к пику, вторую — падает обратно.
    /// Меняется шагами (раз в exchangeStepMinutes), а не каждую секунду.
    /// </summary>
    public class ExchangeService
    {
        private readonly GameDatabase db;
        private readonly IGameClock clock;
        private readonly WalletService wallet;

        public ExchangeService(GameDatabase db, IGameClock clock, WalletService wallet)
        {
            this.db = db;
            this.clock = clock;
            this.wallet = wallet;
        }

        private long CycleMs => (long)Mathf.Max(1, db.exchangeCycleDays) * 24L * 3600L * 1000L;
        private long StepMs => (long)Mathf.Max(1, db.exchangeStepMinutes) * 60L * 1000L;

        /// <summary>Сколько монет дают за 1 $ сейчас.</summary>
        public int CurrentRate => GetRate(clock.NowUnixMs);

        /// <summary>Курс в момент timeMs (Unix, мс).</summary>
        public int GetRate(long timeMs)
        {
            long cycle = Mathf.Max(1, (int)(CycleMs / StepMs)) * StepMs; // целое число шагов в цикле
            long index = FloorDiv(timeMs, cycle);
            long inCycle = timeMs - index * cycle;
            long stepStart = inCycle - inCycle % StepMs;

            float phase = (float)((double)stepStart / cycle);       // 0..1 внутри цикла
            float tri = phase < 0.5f ? phase * 2f : (1f - phase) * 2f; // 0 → 1 → 0
            int peak = GetPeak(index);
            return Mathf.RoundToInt(db.exchangeMinRate + (peak - db.exchangeMinRate) * tri);
        }

        /// <summary>Пик текущего цикла (игроку не показывается — только направление).</summary>
        public int GetPeak(long cycleIndex)
        {
            int min = db.exchangeMinRate;
            int max = Mathf.Max(min, db.exchangeMaxRate);
            double r = Hash01(cycleIndex);
            return min + (int)System.Math.Round(r * (max - min));
        }

        /// <summary>Растёт ли курс сейчас (первая половина цикла).</summary>
        public bool IsRising
        {
            get
            {
                long now = clock.NowUnixMs;
                return GetRate(now + StepMs) >= GetRate(now);
            }
        }

        /// <summary>Секунд до следующего изменения курса.</summary>
        public float SecondsToNextStep
        {
            get
            {
                long now = clock.NowUnixMs;
                long rest = StepMs - (now % StepMs + StepMs) % StepMs;
                return rest / 1000f;
            }
        }

        /// <summary>Какой сейчас день цикла (1..exchangeCycleDays).</summary>
        public int CycleDay
        {
            get
            {
                long now = clock.NowUnixMs;
                long inCycle = now - FloorDiv(now, CycleMs) * CycleMs;
                return (int)(inCycle / (24L * 3600L * 1000L)) + 1;
            }
        }

        public bool CanExchange(long dollars, out string reason)
        {
            if (dollars <= 0)
            {
                reason = "нечего менять";
                return false;
            }
            if (!wallet.CanAfford(Currency.Dollars, dollars))
            {
                reason = $"нужно {dollars} $";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Обменять dollars $ на монеты по текущему курсу.</summary>
        public bool TryExchange(long dollars)
        {
            if (!CanExchange(dollars, out string reason))
            {
                Debug.LogWarning($"[Exchange] Обмен нельзя: {reason}");
                return false;
            }
            int rate = CurrentRate;
            if (!wallet.TrySpend(Currency.Dollars, dollars, $"обмен по курсу {rate}")) return false;
            wallet.Add(Currency.Coins, dollars * rate, $"обмен {dollars} $ по курсу {rate}");
            return true;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        /// <summary>Детерминированное «случайное» число 0..1 по номеру цикла (SplitMix64) — одинаково везде.</summary>
        private static double Hash01(long x)
        {
            ulong z = unchecked((ulong)x + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }
    }
}
