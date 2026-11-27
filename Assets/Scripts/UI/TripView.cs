using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainManager.Core;
using TrainManager.Data;
using TrainManager.Trains;

namespace TrainManager.UI
{
    /// <summary>
    /// Вид поездки сбоку (Этап 12): поезд в 2D, дым из трубы, прицепленные вагоны,
    /// бесконечно бегущая локация (небо, холмы, земля, деревья/кактусы/камни/пальмы, снег),
    /// которая зависит от города, куда едет поезд (зима в России, лес, пустыня…).
    /// Всё рисуется простыми фигурами-заглушками — потом заменим артом. На логику не влияет.
    /// </summary>
    public class TripView : MonoBehaviour
    {
        public GameRoot root;
        public TrainSelection selection;
        public Button openButton;
        public Button closeButton;
        [Tooltip("Область, где рисуется сцена (с RectMask2D)")]
        public RectTransform stage;
        public TMP_Text caption;

        [Tooltip("Пикселей в секунду бега земли на скорость поезда 50")]
        public float scrollPerSpeed50 = 220f;

        private Image sky, hills, ground, rail;
        private readonly List<RectTransform> decor = new List<RectTransform>();
        private readonly List<Image> decorImages = new List<Image>();
        private readonly List<RectTransform> hillBumps = new List<RectTransform>();
        private readonly List<Image> smoke = new List<Image>();
        private readonly List<Vector2> smokeVel = new List<Vector2>();
        private readonly List<float> smokeAge = new List<float>();
        private readonly List<RectTransform> snow = new List<RectTransform>();
        private readonly List<Image> trainParts = new List<Image>();
        private RectTransform trainRoot;
        private RectTransform chimney;

        private TripBiome biome;
        private Color curSky, curHills, curGround, curDecor;
        private string trainSignature;
        private float smokeTimer;
        private float bob;

        private const float GroundHeight = 110f;
        private const float RailY = 110f;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            BuildScene();
            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            trainSignature = null;
            biome = null;
        }

        private TrainInstance Train => selection != null ? selection.Selected : null;

        private void Update()
        {
            if (root == null || !root.IsReady) return;

            TrainInstance train = Train;
            if (train == null || stage == null)
            {
                if (caption != null) caption.text = "Выбери поезд в пути";
                return;
            }

            // Куда едет — от этого локация
            root.Trains.GetPosition(train, out string fromId, out string toId, out float legT);
            CityData next = root.Database.GetCity(toId);
            TripBiome target = root.Database.GetBiome(next);
            if (target != biome)
            {
                bool first = biome == null;
                biome = target;
                RebuildDecorShapes();
                if (first) SnapColors();
            }
            BlendColors(Time.unscaledDeltaTime * 1.5f);

            bool moving = train.IsMoving;
            TrainData model = root.Trains.GetModel(train);
            float speed = moving && model != null ? model.baseSpeed : 0f;
            float scroll = scrollPerSpeed50 * Mathf.Clamp(speed / 50f, 0.5f, 3f) * (moving ? 1f : 0f);
            float dt = Time.unscaledDeltaTime;
            float width = stage.rect.width;

            Scroll(decor, scroll * dt, width);
            Scroll(hillBumps, scroll * 0.3f * dt, width);
            UpdateSnow(dt, width);
            UpdateTrain(train, dt, moving);
            UpdateSmoke(dt, moving, scroll);

            if (caption != null)
            {
                string state = train.state == TrainState.Stranded ? "<color=#ff6060>сломался на линии</color>"
                    : moving ? $"→ {(next != null ? next.displayName : "?")}, участок {legT * 100f:0}%, вся поездка {root.Trains.GetTripProgress(train) * 100f:0}%"
                    : "стоит на станции";
                caption.text = $"«{root.Trains.GetDisplayName(train)}» {state} · {biome?.displayName}";
            }
        }

        // ================= Сцена =================

