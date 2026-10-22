using Il2Cpp;
using Il2CppMegagon.Downhill.UI;
using Il2CppMegagon.Downhill.UI.Screens;
using Il2CppTMPro;
using LMDCustomRides.Game;
using UnityEngine;

namespace LMDCustomRides.Ui.Kit;

/// <summary>
/// The game's pop-up (a copy of Daily Rides' "time is running out" box): a headline, an optional message, an optional
/// pick list (a <see cref="RowList"/>) or a text field, and two key labels, accept on the right and back on the left.
/// It shows and hides with its own animations, and the game menu treats it as one of its pop-ups (the tabs are locked
/// while it is open). Enter accepts (or picks the selected entry), Escape closes it.
/// </summary>
internal sealed class Popup
{
    // The pop-up's hide animation; after it the pop-up is switched off whatever happened (a fallback).
    private const float HideSeconds = 0.5f;

    private readonly GameObject _root;
    private readonly ScreenElement _element;
    private readonly CanvasGroup _group;
    private readonly RectTransform _box;
    private readonly TextMeshProUGUI _headline;
    private readonly TextMeshProUGUI _message;
    private readonly KeyLabel _accept;
    private readonly KeyLabel _cancel;
    private readonly TextField _field;
    private readonly float _minHeight;
    private Action _onAccept;
    private float _openedAt;
    private float _offAt = -1f;

    public RowList List { get; }

    public bool IsOpen { get; private set; }

    public Popup(Transform parent)
    {
        _root = UnityEngine.Object.Instantiate(GameAssets.PopUp, parent, false);
        _root.name = "LMDCustomRides_Popup";
        Screens.Disarm(_root);
        _element = _root.GetComponent<ScreenElement>();
        _group = _root.GetComponent<CanvasGroup>();
        _box = _root.transform.Find("Image_PopUpBackground").GetComponent<RectTransform>();
        _minHeight = _box.sizeDelta.y;
        var content = _box.Find("Content");
        _headline = content.Find("TextMeshPro Text_Headline").GetComponent<TextMeshProUGUI>();
        DisableLocalization(_headline);
        _message = content.Find("TextMeshPro Text_TimeRemaining").GetComponent<TextMeshProUGUI>();
        DisableLocalization(_message);
        _accept = new KeyLabel(content.Find("TextMeshPro Text_Button_Accept").GetComponent<TextMeshProUGUI>(), GameAction.Submit);
        _cancel = new KeyLabel(content.Find("TextMeshPro Text_Button_Cancel").GetComponent<TextMeshProUGUI>(), GameAction.Cancel);

        // The pick list, or the text field, sits between the headline and the key labels.
        var inner = _box.sizeDelta.x - 2 * Tokens.PopupMargin;
        List = new RowList(content, new Vector2(Tokens.PopupMargin, -Tokens.PopupListTop), inner).Scrolling(Tokens.PopupMaxRows);
        // The text field in the middle of the box.
        _field = new TextField(content, new Vector2(Tokens.PopupMargin, -(_minHeight - Tokens.TitleHeight) / 2f), new Vector2(inner, Tokens.TitleHeight));
        _root.SetActive(false);
    }

    private static void DisableLocalization(TextMeshProUGUI text)
    {
        var localized = text.GetComponent<TextMeshProLocalized>();
        if (localized != null)
        {
            localized.enabled = false;
        }
    }

    /// <summary>
    /// Opens the pop-up: a headline, a message (or null when the list says it all), the accept label's loca key
    /// ("{0} Add") and what accepting does (null: Enter picks the list's selected entry).
    /// </summary>
    public void Open(string headline, string message, string acceptKey, Action accept = null)
    {
        _onAccept = accept;
        UiTools.SetText(_headline, headline);
        _message.gameObject.SetActive(message != null);
        UiTools.SetText(_message, message ?? "");
        _accept.SetText(acceptKey);
        _cancel.SetText(Tokens.PopupBackKey);
        _field.Cancel();

        // Room for the list's rows (a longer list scrolls).
        var rows = List.Slots * Tokens.RowHeight;
        _box.sizeDelta = new Vector2(_box.sizeDelta.x, Mathf.Max(_minHeight, Tokens.PopupListTop + rows + Tokens.PopupListBottom));
        List.Root.SetActive(List.Count > 0);
        Show();
        List.SetActive(true);
    }

