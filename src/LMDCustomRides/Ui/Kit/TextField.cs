using Il2CppTMPro;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// A one-line text input in the game's style (a copy of the Daily Rides menu's hidden seed field). While it is open
/// the game's input is off (typing E or Q would switch tabs otherwise): Enter or the gamepad's confirm button confirms,
/// Escape or its back button cancels (typing can come from an on-screen keyboard). The game's
/// input comes back once the keys are let go, so the Enter or Escape that closed it does nothing else.
/// </summary>
internal sealed class TextField
{
    private readonly TMP_InputField _field;
    private readonly float _fontSize;
    private Action<string> _done;
    private Action _cancelled;

    public bool Editing { get; private set; }

    public TextField(Transform parent, Vector2 position, Vector2 size, float fontSize = Tokens.RowFont * Tokens.TitleScale)
    {
        _fontSize = fontSize;
        var copy = UnityEngine.Object.Instantiate(GameAssets.InputField.gameObject, parent, false);
        copy.name = "LMDCustomRides_TextField";
        _field = copy.GetComponent<TMP_InputField>();
        _field.onEndEdit = new TMP_InputField.SubmitEvent();
        _field.onSubmit = new TMP_InputField.SubmitEvent();
        _field.onValueChanged = new TMP_InputField.OnChangeEvent();
        _field.characterLimit = Playlist.MaxName;
        _field.contentType = TMP_InputField.ContentType.Standard;
        _field.lineType = TMP_InputField.LineType.SingleLine;
        UiTools.Place(copy.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), position, size);
        Style(copy);
        copy.SetActive(false);
    }

    /// <summary>The debug field's plain box becomes the text it edits: a light strip, the game's font at title size.</summary>
    private void Style(GameObject copy)
    {
        var background = copy.GetComponent<UnityEngine.UI.Image>();
        if (background != null)
        {
            background.sprite = GameAssets.Sprite(Tokens.LightStrip);
            background.type = UnityEngine.UI.Image.Type.Simple;
            background.color = Tokens.White;
        }

        var font = GameAssets.FontSource;
        var text = _field.textComponent?.TryCast<TextMeshProUGUI>();
        if (text != null)
        {
            text.font = font.font;
            text.fontSharedMaterial = font.fontSharedMaterial;
            text.enableAutoSizing = false;
            text.fontSize = _fontSize;
            text.color = Tokens.White;
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        _field.placeholder?.gameObject.SetActive(false);
        var viewport = _field.textViewport;
        if (viewport != null)
        {
            viewport.offsetMin = new Vector2(Tokens.LabelX / 3f, 0f);
            viewport.offsetMax = new Vector2(-Tokens.LabelX / 3f, 0f);
        }

        _field.customCaretColor = true;
        _field.caretColor = Tokens.White;
        _field.caretWidth = 3;
        _field.selectionColor = new Color(Tokens.Orange.r, Tokens.Orange.g, Tokens.Orange.b, 0.5f);
    }

    // The game's own events around its leaderboard name entry: typing starts (the menus stop reacting to keys) and ends.
    private const int TypingStarts = 700;
    private const int TypingEnds = 701;

    private static void Send(int gameEvent)
    {
        try
        {
            Il2Cpp.ObjectPublicInObDiIn2UnInStDi2Unique.Method_Public_Static_Void_EnumPublicSealedva71Unique_0((Il2Cpp.EnumPublicSealedva71Unique)gameEvent);
        }
        catch (Exception e)
        {
            Mod.Log.Warning("text field: " + e.Message);
        }
    }

    /// <summary>
    /// Opens the field with a text; <paramref name="done"/> gets the confirmed text, <paramref name="cancelled"/> runs
    /// on Escape (or an empty text).
    /// </summary>
    public void Begin(string text, Action<string> done, Action cancelled = null)
    {
        _done = done;
        _cancelled = cancelled;
        _typingEndPending = false;
        _field.gameObject.SetActive(true);
        _field.text = text ?? "";
        Send(TypingStarts);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_field.gameObject);
        _field.ActivateInputField();
        Editing = true;
        _openedFrame = Time.frameCount;
        GameInput.Block(true);
        GameInput.HoldMenu();
    }

    private static bool _typingEndPending;

    /// <summary>
    /// Per frame, whatever page shows: once the keys are let go after a field closed, the game gets its input back
    /// (also when the page went away meanwhile).
    /// </summary>
    public static void Tick()
    {
        if (_typingEndPending && !Input.anyKey)
        {
            _typingEndPending = false;
            Send(TypingEnds);
            GameInput.Block(false);
        }
    }

    // The frame the field opened: the key that opened it (Enter on "+ New Playlist") is not its confirm.
    private int _openedFrame = -10;

    public void Update()
    {
        if (!Editing || Time.frameCount == _openedFrame)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || GameInput.PressedThrough(GameAction.Submit))
        {
            Close(true);
        }
        else if (Input.GetKeyDown(KeyCode.Escape) || GameInput.PressedThrough(GameAction.Cancel))
        {
            Close(false);
        }
        else if (!_field.isFocused)
        {
            _field.ActivateInputField();
        }
    }

    /// <summary>Closes without a result (the page went away).</summary>
    public void Cancel()
    {
        if (Editing)
        {
            Close(false);
        }
    }

    private void Close(bool confirmed)
    {
        Editing = false;
        _typingEndPending = true;
        GameInput.ReleaseMenu();
        var text = _field.text?.Trim() ?? "";
        _field.DeactivateInputField();
        _field.gameObject.SetActive(false);
        if (confirmed && text.Length > 0)
        {
            _done?.Invoke(text);
        }
        else
        {
            _cancelled?.Invoke();
        }
    }
}
