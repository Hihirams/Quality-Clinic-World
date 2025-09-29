// Assets/Scripts/AppleStyleExitButton.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace QualityClinic.UI
{
    /// <summary>
    /// Botón universal de salida estilo Apple que vive en un Canvas overlay ScreenSpaceOverlay
    /// con z-order máximo. Incluye modal de confirmación (bloquea clics por detrás).
    /// - No requiere referencias en escena.
    /// - Garantiza existencia de EventSystem.
    /// - Silencioso por defecto (usa QCLog para verboso).
    /// </summary>
    [DisallowMultipleComponent]
    public class AppleStyleExitButton : MonoBehaviour
    {
        #region Serialized Fields
        // (No exponer nada extra al Inspector para no introducir dependencias ni referencias)
        #endregion

        #region Private State
        private static AppleStyleExitButton _instance;

        // Diseño y z-order
        private const int BTN_SIZE = 32;
        private const int CANVAS_SORT_ORDER = 32760; // Muy por encima
        private const float BACKDROP_ALPHA = 0.55f;

        // Refs principales
        private Canvas _overlayCanvas;
        private RectTransform _canvasRoot;
        private Button _exitButton;

        // Modal
        private GameObject _modalBackdrop;
        private CanvasGroup _modalGroup;
        private RectTransform _modalPanel;

        // Sprites generados en runtime (9-slice)
        private Sprite _roundedBtn, _roundedPanel, _roundedPill;

        // Paleta (neutra, tipo Apple)
        private static readonly Color _buttonBg = new Color(0.96f, 0.96f, 0.97f, 0.95f);
        private static readonly Color _buttonHover = new Color(0.98f, 0.98f, 0.99f, 0.98f);
        private static readonly Color _buttonPress = new Color(0.90f, 0.90f, 0.92f, 0.95f);
        private static readonly Color _iconColor = new Color(0.20f, 0.20f, 0.20f, 0.90f);
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            // Asegurar singleton
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeButton();
        }

        private void Start()
        {
            // Mantener visible desde el inicio
            if (_overlayCanvas != null)
            {
                BringOverlayToFront();
                ShowButton();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Método público para forzar mostrar el botón (desde otros scripts).
        /// </summary>
        public void ForceShowButton()
        {
            if (_exitButton != null)
            {
                BringOverlayToFront();
                ShowButton();
            }
        }

        /// <summary>
        /// Acceso estático para asegurar visibilidad del botón.
        /// </summary>
        public static void EnsureButtonVisible()
        {
            if (_instance != null)
                _instance.ForceShowButton();
        }
        #endregion

        #region Internal Helpers
        private void InitializeButton()
        {
            BuildSprites();
            BuildCanvasOverlay();   // Canvas propio y altísimo
            BuildExitButton();      // Botón persistente
            BuildConfirmModal();    // Modal con backdrop que bloquea
            BringOverlayToFront();  // Garantiza tope visual
            ShowButton();           // Activo de inmediato
        }

        private void BuildCanvasOverlay()
        {
            var canvasObj = new GameObject("AppleExitOverlay",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObj);

            _overlayCanvas = canvasObj.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = CANVAS_SORT_ORDER;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot = canvasObj.transform as RectTransform;
            _canvasRoot.anchorMin = Vector2.zero; _canvasRoot.anchorMax = Vector2.one;
            _canvasRoot.offsetMin = Vector2.zero; _canvasRoot.offsetMax = Vector2.zero;

            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
                QCLog.Info("[AppleStyleExitButton] EventSystem creado automáticamente.");
            }
        }

        private void BuildExitButton()
        {
            var go = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(BTN_SIZE, BTN_SIZE);
            rt.anchoredPosition = new Vector2(-20, -10);

            var img = go.GetComponent<Image>();
            img.sprite = _roundedBtn; img.type = Image.Type.Sliced;
            img.color = _buttonBg;

            var sh = go.GetComponent<Shadow>();
            sh.effectDistance = new Vector2(0, -2);
            sh.effectColor = new Color(0, 0, 0, 0.15f);

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = _buttonBg;
            cb.highlightedColor = _buttonHover;
            cb.pressedColor = _buttonPress;
            cb.selectedColor = _buttonBg;
            cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            // Ícono (X) con dos líneas
            CreateIconLine(rt, "L1", BTN_SIZE * 0.45f, 2f, 45f);
            CreateIconLine(rt, "L2", BTN_SIZE * 0.45f, 2f, -45f);

            btn.onClick.AddListener(ShowModal);
            _exitButton = btn;

            go.SetActive(true);
        }

        private void CreateIconLine(RectTransform parent, string name, float length, float thickness, float rot)
        {
            var l = new GameObject(name, typeof(RectTransform), typeof(Image));
            l.transform.SetParent(parent, false);
            var r = l.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(length, thickness);
            r.localRotation = Quaternion.Euler(0, 0, rot);
            var img = l.GetComponent<Image>(); img.color = _iconColor; img.raycastTarget = false;
        }

        private void BuildConfirmModal()
        {
            var backdrop = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            backdrop.transform.SetParent(_canvasRoot, false);

            var rtB = backdrop.GetComponent<RectTransform>();
            rtB.anchorMin = Vector2.zero; rtB.anchorMax = Vector2.one;
            rtB.offsetMin = Vector2.zero; rtB.offsetMax = Vector2.zero;

            var imgB = backdrop.GetComponent<Image>();
            imgB.color = new Color(0, 0, 0, BACKDROP_ALPHA);

            _modalGroup = backdrop.GetComponent<CanvasGroup>();
            _modalGroup.alpha = 0f;
            _modalGroup.blocksRaycasts = false;
            _modalGroup.interactable = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Shadow));
            panel.transform.SetParent(backdrop.transform, false);
            _modalPanel = panel.GetComponent<RectTransform>();
            _modalPanel.anchorMin = _modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _modalPanel.sizeDelta = new Vector2(380, 200);

            var imgP = panel.GetComponent<Image>();
            imgP.sprite = _roundedPanel; imgP.type = Image.Type.Sliced;
            imgP.color = new Color(1, 1, 1, 0.98f);

            var sh = panel.GetComponent<Shadow>();
            sh.effectDistance = new Vector2(0, -2);
            sh.effectColor = new Color(0, 0, 0, 0.2f);

            var tGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGO.transform.SetParent(panel.transform, false);
            var rtT = tGO.GetComponent<RectTransform>();
            rtT.anchorMin = new Vector2(0, 1); rtT.anchorMax = new Vector2(1, 1);
            rtT.offsetMin = new Vector2(24, -60); rtT.offsetMax = new Vector2(-24, -16);
            var t = tGO.GetComponent<TextMeshProUGUI>();
            t.text = "Salir de Quality Clinic";
            t.fontSize = 24;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.1f, 0.1f, 0.1f, 1);

            var mGO = new GameObject("Msg", typeof(RectTransform), typeof(TextMeshProUGUI));
            mGO.transform.SetParent(panel.transform, false);
            var rtM = mGO.GetComponent<RectTransform>();
            rtM.anchorMin = new Vector2(0, 1); rtM.anchorMax = new Vector2(1, 1);
            rtM.offsetMin = new Vector2(26, -120); rtM.offsetMax = new Vector2(-26, -64);
            var m = mGO.GetComponent<TextMeshProUGUI>();
            m.text = "¿Seguro que quieres salir?";
            m.fontSize = 18;
            m.alignment = TextAlignmentOptions.Center;
            m.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(panel.transform, false);
            var rtR = row.GetComponent<RectTransform>();
            rtR.anchorMin = new Vector2(0, 0); rtR.anchorMax = new Vector2(1, 0);
            rtR.sizeDelta = new Vector2(0, 60); rtR.anchoredPosition = new Vector2(0, 24);

            var btnCancel = MakePill(rtR, "Cancelar", new Vector2(-90, 0), true);
            btnCancel.onClick.AddListener(HideModal);

            var btnQuit = MakePill(rtR, "Salir", new Vector2(90, 0), false);
            btnQuit.onClick.AddListener(QuitApp);

            _modalBackdrop = backdrop;
            _modalBackdrop.SetActive(false);
        }

        private Button MakePill(RectTransform parent, string label, Vector2 pos, bool neutral)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(140, 44);
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.sprite = _roundedPill; img.type = Image.Type.Sliced;

            Color baseFill = neutral ? new Color(0.95f, 0.95f, 0.97f, 1) : new Color(1.00f, 0.92f, 0.92f, 1);
            Color hiFill = neutral ? new Color(0.98f, 0.98f, 0.99f, 1) : new Color(1.00f, 0.95f, 0.95f, 1);

            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.normalColor = baseFill;
            cb.highlightedColor = hiFill;
            cb.pressedColor = new Color(baseFill.r * 0.95f, baseFill.g * 0.95f, baseFill.b * 0.95f, 1);
            cb.disabledColor = new Color(0.85f, 0.85f, 0.88f, 1);
            b.colors = cb;

            var txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);

            var rtTxt = txtGO.GetComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
            rtTxt.offsetMin = new Vector2(10, 6); rtTxt.offsetMax = new Vector2(-10, -6);

            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 18;
            txt.color = neutral ? new Color(0.12f, 0.12f, 0.14f, 1) : new Color(0.85f, 0.12f, 0.12f, 1);

            return b;
        }

        private void ShowButton()
        {
            if (_exitButton != null)
            {
                _exitButton.gameObject.SetActive(true);
                _exitButton.interactable = true;
            }
        }

        private void ShowModal()
        {
            BringOverlayToFront();
            _modalBackdrop.SetActive(true);
            _modalBackdrop.transform.SetAsLastSibling();
            _modalGroup.alpha = 1f;
            _modalGroup.blocksRaycasts = true;
            _modalGroup.interactable = true;
        }

        private void HideModal()
        {
            _modalGroup.alpha = 0f;
            _modalGroup.blocksRaycasts = false;
            _modalGroup.interactable = false;
            _modalBackdrop.SetActive(false);
        }

        private void BringOverlayToFront()
        {
            if (_overlayCanvas == null) return;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = CANVAS_SORT_ORDER;
            _canvasRoot.SetAsLastSibling();
        }

        private void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildSprites()
        {
            _roundedBtn = MakeRounded(128, 128, 28);
            _roundedPanel = MakeRounded(128, 128, 40);
            _roundedPill = MakeRounded(128, 64, 36);
        }

        private Sprite MakeRounded(int w, int h, int r)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside =
                        (x >= r && x < w - r) || (y >= r && y < h - r) ||
                        (Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) <= r) ||
                        (Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, r)) <= r) ||
                        (Vector2.Distance(new Vector2(x, y), new Vector2(r, h - r - 1)) <= r) ||
                        (Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, h - r - 1)) <= r);

                    px[y * w + x] = inside ? Color.white : new Color(1, 1, 1, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            var border = new Vector4(r, r, r, r);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
        #endregion

        #region Debug
        // (Nada que ejecutar en Update; componente es estático/ligero)
        #endregion
    }
    
    
}
