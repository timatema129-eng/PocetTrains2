using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Регион / материк (например, Европа). Открытие региона даёт право покупать линии внутри него.
    /// Открыт ли регион у игрока — хранится в GameState, а не здесь.
    /// </summary>
    [CreateAssetMenu(fileName = "Region_", menuName = "Train Manager/Region Data")]
    public class RegionData : ScriptableObject
    {
        [Header("ID")]
        [Tooltip("Уникальный ID региона, латиницей: europe")]
        public string regionId;

        [Header("Info")]
        public string displayName;
    }
}
