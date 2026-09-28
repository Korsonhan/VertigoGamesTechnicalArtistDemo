using UnityEngine;

namespace VertigoDemo.UI
{
    /// <summary>Keeps its rect inside the device safe area (notches, rounded corners, home indicator).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect appliedArea;
        Vector2Int appliedScreen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
                Apply();
        }

        void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(appliedArea.xMin / Screen.width, appliedArea.yMin / Screen.height);
            rect.anchorMax = new Vector2(appliedArea.xMax / Screen.width, appliedArea.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