        private void BuildScene()
        {
            if (stage == null) return;
            sky = MakeImage("Sky", stage, Color.cyan);
            Stretch(sky.rectTransform, 0f, 1f);
            hills = MakeImage("HillsBand", stage, Color.green);
            Band(hills.rectTransform, GroundHeight, 40f);
            for (int i = 0; i < 6; i++)
            {
                Image bump = MakeImage("Hill", stage, Color.green);
                RectTransform r = bump.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
                r.sizeDelta = new Vector2(Random.Range(180f, 320f), Random.Range(40f, 110f));
                r.anchoredPosition = new Vector2(i * 260f, GroundHeight + 20f);
                hillBumps.Add(r);
            }
            ground = MakeImage("Ground", stage, Color.green);
            Band(ground.rectTransform, 0f, GroundHeight);
            rail = MakeImage("Rail", stage, new Color(0.3f, 0.25f, 0.2f));
            Band(rail.rectTransform, RailY - 6f, 6f);

            for (int i = 0; i < 10; i++)
            {
                Image d = MakeImage("Decor", stage, Color.green);
                RectTransform r = d.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
                r.anchoredPosition = new Vector2(i * 140f + Random.Range(0f, 60f), Random.Range(20f, 80f));
                decor.Add(r);
                decorImages.Add(d);
            }

            var trainGo = new GameObject("Train", typeof(RectTransform));
            trainGo.transform.SetParent(stage, false);
            trainRoot = (RectTransform)trainGo.transform;
            trainRoot.anchorMin = trainRoot.anchorMax = trainRoot.pivot = new Vector2(0f, 0f);
            trainRoot.anchoredPosition = new Vector2(0f, RailY);

            for (int i = 0; i < 40; i++)
            {
                Image s = MakeImage("Snow", stage, Color.white);
                RectTransform r = s.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
                r.sizeDelta = new Vector2(5f, 5f);
                r.anchoredPosition = new Vector2(Random.Range(0f, 1600f), Random.Range(0f, 500f));
                snow.Add(r);
            }
        }

        private void RebuildDecorShapes()
        {
            foreach (RectTransform r in decor)
            {
                switch (biome.decor)
                {
                    case BiomeDecor.Cactus: r.sizeDelta = new Vector2(Random.Range(14f, 22f), Random.Range(40f, 80f)); break;
                    case BiomeDecor.Rocks: r.sizeDelta = new Vector2(Random.Range(40f, 90f), Random.Range(20f, 45f)); break;
                    case BiomeDecor.Palms: r.sizeDelta = new Vector2(Random.Range(10f, 14f), Random.Range(90f, 140f)); break;
                    default: r.sizeDelta = new Vector2(Random.Range(26f, 40f), Random.Range(60f, 120f)); break;
                }
            }
            foreach (RectTransform s in snow) s.gameObject.SetActive(biome.snow);
        }

        private void SnapColors()
        {
            curSky = biome.skyColor;
            curHills = biome.hillsColor;
            curGround = biome.groundColor;
            curDecor = biome.decorColor;
            ApplyColors();
        }

        private void BlendColors(float t)
        {
            if (biome == null) return;
            curSky = Color.Lerp(curSky, biome.skyColor, t);
            curHills = Color.Lerp(curHills, biome.hillsColor, t);
            curGround = Color.Lerp(curGround, biome.groundColor, t);
            curDecor = Color.Lerp(curDecor, biome.decorColor, t);
            ApplyColors();
        }

        private void ApplyColors()
        {
            sky.color = curSky;
            hills.color = curHills;
            foreach (RectTransform b in hillBumps) b.GetComponent<Image>().color = curHills;
            ground.color = curGround;
            foreach (Image d in decorImages) d.color = curDecor;
        }

        private static void Scroll(List<RectTransform> items, float dx, float width)
        {
            foreach (RectTransform r in items)
            {
                Vector2 p = r.anchoredPosition;
                p.x -= dx;
                if (p.x + r.sizeDelta.x < 0f) p.x += width + r.sizeDelta.x + Random.Range(0f, 120f);
                r.anchoredPosition = p;
            }
        }

        private void UpdateSnow(float dt, float width)
        {
            if (biome == null || !biome.snow) return;
            float height = stage.rect.height;
            foreach (RectTransform r in snow)
            {
                Vector2 p = r.anchoredPosition + new Vector2(-40f, -60f) * dt;
                if (p.y < 0f) p = new Vector2(Random.Range(0f, width + 100f), height);
                if (p.x < 0f) p.x += width;
                r.anchoredPosition = p;
            }
        }

        // ================= Поезд =================

