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
    /// Формат описан в начале файла _World.txt. Файлы читаются в два прохода, поэтому
    /// линия может соединять города из разных файлов (межконтинентальные линии).
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

            // ---- Проход 1: прочитать все файлы (порядок не важен) ----
            var parsed = new ParsedMap();
            string[] files = Directory.GetFiles(SourceFolder, "*.txt");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
                ParseFile(file.Replace('\\', '/'), parsed);

            // ---- Проход 2: создать/обновить ассеты ----
            EnsureFolder(DataRoot + "/Regions");
            EnsureFolder(DataRoot + "/Cities");
            EnsureFolder(DataRoot + "/Lines");

            var regionById = new Dictionary<string, RegionData>();
            var regions = new List<RegionData>();
            foreach (RegionRow r in parsed.regions)
            {
                if (regionById.ContainsKey(r.id))
                {
                    parsed.Error(r.where, $"регион «{r.id}» уже есть");
                    continue;
                }
                RegionData region = LoadOrCreate<RegionData>($"{DataRoot}/Regions/Region_{r.id}.asset");
                region.regionId = r.id;
                region.displayName = r.name;
                region.priceCoins = r.price;
                EditorUtility.SetDirty(region);
                regionById.Add(r.id, region);
                regions.Add(region);
            }

            var cityById = new Dictionary<string, CityData>();
            var cities = new List<CityData>();
            foreach (CityRow c in parsed.cities)
            {
                if (cityById.ContainsKey(c.id))
                {
                    parsed.Error(c.where, $"город «{c.id}» уже есть");
                    continue;
                }
                CityData city = LoadOrCreate<CityData>($"{DataRoot}/Cities/City_{c.id}.asset");
                city.cityId = c.id;
                city.displayName = c.name;
                city.latitude = (float)c.lat;
                city.longitude = (float)c.lon;
                city.region = c.regionId != null && regionById.TryGetValue(c.regionId, out RegionData reg) ? reg : null;
                city.mapPosition = Project(c.lat, c.lon, parsed);
                EditorUtility.SetDirty(city);
                cityById.Add(c.id, city);
                cities.Add(city);
            }

            var lines = new List<RailwayLineData>();
            var pairs = new HashSet<string>();
            foreach (LineRow l in parsed.lines)
            {
                if (!cityById.TryGetValue(l.a, out CityData a)) { parsed.Error(l.where, $"нет города «{l.a}»"); continue; }
                if (!cityById.TryGetValue(l.b, out CityData b)) { parsed.Error(l.where, $"нет города «{l.b}»"); continue; }
                if (a == b) { parsed.Error(l.where, "линия из города в него же"); continue; }

                string pairKey = string.CompareOrdinal(l.a, l.b) < 0 ? l.a + "|" + l.b : l.b + "|" + l.a;
                if (!pairs.Add(pairKey)) { parsed.Error(l.where, $"линия {l.a} — {l.b} уже есть"); continue; }

                string lineId = l.a + "_" + l.b;
                RailwayLineData line = LoadOrCreate<RailwayLineData>($"{DataRoot}/Lines/Line_{lineId}.asset");
                line.lineId = lineId;
                line.region = l.regionId != null && regionById.TryGetValue(l.regionId, out RegionData reg) ? reg : null;
                line.cityA = a;
                line.cityB = b;
                line.distance = l.distance > 0 ? (float)l.distance : GeoDistanceRounded(a, b);
                EditorUtility.SetDirty(line);
                lines.Add(line);
            }

            AssignLineStyles(lines);

            GameDatabase db = LoadOrCreate<GameDatabase>(DatabasePath);
            db.regions = regions;
            db.cities = cities;
            db.lines = lines;
            db.ClearCache();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            string summary = $"[Map Import] Регионов: {regions.Count}, городов: {cities.Count}, линий: {lines.Count}";
            if (parsed.errors > 0) Debug.LogError($"{summary}. Ошибок: {parsed.errors} (см. сообщения выше)");
            else Debug.Log(summary);
            return db;
        }

        // ================= Чтение файлов =================

        private class RegionRow { public string id, name, where; public long price; }
        private class CityRow { public string id, name, regionId, where; public double lat, lon; }
        private class LineRow { public string a, b, regionId, where; public double distance; }

        private class ParsedMap
        {
            public readonly List<RegionRow> regions = new List<RegionRow>();
            public readonly List<CityRow> cities = new List<CityRow>();
            public readonly List<LineRow> lines = new List<LineRow>();
            public double lat0, lon0 = 10, scale = 24, offsetX, offsetY;
            public bool hasMap;
            public int errors;

            public void Error(string where, string message)
            {
                errors++;
                Debug.LogError($"[Map Import] {where}: {message}");
            }
        }

        private static void ParseFile(string path, ParsedMap map)
        {
            string regionId = null;
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
                            regionId = p[1];
                            map.regions.Add(new RegionRow
                            {
                                id = p[1], name = p[2], where = where,
                                price = p.Length >= 4 && p[3].Length > 0 ? (long)Num(p[3]) : 0
                            });
                            break;

                        case "map":
                            Need(p, 6);
                            if (map.hasMap) throw new Exception("команда map уже была (она одна на всю карту)");
                            map.hasMap = true;
                            map.lat0 = Num(p[1]); map.lon0 = Num(p[2]); map.scale = Num(p[3]);
                            map.offsetX = Num(p[4]); map.offsetY = Num(p[5]);
                            break;

                        case "city":
                            Need(p, 5);
                            map.cities.Add(new CityRow
                            {
                                id = p[1], name = p[2], lat = Num(p[3]), lon = Num(p[4]), regionId = regionId, where = where
                            });
                            break;

                        case "line":
                            Need(p, 3);
                            map.lines.Add(new LineRow
                            {
                                a = p[1], b = p[2], regionId = regionId, where = where,
                                distance = p.Length >= 4 && p[3].Length > 0 ? Num(p[3]) : 0
                            });
                            break;

                        default:
                            throw new Exception($"неизвестная команда «{p[0]}»");
                    }
                }
                catch (Exception e)
                {
                    map.Error(where, e.Message);
                }
            }
        }

        /// <summary>Проекция Меркатора: широта/долгота → позиция на карте (пиксели от центра).</summary>
        private static Vector2 Project(double lat, double lon, ParsedMap map)
        {
            double x = (lon - map.lon0) * map.scale + map.offsetX;
            double y = (MercatorY(lat) - MercatorY(map.lat0)) * map.scale + map.offsetY;
            return new Vector2((float)Math.Round(x), (float)Math.Round(y));
        }

        /// <summary>Меркатор в «градусах», чтобы масштаб по X и Y был одинаковым.</summary>
        private static double MercatorY(double lat)
        {
            double clamped = Math.Max(-85.0, Math.Min(85.0, lat));
            double rad = clamped * Math.PI / 180.0;
            return Math.Log(Math.Tan(Math.PI / 4 + rad / 2)) * 180.0 / Math.PI;
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

        // ================= Цвета линий =================

        private struct LineStyle
        {
            public Color primary;
            public Color secondary;
            public LinePattern pattern;
        }

        /// <summary>
        /// Палитра стилей линий: 24 оттенка × 3 варианта яркости = 72 цвета, каждый — сплошной и пунктиром,
        /// плюс двухцветные сочетания. Итого больше 300 различимых стилей (в референсе было ~20 цветов).
        /// </summary>
        private static List<LineStyle> BuildPalette()
        {
            const int hues = LinePalette.Hues;
            var colors = new List<Color>(LinePalette.Colors);

            var palette = new List<LineStyle>();
            foreach (Color c in colors) palette.Add(new LineStyle { primary = c, secondary = c, pattern = LinePattern.Solid });
            foreach (Color c in colors) palette.Add(new LineStyle { primary = c, secondary = c, pattern = LinePattern.Dashed });
            for (int i = 0; i < colors.Count; i++)
            {
                // Второй цвет — примерно противоположный оттенок
                Color second = colors[(i + hues / 2 + 5) % colors.Count];
                palette.Add(new LineStyle { primary = colors[i], secondary = second, pattern = LinePattern.TwoTone });
                palette.Add(new LineStyle { primary = second, secondary = colors[i], pattern = LinePattern.TwoTone });
            }
            return palette;
        }

        /// <summary>
        /// Раздать линиям стили: у линий с общим городом стили всегда разные,
        /// и каждый стиль используется как можно реже (жадная раскраска графа).
        /// </summary>
        private static void AssignLineStyles(List<RailwayLineData> lines)
        {
            List<LineStyle> palette = BuildPalette();
            var used = new int[palette.Count];
            var styleOf = new Dictionary<RailwayLineData, int>();

            foreach (RailwayLineData line in lines)
            {
                var forbidden = new HashSet<int>();
                foreach (var pair in styleOf)
                {
                    RailwayLineData other = pair.Key;
                    if (other.cityA == line.cityA || other.cityA == line.cityB ||
                        other.cityB == line.cityA || other.cityB == line.cityB)
                        forbidden.Add(pair.Value);
                }

                int best = -1;
                for (int i = 0; i < palette.Count; i++)
                {
                    if (forbidden.Contains(i)) continue;
                    if (best < 0 || used[i] < used[best]) best = i;
                }
                if (best < 0) best = 0;

                used[best]++;
                styleOf[line] = best;
                line.color = palette[best].primary;
                line.secondaryColor = palette[best].secondary;
                line.pattern = palette[best].pattern;
                EditorUtility.SetDirty(line);
            }
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
