using System.Collections.Generic;
using UnityEngine;

namespace TrainManager.Data
{
    /// <summary>
    /// Палитра цветов линий: 24 оттенка × 3 варианта яркости = 72 цвета.
    /// Ей пользуются импорт карты (раздача стилей по умолчанию) и игрок (смена цвета линии).
    /// </summary>
    public static class LinePalette
    {
        public const int Hues = 24;

        private static List<Color> colors;

        public static IReadOnlyList<Color> Colors
        {
            get
            {
                if (colors == null) colors = Build();
                return colors;
            }
        }

        private static List<Color> Build()
        {
            var shades = new[] { new Vector2(0.75f, 0.95f), new Vector2(0.45f, 0.85f), new Vector2(0.9f, 0.65f) };
            var result = new List<Color>();
            foreach (Vector2 sv in shades)
            {
                for (int i = 0; i < Hues; i++)
                {
                    // Порядок оттенков через золотое сечение — соседние в палитре цвета сильно отличаются
                    float hue = (i * 0.618034f) % 1f;
                    result.Add(Color.HSVToRGB(hue, sv.x, sv.y));
                }
            }
            return result;
        }
    }
}
