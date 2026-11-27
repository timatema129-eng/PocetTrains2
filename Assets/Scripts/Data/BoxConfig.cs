using System;

namespace TrainManager.Data
{
    /// <summary>
    /// Бокс с деталью поезда. Открывается только за доллары; выпадает 1 деталь
    /// случайной модели поезда той же редкости (список моделей — GameDatabase.trains).
    /// </summary>
    [Serializable]
    public class BoxConfig
    {
        public string boxId;
        public string displayName;
        public TrainRarity rarity;
        [UnityEngine.Tooltip("Цена в долларах (черновик)")]
        public int priceDollars;
    }
}
