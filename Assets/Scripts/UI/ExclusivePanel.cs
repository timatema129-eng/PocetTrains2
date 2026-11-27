using System.Collections.Generic;
using UnityEngine;

namespace TrainManager.UI
{
    /// <summary>
    /// Окно, которое открывается только одно за раз: когда оно показывается, остальные такие окна закрываются.
    /// Вешается на корень окна (станция, поезда, боксы, магазин, задания, метро, вид сбоку, маршрут).
    /// </summary>
    public class ExclusivePanel : MonoBehaviour
    {
        private static readonly List<ExclusivePanel> Open = new List<ExclusivePanel>();

        private void OnEnable()
        {
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                ExclusivePanel other = Open[i];
                if (other != null && other != this) other.gameObject.SetActive(false);
            }
            Open.RemoveAll(p => p == null || !p.gameObject.activeSelf);
            if (!Open.Contains(this)) Open.Add(this);
        }

        private void OnDisable() => Open.Remove(this);
    }
}
