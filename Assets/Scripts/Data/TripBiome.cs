using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>Что стоит вдоль дороги в виде сбоку (пока простые фигуры-заглушки, потом арт).</summary>
    public enum BiomeDecor
    {
        Trees,
        Cactus,
        Rocks,
        Palms
    }

    /// <summary>
    /// Локация для вида поездки сбоку: цвета неба и земли, что вдоль дороги, идёт ли снег.
    /// Выбирается по городу, к которому едет поезд: сначала cityIds, потом regionIds.
    /// Только картинка, на логику не влияет.
    /// </summary>
    [Serializable]
    public class TripBiome
    {
        public string biomeId;
        public string displayName;
        public Color skyColor = new Color(0.55f, 0.75f, 0.95f);
        public Color hillsColor = new Color(0.35f, 0.55f, 0.35f);
        public Color groundColor = new Color(0.3f, 0.5f, 0.25f);
        public Color decorColor = new Color(0.15f, 0.4f, 0.15f);
        public BiomeDecor decor = BiomeDecor.Trees;
        public bool snow;
        [Tooltip("Города, где эта локация (важнее регионов)")]
        public List<string> cityIds = new List<string>();
        [Tooltip("Регионы, где эта локация по умолчанию")]
        public List<string> regionIds = new List<string>();
    }
}
