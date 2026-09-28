using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VertigoDemo.UI
{
    /// <summary>Exposes taps on a graphic as an event (drags are ignored).</summary>
    public sealed class PointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging)
                Clicked?.Invoke();
        }
    }
}
