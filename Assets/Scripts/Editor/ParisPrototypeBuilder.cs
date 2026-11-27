using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrainManager.Data;
using TrainManager.Map;
using TrainManager.Trains;
using TrainManager.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TrainManager.EditorTools
{
    /// <summary>
    /// Меню Tools → Train Manager → Build Paris Prototype Scene.
    /// Одним нажатием создаёт данные (города, маршруты, поезд) и сцену ParisPrototype
    /// со всеми объектами и уже назначенными ссылками.
    /// Уже существующие файлы данных НЕ перезаписываются — твои правки в них сохранятся.
    /// </summary>
    public static class ParisPrototypeBuilder
    {
        private const string DataRoot = "Assets/Data";
        private const string ScenePath = "Assets/Scenes/ParisPrototype.unity";

        [MenuItem("Tools/Train Manager/Build Paris Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Train Manager",
                    "Сцена ParisPrototype уже существует. Пересоздать её?\n(Файлы данных в Assets/Data не трогаются.)",
                    "Пересоздать", "Отмена"))
            {
                return;
            }

            // ---------- 1. Данные ----------
            EnsureFolder(DataRoot + "/Cities");
            EnsureFolder(DataRoot + "/Routes");
            EnsureFolder(DataRoot + "/Trains");
            EnsureFolder("Assets/Scenes");

            CityData paris = LoadOrCreate<CityData>(DataRoot + "/Cities/City_Paris.asset", c =>
            {
                c.cityId = "paris"; c.displayName = "Париж"; c.mapPosition = new Vector2(0f, -150f);
            });
            CityData london = LoadOrCreate<CityData>(DataRoot + "/Cities/City_London.asset", c =>
            {
                c.cityId = "london"; c.displayName = "Лондон"; c.mapPosition = new Vector2(-500f, 250f);
            });
            CityData berlin = LoadOrCreate<CityData>(DataRoot + "/Cities/City_Berlin.asset", c =>
            {
                c.cityId = "berlin"; c.displayName = "Берлин"; c.mapPosition = new Vector2(500f, 250f);
            });

            RouteData parisLondon = CreateRoute("Route_Paris_London", "paris_london", paris, london, 300f);
            RouteData parisBerlin = CreateRoute("Route_Paris_Berlin", "paris_berlin", paris, berlin, 600f);
            RouteData londonParis = CreateRoute("Route_London_Paris", "london_paris", london, paris, 300f);
            RouteData berlinParis = CreateRoute("Route_Berlin_Paris", "berlin_paris", berlin, paris, 600f);

            TrainData standard = LoadOrCreate<TrainData>(DataRoot + "/Trains/Train_Standard.asset", t =>
            {
                t.trainId = "train_standard"; t.displayName = "Стандарт"; t.rarity = TrainRarity.Common;
                t.baseSpeed = 50f; t.maxWagons = 8; t.maxFuel = 25; t.maxFuelWagons = 1; t.partsRequiredForAssembly = 3;
            });

            AssetDatabase.SaveAssets();

            // ---------- 2. Сцена ----------
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            CreateEventSystem();

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform canvasT = canvasGo.transform;

            Image background = CreateImage("Background", canvasT, new Color(0.18f, 0.24f, 0.2f));
            Stretch(background.rectTransform);

            // Карта
            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(canvasT, false);
            Stretch((RectTransform)mapGo.transform);
            MapView map = mapGo.AddComponent<MapView>();

            CreateLine("Line_Paris_London", mapGo.transform, paris.mapPosition, london.mapPosition);
            CreateLine("Line_Paris_Berlin", mapGo.transform, paris.mapPosition, berlin.mapPosition);

            CreateStation(paris, mapGo.transform);
            CreateStation(london, mapGo.transform);
            CreateStation(berlin, mapGo.transform);

            // Логика поезда — отдельный объект (не UI)
            var trainLogicGo = new GameObject("Train_Standard (Logic)");
            TrainController train = trainLogicGo.AddComponent<TrainController>();
            train.trainData = standard;
            train.startCity = paris;

            // Картинка поезда на карте (последней — чтобы рисовалась поверх станций)
            Image trainImage = CreateImage("Train_Standard (View)", mapGo.transform, new Color(0.85f, 0.2f, 0.2f));
            trainImage.rectTransform.sizeDelta = new Vector2(70f, 36f);
            trainImage.rectTransform.anchoredPosition = paris.mapPosition;
            trainImage.raycastTarget = false;
            TrainView trainView = trainImage.gameObject.AddComponent<TrainView>();
            trainView.train = train;
            trainView.map = map;

            // Статус
            TMP_Text status = CreateText("TrainStatus", canvasT, "", 32, TextAlignmentOptions.TopLeft);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(40f, -30f);
            statusRect.sizeDelta = new Vector2(900f, 160f);
            TrainStatusText statusText = status.gameObject.AddComponent<TrainStatusText>();
            statusText.train = train;
            statusText.text = status;

            // Кнопки маршрутов
            var panelGo = new GameObject("RouteButtons", typeof(RectTransform));
            panelGo.transform.SetParent(canvasT, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(1500f, 130f);
            var layout = panelGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateRouteButton(panelGo.transform, train, parisLondon);
            CreateRouteButton(panelGo.transform, train, parisBerlin);
            CreateRouteButton(panelGo.transform, train, londonParis);
            CreateRouteButton(panelGo.transform, train, berlinParis);

            // ---------- 3. Сохранение ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = trainLogicGo;
            Debug.Log("[Train Manager] Сцена ParisPrototype создана: " + ScenePath + ". Нажми Play.");
        }

        // ================= Вспомогательное =================

        private static RouteData CreateRoute(string file, string id, CityData from, CityData to, float distance)
        {
            return LoadOrCreate<RouteData>(DataRoot + "/Routes/" + file + ".asset", r =>
            {
                r.routeId = id; r.from = from; r.to = to; r.distance = distance;
            });
        }

        private static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // В проекте включён новый Input System, поэтому нужен этот модуль, а не StandaloneInputModule.
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateStation(CityData city, Transform map)
        {
            Image dot = CreateImage("Station_" + city.cityId, map, new Color(0.25f, 0.55f, 0.95f));
            dot.rectTransform.sizeDelta = new Vector2(60f, 60f);
            dot.rectTransform.anchoredPosition = city.mapPosition;
            dot.raycastTarget = false;

            TMP_Text label = CreateText("Label", dot.transform, city.displayName, 36, TextAlignmentOptions.Center);
            label.rectTransform.anchoredPosition = new Vector2(0f, -65f);
            label.rectTransform.sizeDelta = new Vector2(300f, 50f);

            StationView view = dot.gameObject.AddComponent<StationView>();
            view.city = city;
            view.label = label;
        }

        private static void CreateLine(string name, Transform map, Vector2 a, Vector2 b)
        {
            Image line = CreateImage(name, map, new Color(0.9f, 0.75f, 0.3f));
            line.raycastTarget = false;
            RectTransform rect = line.rectTransform;
            Vector2 delta = b - a;
            rect.sizeDelta = new Vector2(delta.magnitude, 8f);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void CreateRouteButton(Transform parent, TrainController train, RouteData route)
        {
            Image image = CreateImage("Button_" + route.routeId, parent, new Color(0.95f, 0.95f, 0.95f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText("Text", image.transform, route.DisplayName, 30, TextAlignmentOptions.Center);
            label.color = new Color(0.1f, 0.1f, 0.1f);
            Stretch(label.rectTransform);

            RouteButton routeButton = image.gameObject.AddComponent<RouteButton>();
            routeButton.train = train;
            routeButton.route = route;
            routeButton.label = label;
        }
    }
}
