using System;
using System.Collections.Generic;

namespace TrainManager.Data
{
    /// <summary>Класс крупного города для метро: сколько веток и станций можно открыть, множитель дохода.</summary>
    [Serializable]
    public class MetroTier
    {
        public string tierId;
        public string displayName;
        [UnityEngine.Min(1)] public int maxBranches = 4;
        [UnityEngine.Min(1)] public int maxStationsPerBranch = 7;
        [UnityEngine.Min(0f)] public float incomeMultiplier = 1f;
    }

    /// <summary>Город, где можно построить метро (только крупные города).</summary>
    [Serializable]
    public class MetroCity
    {
        public string cityId;
        public string tierId;

        public MetroCity() { }
        public MetroCity(string cityId, string tierId)
        {
            this.cityId = cityId;
            this.tierId = tierId;
        }
    }
}
