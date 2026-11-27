using System;

namespace TrainManager.Data
{
    /// <summary>География: расстояние между городами по их реальным координатам.</summary>
    public static class Geo
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>Расстояние по прямой (по поверхности Земли), км.</summary>
        public static float DistanceKm(CityData a, CityData b)
        {
            if (a == null || b == null) return 0f;

            double la1 = a.latitude * Math.PI / 180.0, la2 = b.latitude * Math.PI / 180.0;
            double dLat = la2 - la1;
            double dLon = (b.longitude - a.longitude) * Math.PI / 180.0;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(la1) * Math.Cos(la2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return (float)(2 * EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1.0, h))));
        }
    }
}
