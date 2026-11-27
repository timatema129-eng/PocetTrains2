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
        [Tooltip("Сколько вагонов поезд может тянуть сам (без дополнительных двигателей)")]
        [Min(1)] public int maxWagons = 8;
        [Min(1)] public int maxFuel = 25;

        [Header("Engines (числа уточняются)")]
        [Tooltip("Основных двигателей")]
        [Min(1)] public int mainEngines = 1;
        [Tooltip("Сколько вагонов-двигателей (дополнительных) можно прицепить. Каждый занимает 1 место в составе")]
        [Min(0)] public int maxExtraEngines = 1;
        [Tooltip("На сколько вагонов увеличивает лимит один дополнительный двигатель (своё у каждой модели)")]
        [Min(0)] public int wagonsPerExtraEngine = 0;

        [Header("Fuel wagons (числа уточняются)")]
        [Tooltip("Сколько топливных вагонов можно прицепить. Каждый занимает 1 место в составе")]
        [Min(0)] public int maxFuelWagons = 1;
        [Tooltip("На сколько один топливный вагон увеличивает запас топлива")]
        [Min(0)] public int fuelPerFuelWagon = 0;

        [Header("Wear (черновик)")]
        [Tooltip("На сколько % падает состояние за 1 км пути")]
        [Min(0f)] public float wearPerKm = 0.02f;
        [Tooltip("Множитель цен техобслуживания, ремонта и буксировки для этой модели")]
        [Min(0f)] public float serviceCostMultiplier = 1f;

        [Header("Assembly")]
        [Tooltip("Сколько одинаковых деталей нужно для сборки (обычный 3 … крутой до 15)")]
        [Min(1)] public int partsRequiredForAssembly = 3;

        [Header("Visual")]
        public Sprite sprite;
    }
}