    /// <summary>
    /// Opens the pop-up (or turns the open one) into a text field: Enter gives <paramref name="done"/> the text and
    /// closes it, Escape closes it (or the gamepad's confirm and back buttons; typing needs a keyboard, on screen or not).
    /// </summary>
    public void OpenInput(string headline, string text, string acceptKey, Action<string> done)
    {
        _onAccept = null;
        UiTools.SetText(_headline, headline);
        _message.gameObject.SetActive(false);
        _accept.SetText(acceptKey);
        _cancel.SetText(Tokens.PopupBackKey);
        List.SetActive(false);
        List.Root.SetActive(false);
        _box.sizeDelta = new Vector2(_box.sizeDelta.x, _minHeight);
        Show();
        _field.Begin(text, t =>
        {
            Close();
            done(t);
        }, Close);
    }

    private void Show()
    {
        _offAt = -1f;
        _root.SetActive(true);
        if (!IsOpen)
        {
            try
            {
                _element.Method_Public_Virtual_New_Void_3();
            }
            catch (Exception e)
            {
                Mod.Log.Warning("pop-up show: " + e.Message);
            }

            CustomTab.GameMenu?.Method_Public_Void_ScreenElement_0(_element);
            GameInput.HoldMenu();
        }

        if (_group != null && _group.alpha < 0.01f)
        {
            _group.alpha = 1f;
        }

        IsOpen = true;
        _openedAt = Time.unscaledTime;
    }

    /// <summary>A pop-up has (or just had) the input: the key that closed it (Escape, Enter) does nothing else.</summary>
    public static bool HoldsInput => GameInput.MenuHeld;

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _field.Cancel();
        List.SetActive(false);
        GameInput.ReleaseMenu();
        CustomTab.GameMenu?.Method_Public_Void_ScreenElement_1(_element);
        try
        {
            _element.Method_Public_Virtual_New_Void_4();
        }
        catch (Exception e)
        {
            Mod.Log.Warning("pop-up hide: " + e.Message);
        }

        _offAt = Time.unscaledTime + HideSeconds;
    }

    /// <summary>
    /// Closed at once, without animation: when the page goes away, and as a fallback when it shows again, so a pop-up
    /// can never stay on screen after its time.
    /// </summary>
    public void Reset()
    {
        Close();
        _offAt = -1f;
        if (_root.activeSelf)
        {
            _root.SetActive(false);
        }
    }

    /// <summary>Per frame, open or not: switches the pop-up off once its hide animation is over.</summary>
    public void Tick()
    {
        if (!IsOpen && _offAt >= 0f && Time.unscaledTime >= _offAt)
        {
            _offAt = -1f;
            _root.SetActive(false);
        }
    }

    /// <summary>Per frame while open: its input (not in the frame it opened: that key opened it).</summary>
    public void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        _field.Update();
        if (!IsOpen || _field.Editing)
        {
            return;
        }

        List.Update();
        _accept.Apply();
        _cancel.Apply();
        if (Time.unscaledTime - _openedAt < 0.1f)
        {
            return;
        }

        if (GameInput.Pressed(GameAction.Cancel) || Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
        else if (GameInput.Pressed(GameAction.Submit) || (Input.GetMouseButtonDown(0) && UiTools.MouseOver(_accept.Rect)))
        {
            if (_onAccept != null)
            {
                Close();
                _onAccept();
            }
            else
            {
                List.Activate();
            }
        }
        else if (Input.GetMouseButtonDown(0) && UiTools.MouseOver(_cancel.Rect))
        {
            Close();
        }
    }

    public void LateUpdate()
    {
        if (IsOpen)
        {
            List.LateUpdate();
            _accept.Apply();
            _cancel.Apply();
        }
    }
}
