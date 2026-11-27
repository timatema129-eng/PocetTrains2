using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.EditorTools
{
    /// <summary>
    /// Импорт карты из текстовых файлов Assets/Data/MapSource/*.txt в конфиги
    /// (RegionData, CityData, RailwayLineData) и в GameDatabase.
    /// Текстовый файл — главный источник: при каждом импорте ассеты обновляются по нему.
    /// Формат описан в начале файла Europe.txt.
    /// Дистанция линии, если не задана вручную, = расстояние по прямой между городами в км,
    /// округлённое до 10.
    /// </summary>
    public static class MapDataImporter
    {
        public const string SourceFolder = "Assets/Data/MapSource";
        public const string DataRoot = "Assets/Data";
        public const string DatabasePath = DataRoot + "/GameDatabase.asset";

        private const double EarthRadiusKm = 6371.0;

        [MenuItem("Tools/Train Manager/Import Map Data")]
        public static void ImportMenu()
        {
            GameDatabase db = Import();
            if (db != null) Selection.activeObject = db;
        }

        /// <summary>Импортировать все файлы карты. Возвращает GameDatabase (или null при ошибке).</summary>
        public static GameDatabase Import()
        {
            if (!Directory.Exists(SourceFolder))
            {
                Debug.LogError($"[Map Import] Нет папки {SourceFolder}");
                return null;
            }

            EnsureFolder(DataRoot + "/Regions");
            EnsureFolder(DataRoot + "/Cities");
            EnsureFolder(DataRoot + "/Lines");

            GameDatabase db = LoadOrCreate<GameDatabase>(DatabasePath);
            var regions = new List<RegionData>();
            var cities = new List<CityData>();
            var lines = new List<RailwayLineData>();
            int errors = 0;

            string[] files = Directory.GetFiles(SourceFolder, "*.txt");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
                errors += ImportFile(file.Replace('\\', '/'), regions, cities, lines);

            db.regions = regions;
            db.cities = cities;
            db.lines = lines;
            db.ClearCache();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            string summary = $"[Map Import] Регионов: {regions.Count}, городов: {cities.Count}, линий: {lines.Count}";
            if (errors > 0) Debug.LogError($"{summary}. Ошибок: {errors} (см. сообщения выше)");
            else Debug.Log(summary);
            return db;
        }

        private static int ImportFile(string path, List<RegionData> regions, List<CityData> cities,
            List<RailwayLineData> lines)
        {
            int errors = 0;
            RegionData region = null;
            double lat0 = 0, lon0 = 0, scale = 38, offsetX = 0, offsetY = 0;
            var cityById = new Dictionary<string, CityData>();
            foreach (CityData c in cities) cityById[c.cityId] = c;

            string[] rows = File.ReadAllLines(path);
            for (int n = 0; n < rows.Length; n++)
            {
                string row = rows[n];
                int hash = row.IndexOf('#');
                if (hash >= 0) row = row.Substring(0, hash);
                row = row.Trim();
                if (row.Length == 0) continue;

                string[] p = row.Split(';');
                for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();
                string where = $"{Path.GetFileName(path)}, строка {n + 1}";

                try
                {
                    switch (p[0])
                    {
                        case "region":
                            Need(p, 3);
                            region = LoadOrCreate<RegionData>($"{DataRoot}/Regions/Region_{p[1]}.asset");
                            region.regionId = p[1];
                            region.displayName = p[2];
                            EditorUtility.SetDirty(region);
                            if (!regions.Contains(region)) regions.Add(region);
                            break;

                        case "map":
                            Need(p, 6);
                            lat0 = Num(p[1]); lon0 = Num(p[2]); scale = Num(p[3]);
                            offsetX = Num(p[4]); offsetY = Num(p[5]);
                            break;

                        case "city":
                        {
                            Need(p, 5);
                            string id = p[1];
                            if (cityById.ContainsKey(id)) throw new Exception($"город «{id}» уже есть");

                            CityData city = LoadOrCreate<CityData>($"{DataRoot}/Cities/City_{id}.asset");
                            city.cityId = id;
                            city.displayName = p[2];
                            city.latitude = (float)Num(p[3]);
                            city.longitude = (float)Num(p[4]);
                            city.region = region;
                            // Равнопромежуточная проекция с поправкой на широту — для Европы выглядит нормально
                            double k = Math.Cos(lat0 * Math.PI / 180.0);
                            city.mapPosition = new Vector2(
                                (float)Math.Round((city.longitude - lon0) * k * scale + offsetX),
                                (float)Math.Round((city.latitude - lat0) * scale + offsetY));
                            EditorUtility.SetDirty(city);

                            cityById.Add(id, city);
                            cities.Add(city);
                            break;
                        }

                        case "line":
                        {
                            Need(p, 3);
                            if (!cityById.TryGetValue(p[1], out CityData a)) throw new Exception($"нет города «{p[1]}»");
                            if (!cityById.TryGetValue(p[2], out CityData b)) throw new Exception($"нет города «{p[2]}»");
                            if (a == b) throw new Exception("линия из города в него же");

                            string lineId = p[1] + "_" + p[2];
                            foreach (RailwayLineData existing in lines)
                            {
                                if (existing.Connects(a, b)) throw new Exception($"линия {p[1]} — {p[2]} уже есть");
                            }

                            RailwayLineData line = LoadOrCreate<RailwayLineData>($"{DataRoot}/Lines/Line_{lineId}.asset");
                            line.lineId = lineId;
                            line.region = region;
                            line.cityA = a;
                            line.cityB = b;
                            line.distance = p.Length >= 4 && p[3].Length > 0
                                ? (float)Num(p[3])
                                : GeoDistanceRounded(a, b);
                            line.color = AutoColor(lines.Count);
                            EditorUtility.SetDirty(line);
                            lines.Add(line);
                            break;
                        }

                        default:
                            throw new Exception($"неизвестная команда «{p[0]}»");
                    }
                }
                catch (Exception e)
                {
                    errors++;
                    Debug.LogError($"[Map Import] {where}: {e.Message}");
                }
            }
            return errors;
        }

        /// <summary>Расстояние по прямой (по поверхности Земли), км, округлённое до 10. Минимум 10.</summary>
        public static float GeoDistanceRounded(CityData a, CityData b)
        {
            double la1 = a.latitude * Math.PI / 180.0, la2 = b.latitude * Math.PI / 180.0;
            double dLat = la2 - la1;
            double dLon = (b.longitude - a.longitude) * Math.PI / 180.0;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(la1) * Math.Cos(la2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double km = 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(h));
            return (float)Math.Max(10.0, Math.Round(km / 10.0) * 10.0);
        }

        /// <summary>Разные хорошо различимые цвета линий (золотое сечение по оттенку).</summary>
        private static Color AutoColor(int index)
        {
            float hue = (index * 0.618034f) % 1f;
            return Color.HSVToRGB(hue, 0.6f, 0.95f);
        }

        private static void Need(string[] p, int count)
        {
            if (p.Length < count) throw new Exception($"нужно минимум {count} полей через ;");
        }

        private static double Num(string s)
        {
            if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new Exception($"«{s}» — не число");
            return v;
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
