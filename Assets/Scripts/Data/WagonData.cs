using UnityEngine;

namespace TrainManager.Data
{
    public enum WagonKind
    {
        /// <summary>Грузовой: везёт груз в город назначения, приносит доход.</summary>
        Cargo,
        /// <summary>Топливный: увеличивает запас топлива (Этап 5).</summary>
        Fuel,
        /// <summary>Вагон-двигатель: увеличивает лимит вагонов (Этап 5). Занимает 1 место.</summary>
        Engine
    }

    /// <summary>
    /// Тип вагона (шаблон). Конкретный вагон игрока — WagonInstance в GameState.
    /// Цена и доход считаются формулой из GameDatabase (раздел Economy) и умножаются на множители типа.
    /// </summary>
    [CreateAssetMenu(fileName = "Wagon_", menuName = "Train Manager/Wagon Data")]
    public class WagonData : ScriptableObject
    {
        [Header("ID")]
        public string wagonTypeId;

        [Header("Info")]
        public string displayName;
        public WagonKind kind = WagonKind.Cargo;

        [Header("Economy")]
        [Min(0f)] public float priceMultiplier = 1f;
        [Min(0f)] public float incomeMultiplier = 1f;

        [Header("Visual")]
        public Color color = new Color(0.55f, 0.4f, 0.3f);
    }
}