        private void UpdateTrain(TrainInstance train, float dt, bool moving)
        {
            string sig = train.instanceId + ":" + string.Join(",", train.wagonIds);
            if (sig != trainSignature)
            {
                trainSignature = sig;
                BuildTrain(train);
            }
            bob += dt * (moving ? 12f : 0f);
            float y = RailY + (moving ? Mathf.Abs(Mathf.Sin(bob)) * 2f : 0f);
            trainRoot.anchoredPosition = new Vector2(stage.rect.width * 0.62f, y);
        }

        private void BuildTrain(TrainInstance train)
        {
            foreach (Image p in trainParts) Destroy(p.gameObject);
            trainParts.Clear();

            // Паровоз справа, вагоны тянутся влево
            Part("Engine", new Color(0.75f, 0.15f, 0.15f), new Vector2(0f, 8f), new Vector2(120f, 56f));
            Part("Cabin", new Color(0.55f, 0.1f, 0.1f), new Vector2(-10f, 64f), new Vector2(46f, 34f));
            Image chim = Part("Chimney", new Color(0.2f, 0.2f, 0.22f), new Vector2(84f, 64f), new Vector2(16f, 30f));
            chimney = chim.rectTransform;
            AddWheels(0f, 120f);

            float x = -10f;
            foreach (WagonInstance w in root.Wagons.GetTrainWagons(train))
            {
                WagonData type = root.Wagons.GetWagonType(w);
                Color c = type != null ? type.color : new Color(0.55f, 0.4f, 0.3f);
                x -= 96f;
                Part("Wagon", c, new Vector2(x, 10f), new Vector2(88f, 46f));
                Part("Coupler", new Color(0.2f, 0.2f, 0.2f), new Vector2(x + 88f, 22f), new Vector2(8f, 4f));
                AddWheels(x, 88f);
            }
        }

        private void AddWheels(float x, float width)
        {
            Part("Wheel", new Color(0.1f, 0.1f, 0.1f), new Vector2(x + 10f, 0f), new Vector2(18f, 14f));
            Part("Wheel", new Color(0.1f, 0.1f, 0.1f), new Vector2(x + width - 28f, 0f), new Vector2(18f, 14f));
        }

        private Image Part(string name, Color color, Vector2 pos, Vector2 size)
        {
            Image img = MakeImage(name, trainRoot, color);
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            trainParts.Add(img);
            return img;
        }

        // ================= Дым =================

        private void UpdateSmoke(float dt, bool moving, float scroll)
        {
            smokeTimer -= dt;
            if (moving && chimney != null && smokeTimer <= 0f)
            {
                smokeTimer = 0.12f;
                SpawnPuff(scroll);
            }

            for (int i = smoke.Count - 1; i >= 0; i--)
            {
                smokeAge[i] += dt;
                float age = smokeAge[i];
                RectTransform r = smoke[i].rectTransform;
                r.anchoredPosition += smokeVel[i] * dt;
                r.sizeDelta = Vector2.one * (14f + age * 40f);
                Color c = smoke[i].color;
                c.a = Mathf.Clamp01(0.8f - age * 0.5f);
                smoke[i].color = c;
                if (age > 1.6f)
                {
                    Destroy(smoke[i].gameObject);
                    smoke.RemoveAt(i);
                    smokeVel.RemoveAt(i);
                    smokeAge.RemoveAt(i);
                }
            }
        }

        private void SpawnPuff(float scroll)
        {
            Image puff = MakeImage("Smoke", stage, new Color(0.85f, 0.85f, 0.85f, 0.8f));
            RectTransform r = puff.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.anchoredPosition = trainRoot.anchoredPosition + chimney.anchoredPosition + new Vector2(8f, 32f);
            smoke.Add(puff);
            smokeVel.Add(new Vector2(-scroll * 0.6f + Random.Range(-10f, 10f), Random.Range(50f, 80f)));
            smokeAge.Add(0f);
        }

        // ================= Помощники =================

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform r, float yMin, float yMax)
        {
            r.anchorMin = new Vector2(0f, yMin);
            r.anchorMax = new Vector2(1f, yMax);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>Полоса во всю ширину: от y снизу, высотой height.</summary>
        private static void Band(RectTransform r, float y, float height)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(0f, height);
        }
    }
}
