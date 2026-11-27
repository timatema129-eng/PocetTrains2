using UnityEngine;

namespace TrainManager.Data
{
    public enum TrainRarity
    {
        Common,
        Rare,
        Epic
    }

    /// <summary>
    /// Шаблон модели поезда (характеристики). Не экземпляр поезда игрока:
    /// текущий город, топливо и маршрут хранятся в TrainInstance (GameState).
    /// Специально без [Range], чтобы баланс не был зашит в код.
    /// </summary>
    [CreateAssetMenu(fileName = "Train_", menuName = "Train Manager/Train Data")]
    public class TrainData : ScriptableObject
    {
        [Header("ID")]
        public string trainId;

        [Header("Info")]
        public string displayName;
        public TrainRarity rarity;

        [Header("Stats")]
        [Min(0.1f)] public float baseSpeed = 50f;
        [Min(1)] public int maxWagons = 8;
        [Min(1)] public int maxFuel = 25;
        [Min(0)] public int maxFuelWagons = 1;

        [Header("Assembly")]
        [Min(1)] public int partsRequiredForAssembly = 3;

        [Header("Visual")]
        public Sprite sprite;
    }
}
