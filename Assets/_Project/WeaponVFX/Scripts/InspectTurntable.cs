using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VertigoDemo.WeaponVFX
{
    /// <summary>
    /// Weapon inspect controls on the pivot the weapon hangs from: drag to rotate, 1 and 2 for the
    /// side and three-quarter views of the reference, and a slow sway once the player lets go.
    /// </summary>
    public sealed class InspectTurntable : MonoBehaviour
    {
        [Tooltip("Yaw (x) and tilt (y) of each preset view, in degrees.")]
        [SerializeField] Vector2[] views = { new Vector2(0f, 0f), new Vector2(-34f, 6f) };
        [SerializeField] float dragDegreesPerPixel = 0.25f;
        [SerializeField] Vector2 tiltLimits = new Vector2(-25f, 25f);
        [SerializeField] float smoothing = 8f;

        [Header("Idle sway")]
        [SerializeField] float swayAngle = 7f;
        [SerializeField] float swayPeriod = 6f;
        [SerializeField] float swayDelay = 1.5f;

        Vector2 target;
        Vector2 current;
        float idleTime;

        void Start()
        {
            target = current = views[0];
            Apply(0f);
        }

        void Update()
        {
            if (TryGetDrag(out Vector2 delta))
            {
                target += new Vector2(-delta.x, delta.y) * dragDegreesPerPixel;
                target.y = Mathf.Clamp(target.y, tiltLimits.x, tiltLimits.y);
                idleTime = 0f;
            }
            else
            {
                idleTime += Time.deltaTime;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    ShowView(0);
                if (keyboard.digit2Key.wasPressedThisFrame && views.Length > 1)
                    ShowView(1);
            }

            current = Vector2.Lerp(current, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            float swayWeight = Mathf.Clamp01((idleTime - swayDelay) / 1.5f);
            Apply(Mathf.Sin(idleTime * 2f * Mathf.PI / swayPeriod) * swayAngle * swayWeight);
        }

        public void ShowView(int index)
        {
            target = views[Mathf.Clamp(index, 0, views.Length - 1)];
            idleTime = 0f;
        }

        /// <summary>Turns the rifle a full circle at a steady speed, then keeps its current view.</summary>
        public IEnumerator Spin(float degrees, float duration)
        {
            float start = target.x;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                target.x = start - degrees * Mathf.SmoothStep(0f, 1f, t / duration);
                idleTime = 0f;
                yield return null;
            }
            // Unwind the extra turn on both angles so the next view does not spin back.
            target.x = start;
            current.x += degrees;
        }

        static bool TryGetDrag(out Vector2 delta)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                delta = mouse.delta.ReadValue();
                return true;
            }
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                delta = touch.primaryTouch.delta.ReadValue();
                return true;
            }
            delta = Vector2.zero;
            return false;
        }

        // Yaw turns around world up; tilt rolls around the barrel axis, which faces the camera's right.
        void Apply(float sway) =>
            transform.localRotation = Quaternion.AngleAxis(current.x + sway, Vector3.up) * Quaternion.AngleAxis(current.y, Vector3.forward);
    }
}
