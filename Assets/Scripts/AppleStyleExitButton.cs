// Assets/Scripts/AppleStyleExitButton.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public class AppleStyleExitButton : MonoBehaviour
{
    static AppleStyleExitButton _instance;

    // Diseño y z-order
    const int BTN_SIZE = 32;
    const int CANVAS_SORT_ORDER = 32760;   // MUY por encima
    const float BACKDROP_ALPHA = 0.55f;

    // Refs
    Canvas overlayCanvas;
    RectTransform canvasRoot;
    Button exitButton;
    GameObject modalBackdrop;
    CanvasGroup modalGroup;
    RectTransform modalPanel;

    // Sprites
    Sprite roundedBtn, roundedPanel, roundedPill;

    // Colores
    readonly Color buttonBg = new Color(0.96f, 0.96f, 0.97f, 0.95f);
    readonly Color buttonHover = new Color(0.98f, 0.98f, 0.99f, 0.98f);
    readonly Color buttonPress = new Color(0.90f, 0.90f, 0.92f, 0.95f);
    readonly Color iconColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

    void Awake()
    {
        // Asegurar singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Inicializar inmediatamente
        InitializeButton();
    }

    void Start()
    {
        // Asegurar que el botón esté visible desde el start
        if (overlayCanvas != null)
        {
            BringOverlayToFront();
            ShowButton();
        }
    }

    void InitializeButton()
    {
        BuildSprites();
        BuildCanvasOverlay();   // Canvas propio y altísimo
        BuildExitButton();      // Botón siempre visible
        BuildConfirmModal();    // Modal bloquea clics detrás
        BringOverlayToFront();  // Refuerza tope
        ShowButton();           // Mostrar el botón inmediatamente
    }

    void ShowButton()
    {
        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
            exitButton.interactable = true;
        }
    }

    // ---------- Overlay por encima de todo ----------
    void BuildCanvasOverlay()
    {
        var canvasObj = new GameObject("AppleExitOverlay",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObj);

        overlayCanvas = canvasObj.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = CANVAS_SORT_ORDER;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot = canvasObj.transform as RectTransform;
        canvasRoot.anchorMin = Vector2.zero; canvasRoot.anchorMax = Vector2.one;
        canvasRoot.offsetMin = Vector2.zero; canvasRoot.offsetMax = Vector2.zero;

        EnsureEventSystem();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }

    // ---------- Botón esquina superior derecha ----------
    void BuildExitButton()
    {
        var go = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        go.transform.SetParent(canvasRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(BTN_SIZE, BTN_SIZE);
        rt.anchoredPosition = new Vector2(-20, -10);

        var img = go.GetComponent<Image>();
        img.sprite = roundedBtn; img.type = Image.Type.Sliced;
        img.color = buttonBg;

        var sh = go.GetComponent<Shadow>();
        sh.effectDistance = new Vector2(0, -2);
        sh.effectColor = new Color(0, 0, 0, 0.15f);

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = buttonBg;
        cb.highlightedColor = buttonHover;
        cb.pressedColor = buttonPress;
        cb.selectedColor = buttonBg;
        cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        // Ícono X
        CreateIconLine(rt, "L1", BTN_SIZE * 0.45f, 2f, 45f);
        CreateIconLine(rt, "L2", BTN_SIZE * 0.45f, 2f, -45f);

        btn.onClick.AddListener(ShowModal);
        exitButton = btn;

        // Asegurar que esté activo
        go.SetActive(true);
    }

    void CreateIconLine(RectTransform parent, string name, float length, float thickness, float rot)
    {
        var l = new GameObject(name, typeof(RectTransform), typeof(Image));
        l.transform.SetParent(parent, false);
        var r = l.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(length, thickness);
        r.localRotation = Quaternion.Euler(0, 0, rot);
        var img = l.GetComponent<Image>(); img.color = iconColor; img.raycastTarget = false;
    }

    // ---------- Modal de confirmación ----------
    void BuildConfirmModal()
    {
        var backdrop = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        backdrop.transform.SetParent(canvasRoot, false);

        var rtB = backdrop.GetComponent<RectTransform>();
        rtB.anchorMin = Vector2.zero; rtB.anchorMax = Vector2.one;
        rtB.offsetMin = Vector2.zero; rtB.offsetMax = Vector2.zero;
        var imgB = backdrop.GetComponent<Image>(); imgB.color = new Color(0, 0, 0, BACKDROP_ALPHA);

        modalGroup = backdrop.GetComponent<CanvasGroup>();
        modalGroup.alpha = 0f; modalGroup.blocksRaycasts = false; modalGroup.interactable = false;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Shadow));
        panel.transform.SetParent(backdrop.transform, false);
        modalPanel = panel.GetComponent<RectTransform>();
        modalPanel.anchorMin = modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
        modalPanel.sizeDelta = new Vector2(380, 200);

        var imgP = panel.GetComponent<Image>(); imgP.sprite = roundedPanel; imgP.type = Image.Type.Sliced; imgP.color = new Color(1, 1, 1, 0.98f);
        var sh = panel.GetComponent<Shadow>(); sh.effectDistance = new Vector2(0, -2); sh.effectColor = new Color(0, 0, 0, 0.2f);

        var tGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGO.transform.SetParent(panel.transform, false);
        var rtT = tGO.GetComponent<RectTransform>(); rtT.anchorMin = new Vector2(0, 1); rtT.anchorMax = new Vector2(1, 1);
        rtT.offsetMin = new Vector2(24, -60); rtT.offsetMax = new Vector2(-24, -16);
        var t = tGO.GetComponent<TextMeshProUGUI>(); t.text = "Salir de Quality Clinic"; t.fontSize = 24; t.alignment = TextAlignmentOptions.Center; t.color = new Color(0.1f, 0.1f, 0.1f, 1);

        var mGO = new GameObject("Msg", typeof(RectTransform), typeof(TextMeshProUGUI));
        mGO.transform.SetParent(panel.transform, false);
        var rtM = mGO.GetComponent<RectTransform>(); rtM.anchorMin = new Vector2(0, 1); rtM.anchorMax = new Vector2(1, 1);
        rtM.offsetMin = new Vector2(26, -120); rtM.offsetMax = new Vector2(-26, -64);
        var m = mGO.GetComponent<TextMeshProUGUI>(); m.text = "¿Seguro que quieres salir?"; m.fontSize = 18; m.alignment = TextAlignmentOptions.Center; m.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(panel.transform, false);
        var rtR = row.GetComponent<RectTransform>(); rtR.anchorMin = new Vector2(0, 0); rtR.anchorMax = new Vector2(1, 0); rtR.sizeDelta = new Vector2(0, 60); rtR.anchoredPosition = new Vector2(0, 24);

        var btnCancel = MakePill(rtR, "Cancelar", new Vector2(-90, 0), true);
        btnCancel.onClick.AddListener(HideModal);

        var btnQuit = MakePill(rtR, "Salir", new Vector2(90, 0), false);
        btnQuit.onClick.AddListener(QuitApp);

        modalBackdrop = backdrop;
        modalBackdrop.SetActive(false);
    }

    Button MakePill(RectTransform parent, string label, Vector2 pos, bool neutral)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(140, 44); rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>(); img.sprite = roundedPill; img.type = Image.Type.Sliced;
        Color baseFill = neutral ? new Color(0.95f, 0.95f, 0.97f, 1) : new Color(1.00f, 0.92f, 0.92f, 1);
        Color hiFill = neutral ? new Color(0.98f, 0.98f, 0.99f, 1) : new Color(1.00f, 0.95f, 0.95f, 1);
        var b = go.GetComponent<Button>();
        var cb = b.colors; cb.normalColor = baseFill; cb.highlightedColor = hiFill; cb.pressedColor = new Color(baseFill.r * 0.95f, baseFill.g * 0.95f, baseFill.b * 0.95f, 1); cb.disabledColor = new Color(0.85f, 0.85f, 0.88f, 1); b.colors = cb;

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        var rtTxt = txtGO.GetComponent<RectTransform>(); rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one; rtTxt.offsetMin = new Vector2(10, 6); rtTxt.offsetMax = new Vector2(-10, -6);
        var txt = txtGO.GetComponent<TextMeshProUGUI>(); txt.text = label; txt.alignment = TextAlignmentOptions.Center; txt.fontSize = 18; txt.color = neutral ? new Color(0.12f, 0.12f, 0.14f, 1) : new Color(0.85f, 0.12f, 0.12f, 1);

        return b;
    }

    // ---------- Mostrar/Ocultar ----------
    void ShowModal()
    {
        BringOverlayToFront();
        modalBackdrop.SetActive(true);
        modalBackdrop.transform.SetAsLastSibling();
        modalGroup.alpha = 1f;
        modalGroup.blocksRaycasts = true;
        modalGroup.interactable = true;
    }

    void HideModal()
    {
        modalGroup.alpha = 0f;
        modalGroup.blocksRaycasts = false;
        modalGroup.interactable = false;
        modalBackdrop.SetActive(false);
    }

    void BringOverlayToFront()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = CANVAS_SORT_ORDER;
            canvasRoot.SetAsLastSibling();
        }
    }

    // ---------- Salir ----------
    void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Sprites redondeados ----------
    void BuildSprites()
    {
        roundedBtn = MakeRounded(128, 128, 28);
        roundedPanel = MakeRounded(128, 128, 40);
        roundedPill = MakeRounded(128, 64, 36);
    }

    Sprite MakeRounded(int w, int h, int r)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = (x >= r && x < w - r) || (y >= r && y < h - r) ||
                  (Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) <= r) ||
                  (Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, r)) <= r) ||
                  (Vector2.Distance(new Vector2(x, y), new Vector2(r, h - r - 1)) <= r) ||
                  (Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, h - r - 1)) <= r);
                px[y * w + x] = inside ? Color.white : new Color(1, 1, 1, 0);
            }
        tex.SetPixels(px); tex.Apply();
        var border = new Vector4(r, r, r, r);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // Método público para forzar mostrar el botón si es necesario
    public void ForceShowButton()
    {
        if (_instance != null && _instance.exitButton != null)
        {
            _instance.BringOverlayToFront();
            _instance.ShowButton();
        }
    }

    // Método estático para acceder desde otros scripts
    public static void EnsureButtonVisible()
    {
        if (_instance != null)
        {
            _instance.ForceShowButton();
        }
    }
}