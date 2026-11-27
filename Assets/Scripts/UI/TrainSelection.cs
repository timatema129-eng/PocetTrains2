using System;
using UnityEngine;
using TrainManager.Core;

namespace TrainManager.UI
{
    /// <summary>
    /// Какой поезд сейчас выбран игроком. Один объект на сцене; окно станции, составление маршрута,
    /// статус и картинки поездов смотрят сюда. Хранится instanceId, а не номер в списке.
    /// </summary>
    public class TrainSelection : MonoBehaviour
    {
        public GameRoot root;

        public string SelectedId { get; private set; }

        public event Action<TrainInstance> Changed;

        public TrainInstance Selected
        {
            get
            {
                if (root == null || !root.IsReady) return null;

                TrainInstance train = root.State.FindTrain(SelectedId);
                if (train == null && root.State.trains.Count > 0)
                {
                    // Ничего не выбрано или поезд исчез — берём первый
                    train = root.State.trains[0];
                    SelectedId = train.instanceId;
                }
                return train;
            }
        }

        public void Select(TrainInstance train)
        {
            if (train == null || train.instanceId == SelectedId) return;

            SelectedId = train.instanceId;
            Changed?.Invoke(train);
        }
    }
}
