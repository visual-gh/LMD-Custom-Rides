using LMDCustomRides.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LMDCustomRides.Ui.Kit;

/// <summary>A black screen over the menus, faded in and out, to hide the menus being switched behind it.</summary>
internal static class Fade
{
    private const float Duration = 0.2f;

    private static CanvasGroup _group;
    private static float _target;

    /// <summary>True once the screen is fully black.</summary>
    public static bool Covered => _group != null && _group.alpha >= 1f;

    public static bool Visible => _group != null && _group.alpha > 0f;

    public static void In() => _target = 1f;

    public static void Out() => _target = 0f;

    public static void Update()
    {
        if (_group == null)
        {
            if (_target <= 0f || !Build())
            {
                return;
            }
        }

        var step = Time.unscaledDeltaTime / Duration;
        _group.alpha = Mathf.MoveTowards(_group.alpha, _target, step);
        var on = _group.alpha > 0f;
        if (_group.gameObject.activeSelf != on)
        {
            _group.gameObject.SetActive(on);
        }
    }

    private static bool Build()
    {
        if (CustomTab.GameMenu == null)
        {
            return false;
        }

        // A screen overlay canvas of its own: always drawn over everything (the menus' canvas is in the 3D scene).
        var root = new GameObject("LMDCustomRides_Fade");
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var go = new GameObject("Black", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        return true;
    }
}