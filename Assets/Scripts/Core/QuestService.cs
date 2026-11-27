using System.Collections.Generic;
using UnityEngine;
using TrainManager.Data;

namespace TrainManager.Core
{
    /// <summary>
    /// Задания-события в городах (Этап 10). Пример Артема: «успей привезти или увезти определённые вагоны
    /// в город или из города и получи эпический бокс». Типы — QuestKind, шаблоны — GameDatabase.questTemplates
    /// (новый вариант задания = новый шаблон, без нового менеджера).
    /// Задания появляются по реальному времени (раз в questSpawnMinutes, не больше maxActiveQuests),
    /// истекают по таймеру. Прогресс считается при доставке вагонов.
    /// </summary>
    public class QuestService
    {
        private readonly GameState state;
        private readonly GameDatabase db;
        private readonly IGameClock clock;
        private readonly GameEvents events;
        private readonly NetworkService network;
        private readonly RewardService rewards;
        private readonly System.Random random = new System.Random();

        public QuestService(GameState state, GameDatabase db, IGameClock clock, GameEvents events,
            NetworkService network, RewardService rewards)
        {
            this.state = state;
            this.db = db;
            this.clock = clock;
            this.events = events;
            this.network = network;
            this.rewards = rewards;
            if (state.quests == null) state.quests = new List<QuestInstance>();
            if (state.stats == null) state.stats = new PlayerStats();
            events.WagonDelivered += OnWagonDelivered;
        }

        public IReadOnlyList<QuestInstance> Active => state.quests;

        public QuestTemplate GetTemplate(QuestInstance q) => q != null ? db.GetQuestTemplate(q.templateId) : null;

        public List<QuestInstance> GetQuestsIn(string cityId)
        {
            var result = new List<QuestInstance>();
            foreach (QuestInstance q in state.quests)
                if (q != null && q.cityId == cityId) result.Add(q);
            return result;
        }

        public float GetSecondsLeft(QuestInstance q) =>
            q != null ? Mathf.Max(0f, (q.endUnixMs - clock.NowUnixMs) / 1000f) : 0f;

        /// <summary>«Привези 3 вагона в Лондон: 1/3».</summary>
        public string Describe(QuestInstance q)
        {
            QuestTemplate t = GetTemplate(q);
            CityData city = db.GetCity(q.cityId);
            string name = city != null ? city.displayName : q.cityId;
            string what = t != null && t.kind == QuestKind.TakeFrom
                ? $"Увези {q.required} ваг. из города {name} (купленные здесь, доставка куда угодно)"
                : $"Привези {q.required} ваг. в город {name}";
            return $"{what}: {q.progress}/{q.required}";
        }

        public string DescribeReward(QuestInstance q)
        {
            QuestTemplate t = GetTemplate(q);
            return t != null ? rewards.Describe(t.reward) : "—";
        }

        /// <summary>Истечение и появление заданий. Вызывается каждый кадр и после загрузки.</summary>
        public void Tick()
        {
            long now = clock.NowUnixMs;
            bool changed = false;

            // Истекшие и сломанные (шаблон или город убрали) — убрать
            for (int i = state.quests.Count - 1; i >= 0; i--)
            {
                QuestInstance q = state.quests[i];
                if (q == null || GetTemplate(q) == null || db.GetCity(q.cityId) == null || now >= q.endUnixMs)
                {
                    if (q != null) Debug.Log($"[Quest] Задание {q.questId} истекло");
                    state.quests.RemoveAt(i);
                    changed = true;
                }
            }

            if (state.nextQuestUnixMs <= 0) state.nextQuestUnixMs = now; // первое задание — сразу
            if (now >= state.nextQuestUnixMs && state.quests.Count < db.maxActiveQuests)
            {
                if (Spawn(now)) changed = true;
                state.nextQuestUnixMs = now + db.questSpawnMinutes * 60L * 1000L;
                changed = true;
            }

            if (changed) events.RaiseQuestsChanged();
        }

        private bool Spawn(long now)
        {
            var templates = new List<QuestTemplate>();
            foreach (QuestTemplate t in db.questTemplates)
                if (t != null && !string.IsNullOrEmpty(t.templateId)) templates.Add(t);
            if (templates.Count == 0) return false;

            // Город: открытый регион, без другого задания
            var cities = new List<CityData>();
            foreach (CityData c in db.cities)
                if (c != null && network.IsCityAccessible(c) && GetQuestsIn(c.cityId).Count == 0) cities.Add(c);
            if (cities.Count == 0) return false;

            QuestTemplate template = templates[random.Next(templates.Count)];
            CityData city = cities[random.Next(cities.Count)];
            int min = Mathf.Max(1, template.minWagons);
            int max = Mathf.Max(min, template.maxWagons);

            var q = new QuestInstance
            {
                questId = state.NewInstanceId("quest"),
                templateId = template.templateId,
                cityId = city.cityId,
                required = random.Next(min, max + 1),
                startUnixMs = now,
                endUnixMs = now + Mathf.Max(1, template.durationMinutes) * 60L * 1000L
            };
            state.quests.Add(q);
            Debug.Log($"[Quest] Новое задание: {Describe(q)}, награда {DescribeReward(q)}");
            return true;
        }

        private void OnWagonDelivered(WagonInstance wagon, CityData city, long income)
        {
            if (wagon == null || city == null) return;
            long now = clock.NowUnixMs;
            bool changed = false;

            for (int i = state.quests.Count - 1; i >= 0; i--)
            {
                QuestInstance q = state.quests[i];
                QuestTemplate t = GetTemplate(q);
                if (t == null || now >= q.endUnixMs) continue;

                bool counts = t.kind == QuestKind.DeliverTo
                    ? city.cityId == q.cityId
                    : wagon.originCityId == q.cityId;
                if (!counts) continue;

                q.progress++;
                changed = true;
                if (q.progress >= q.required)
                {
                    state.quests.RemoveAt(i);
                    state.stats.questsCompleted++;
                    rewards.Grant(t.reward, $"задание «{Describe(q)}»");
                    Debug.Log($"[Quest] Задание {q.questId} выполнено");
                }
            }

            if (changed) events.RaiseQuestsChanged();
        }
    }
}
