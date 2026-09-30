using UnityEngine;
using UnityEngine.EventSystems;

namespace VertigoDemo.UI
{
    /// <summary>Squashes a button slightly while it is held, so every tap gets immediate feedback.</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0.5f, 1f)] float pressedScale = 0.92f;

        Coroutine routine;

        public void OnPointerDown(PointerEventData eventData) => Animate(pressedScale);

        public void OnPointerUp(PointerEventData eventData) => Animate(1f);

        void OnDisable()
        {
            routine = null;
            transform.localScale = Vector3.one;
        }

        void Animate(float scale)
        {
            if (routine != null)
                StopCoroutine(routine);
            float from = transform.localScale.x;
            routine = StartCoroutine(Tween.Run(0.08f, t => transform.localScale = Vector3.one * Mathf.Lerp(from, scale, t)));
        }
    }
}
