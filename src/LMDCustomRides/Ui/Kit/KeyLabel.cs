using Il2CppMegagon.Downhill.UI;
using Il2CppTMPro;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A copy of one of the game's input-bound texts ("[Enter] Start Ride", or the gamepad button): its text is a loca key
/// with "{0}" for the key. The game's element works out its text and the key for the device in use, but a copy does
/// not always write them out: written here every frame it is on screen (cheap when nothing changed).
/// </summary>
internal sealed class KeyLabel
{
    private readonly InteractiveUIElement _input;
    private readonly TextMeshProUGUI _text;
    private string _wanted;
    private string _applied;

    public KeyLabel(TextMeshProUGUI text, GameAction action)
    {
        _text = text;
        _input = text.GetComponent<InteractiveUIElement>();
        if (_input != null)
        {
            _input.m_rewiredActionID = GameInput.Id(action);
        }
    }

    public RectTransform Rect => _text == null ? null : _text.rectTransform;

    /// <summary>Binds another game action (its key or button shows from the next <see cref="Apply"/>).</summary>
    public void SetAction(GameAction action)
    {
        var id = GameInput.Id(action);
        if (_input != null && _input.m_rewiredActionID != id)
        {
            _input.m_rewiredActionID = id;
            _applied = null;
        }
    }

    /// <summary>The text, a loca key with "{0}" for the key ("{0} Remove Ride").</summary>
    public void SetText(string locaKey) => _wanted = locaKey;

    public void Apply()
    {
        if (_input == null || _text == null)
        {
            return;
        }

        if (!_text.gameObject.activeInHierarchy)
        {
            _applied = null;
            return;
        }

        if (_applied != _wanted)
        {
            _applied = _wanted;
            _input.Method_Public_Virtual_Final_New_Void_String_0(_wanted);
        }

        var format = _input.field_Private_String_0;
        var key = _input.field_Private_String_1;
        if (format != null && !format.Contains("{0}") && key != null && key.Contains("{0}"))
        {
            (format, key) = (key, format);
        }

        if (!string.IsNullOrEmpty(format) && format.Contains("{0}") && key != null)
        {
            UiTools.SetText(_text, format.Replace("{0}", key));
        }
    }
}
