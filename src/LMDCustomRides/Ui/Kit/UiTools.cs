using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>Small helpers for building UI out of copies of the game's own UI pieces.</summary>
internal static class UiTools
{
    public static Transform Find(Transform root, string path) => root == null ? null : root.Find(path);

    public static T Get<T>(Transform root, string path) where T : Component
    {
        var t = Find(root, path);
        return t == null ? null : t.GetComponent<T>();
    }

    /// <summary>
    /// Removes the game's own MonoBehaviours (screen elements, animations, localization, input) from a copied
    /// object, leaving Unity's UI components (images, texts, layout) so it only looks like the original.
    /// </summary>
    public static void StripBehaviours(GameObject root)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                continue;
            }

            // Buttons and event triggers keep their wiring to the original screen's methods, so they go too.
            var type = behaviour.GetIl2CppType();
            var ns = type.Namespace ?? "";
            var input = type.Name is "Button" or "EventTrigger" or "Toggle";
            if (!input && (ns.StartsWith("UnityEngine", StringComparison.Ordinal) || ns.StartsWith("TMPro", StringComparison.Ordinal)))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(behaviour);
        }

        foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    public static void SetText(TextMeshProUGUI label, string text)
    {
        if (label != null && label.text != text)
        {
            label.text = text;
        }
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static bool MouseOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
    }

    public static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic != null)
        {
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
