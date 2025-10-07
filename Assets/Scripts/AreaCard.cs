using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AreaData = AreaManager.AreaData;

public class AreaCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Configuración del Área")]
    public string areaName;
    public AreaData areaData;

    [Header("Referencias UI (Generadas)")]
    private Canvas _cardCanvas;
    private GameObject _cardPanel;
    private Text _areaNameText;
    private Text _overallResultText;
    private Image _backgroundImage;

    [Header("Dimensiones de tarjeta")]
    public float cardWidth = 900f;
    public float cardHeight = 450f;
    public Vector3 cardOffset = new Vector3(0, 25, 0);

    [Header("Esquinas redondeadas")]
    [Range(2, 64)] public int cornerRadiusPx = 32;       // Más redondeado Apple-style
    [Range(32, 512)] public int roundedBaseSize = 64;

    [Header("Conexión tipo globo (flecha)")]
    public float connectionWidth = 0.18f;
    public float connectionOpacity = 0.95f;
    public bool inheritCardColor = true;
    public Color connectionColor = Color.white;
    public float pointerAttachInset = 0.008f;
    public float pointerCurveHeight = 0.9f;
    public float pointerBend = 0.15f;
    public int pointerSegments = 24;
    private LineRenderer _pointerLine;

    [Header("Animación")]
    public float hoverScale = 1.30f;
    public float animationSpeed = 13f;
    public float floatAmplitude = 1.5f;

    [Header("Top-Down")]
    [Tooltip("Aumenta la escala para legibilidad en vista top-down.")]
    public float topDownScaleMultiplier = 1.35f;
    [Tooltip("En Top-Down, rota sólo sobre Y para un billboard limpio.")]
    public bool yawOnlyInTopDown = true;

    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private bool _isHovering = false;
    private bool _topDownEnabled = false;
    private Camera _playerCamera;

    // Apple-style theme colors
    private Color _optimusColor  = new Color(0.15f, 0.65f, 0.25f, 1f); // >=90%
    private Color _healthyColor  = new Color(0.45f, 0.75f, 0.35f, 1f); // 80–89%
    private Color _sickColor     = new Color(0.85f, 0.65f, 0.15f, 1f); // 70–79%
    private Color _highRiskColor = new Color(0.75f, 0.25f, 0.25f, 1f); // <70%

    // Cache sprite redondeado
    private static Sprite sRoundedSpriteCache;
    private static int sCachedSize;
    private static int sCachedRadius;

    void Start()
{
    _playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

    InitializeAreaData();     // pinta algo (puede ser fallback si el manager aún no cargó)
    CreateFloatingCard();
    SetupAreaCollider();
    CreateConnection();

    _originalScale = _cardPanel.transform.localScale;
    _originalPosition = _cardPanel.transform.position;

    // <-- NUEVO: reintento tras 1-2 frames y refresco de UI
    StartCoroutine(BindFromManagerAndRefresh());
}

IEnumerator BindFromManagerAndRefresh()
{
    // espera 2 frames para dar tiempo a AreaManager.Start() e InitializeAreaData()
    yield return null;
    yield return null;

    var manager = FindFirstObjectByType<AreaManager>();
    if (manager == null) yield break;

    string raw = string.IsNullOrEmpty(areaName) ? gameObject.name : areaName;
    string key = NormalizeKey(raw);

    var data = manager.GetAreaData(key);
    if (data != null)
    {
        areaData = data;
        // refrescar UI ya creada
        if (_areaNameText != null)      _areaNameText.text = areaData.displayName.ToUpper();
        if (_overallResultText != null) _overallResultText.text = $"{areaData.overallResult:F0}%";
        if (_backgroundImage != null)   _backgroundImage.color = GetAreaColor(areaData.overallResult);
    }
}


    /// <summary>Permite mostrar/ocultar la tarjeta completa desde otros scripts.</summary>
    public void SetCardActive(bool active)
    {
        if (_cardCanvas != null) _cardCanvas.gameObject.SetActive(active);
        if (_pointerLine != null) _pointerLine.gameObject.SetActive(active);
    }

    // ------------------- Datos demo -------------------
    void InitializeAreaData()
    {
        // 1) Intentar datos reales del AreaManager (via AreaConfigSO/Bootstrapper)
        var manager = FindFirstObjectByType<AreaManager>();
        if (manager != null)
        {
            string raw = string.IsNullOrEmpty(areaName) ? gameObject.name : areaName;
            string key = NormalizeKey(raw);

            var data = manager.GetAreaData(key);   // AreaManager.AreaData
            if (data != null)
            {
                areaData = data;                   // PRIORIDAD 1 (SO)
                return;
            }
        }

        // 2) Fallback: tus valores hardcodeados (PRIORIDAD 2)
        string upper = (string.IsNullOrEmpty(areaName) ? gameObject.name : areaName).ToUpperInvariant();
        switch (upper)
        {
            case "AT HONDA":
            case "ATHONDA":
            case "AREA_ATHONDA":
                areaData = new AreaData { areaName = "ATHONDA", displayName = "AT HONDA", overallResult = 95f };
                break;

            case "VCT L4":
            case "VCTL4":
            case "AREA_VCTL4":
                areaData = new AreaData { areaName = "VCTL4", displayName = "VCT L4", overallResult = 92f };
                break;

            case "BUZZER L2":
            case "BUZZERL2":
            case "AREA_BUZZERL2":
                areaData = new AreaData { areaName = "BUZZERL2", displayName = "BUZZER L2", overallResult = 73f };
                break;

            case "VB L1":
            case "VBL1":
            case "AREA_VBL1":
                areaData = new AreaData { areaName = "VBL1", displayName = "VB L1", overallResult = 49f };
                break;

            default:
                string n = string.IsNullOrEmpty(areaName) ? gameObject.name : areaName;
                areaData = new AreaData { areaName = n, displayName = n, overallResult = 81f };
                break;
        }
    }

    // Helper para normalizar claves (ATHONDA, VCTL4, etc.)
    string NormalizeKey(string s)

    {
        string u = (s ?? "").ToUpperInvariant().Trim();
        if (u.StartsWith("AREA_")) u = u.Substring(5);
        u = u.Replace("_", "").Replace(" ", "");
        return u;

    }

    // ------------------- Tarjeta -------------------
    void CreateFloatingCard()
    {
        GameObject canvasObj = new GameObject($"Canvas_{areaData.areaName}");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = cardOffset;

        _cardCanvas = canvasObj.AddComponent<Canvas>();
        _cardCanvas.renderMode = RenderMode.WorldSpace;
        _cardCanvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        canvasRect.localScale = Vector3.one * 0.02f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel principal
        _cardPanel = new GameObject("CardPanel");
        _cardPanel.transform.SetParent(canvasObj.transform, false);
        var panelRT = _cardPanel.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(cardWidth, cardHeight);
        panelRT.anchoredPosition = Vector2.zero;

        _backgroundImage = _cardPanel.AddComponent<Image>();
        Sprite rounded = GetOrCreateRoundedSprite(roundedBaseSize, cornerRadiusPx);
        _backgroundImage.sprite = rounded;
        _backgroundImage.type = Image.Type.Sliced;
        var baseColor = GetAreaColor(areaData.overallResult);
        _backgroundImage.color = baseColor;
        AddWhiteOutline(_backgroundImage);

        // Título
        GameObject nameObj = new GameObject("AreaName");
        nameObj.transform.SetParent(_cardPanel.transform, false);
        var nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.4f);
        nameRT.anchoredPosition = new Vector2(0, cardHeight * 0.15f);

        _areaNameText = nameObj.AddComponent<Text>();
        _areaNameText.text = areaData.areaName.ToUpper();
        _areaNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _areaNameText.fontSize = 84;
        _areaNameText.fontStyle = FontStyle.Bold;
        _areaNameText.color = Color.white;
        _areaNameText.alignment = TextAnchor.MiddleCenter;
        _areaNameText.resizeTextForBestFit = true;
        _areaNameText.resizeTextMinSize = 26;
        _areaNameText.resizeTextMaxSize = 110;

        // Porcentaje
        GameObject resObj = new GameObject("OverallResult");
        resObj.transform.SetParent(_cardPanel.transform, false);
        var resRT = resObj.AddComponent<RectTransform>();
        resRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.55f);
        resRT.anchoredPosition = new Vector2(0, -cardHeight * 0.15f);

        _overallResultText = resObj.AddComponent<Text>();
        _overallResultText.text = $"{areaData.overallResult:F0}%";
        _overallResultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _overallResultText.fontSize = 190;
        _overallResultText.fontStyle = FontStyle.Bold;
        _overallResultText.color = Color.white;
        _overallResultText.alignment = TextAnchor.MiddleCenter;
        _overallResultText.resizeTextForBestFit = true;
        _overallResultText.resizeTextMinSize = 68;
        _overallResultText.resizeTextMaxSize = 240;

        StartCoroutine(LookAtCamera());
    }

    void SetupAreaCollider()
    {
        if (GetComponent<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(15f, 8f, 15f);
            box.center = new Vector3(0, 4f, 0);
        }
    }

    // ------------------- Conexión -------------------
    void CreateConnection()
    {
        GameObject go = new GameObject("BalloonPointer");
        go.transform.SetParent(transform, false);

        _pointerLine = go.AddComponent<LineRenderer>();
        _pointerLine.material = CreateLineMaterial();

        Color col = inheritCardColor && _backgroundImage != null ? _backgroundImage.color : connectionColor;
        col.a = connectionOpacity;
        _pointerLine.startColor = col;
        _pointerLine.endColor = col;

        _pointerLine.numCornerVertices = 8;
        _pointerLine.numCapVertices = 8;
        _pointerLine.textureMode = LineTextureMode.Stretch;
        _pointerLine.alignment = LineAlignment.View;
        _pointerLine.useWorldSpace = true;
        _pointerLine.positionCount = Mathf.Max(8, pointerSegments);

        var w = new AnimationCurve();
        w.AddKey(0f, connectionWidth);
        w.AddKey(0.85f, connectionWidth * 0.35f);
        w.AddKey(1f, 0.0f);
        _pointerLine.widthCurve = w;

        UpdateBalloonPointer();
    }

    void UpdateBalloonPointer()
    {
        if (_pointerLine == null || _cardPanel == null) return;

        // Puntos del bezier desde la base inferior de la tarjeta hacia el objeto (centro)
        Vector3 startWorld = GetCardBottomWorld();
        Vector3 endWorld = transform.position + Vector3.up * (pointerAttachInset * Mathf.Max(1f, transform.localScale.y));

        // Curva con ligera elevación y flexión
        Vector3 dir = (endWorld - startWorld);
        float len = dir.magnitude;
        if (len < 0.001f) len = 0.001f;
        Vector3 nrm = dir / len;

        Vector3 p0 = startWorld;
        Vector3 p3 = endWorld;
        Vector3 up = Vector3.up * (pointerCurveHeight * Mathf.Clamp(len, 0.25f, 8f));
        Vector3 bend = Vector3.Cross(nrm, Vector3.up) * (pointerBend * len);

        Vector3 p1 = p0 + up * 0.5f + bend * 0.25f;
        Vector3 p2 = p3 - up * 0.5f - bend * 0.25f;

        int segs = Mathf.Max(8, pointerSegments);
        _pointerLine.positionCount = segs;
        for (int i = 0; i < segs; i++)
        {
            float t = i / (segs - 1f);
            _pointerLine.SetPosition(i, CubicBezier(p0, p1, p2, p3, t));
        }

        // Color dinámico según fondo
        if (inheritCardColor && _backgroundImage != null)
        {
            var c = _backgroundImage.color; c.a = connectionOpacity;
            _pointerLine.startColor = c; _pointerLine.endColor = c;
        }
    }

    Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    Vector3 GetCardBottomWorld()
    {
        var rt = _cardPanel.GetComponent<RectTransform>();
        Vector3 local = new Vector3(0, -rt.rect.height * 0.5f, 0);
        return rt.TransformPoint(local);
    }

    Material CreateLineMaterial()
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.white;
        return mat;
    }

    void AddWhiteOutline(Image image)
    {
        if (image == null) return;
        var outline = image.GetComponent<Outline>();
        if (outline == null) outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(10.0f, 10.0f);  // Contorno más grueso Apple-style
        outline.useGraphicAlpha = false;
    }

    // Colores por KPI (consistente para Top-Down / Libre)
    Color GetAreaColor(float result)
    {
        Color baseCol;

        if (result >= 95f)      baseCol = _optimusColor;     // Verde oscuro
        else if (result >= 80f) baseCol = _healthyColor;     // Verde claro
        else if (result >= 70f) baseCol = _sickColor;        // Amarillo
        else                    baseCol = _highRiskColor;    // Rojo

        float a = _topDownEnabled ? 0.88f : 0.92f;           // sutil variación
        return new Color(baseCol.r, baseCol.g, baseCol.b, a);
    }

    IEnumerator LookAtCamera()
    {
        while (true)
        {
            if (_playerCamera != null && _cardCanvas != null)
            {
                if (_topDownEnabled)
                {
                    if (yawOnlyInTopDown)
                    {
                        // Vista superior limpia
                        _cardCanvas.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    else
                    {
                        // Billboard sólo yaw
                        Vector3 toCam = _playerCamera.transform.position - _cardCanvas.transform.position;
                        toCam.y = 0f;
                        if (toCam.sqrMagnitude > 0.0001f)
                            _cardCanvas.transform.rotation = Quaternion.LookRotation(-toCam);
                    }
                }
                else
                {
                    // Modo Libre
                    Vector3 dir = _playerCamera.transform.position - _cardCanvas.transform.position;
                    _cardCanvas.transform.rotation = Quaternion.LookRotation(-dir);
                }
            }
            yield return new WaitForSeconds(0.05f);
        }
    }
    // Refuerzo de billboard por frame (suaviza)
    void ApplyBillboardRotation()
    {
        if (_playerCamera == null || _cardCanvas == null) return;

        if (_topDownEnabled && yawOnlyInTopDown)
        {
            Vector3 toCam = _playerCamera.transform.position - _cardCanvas.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(-toCam.normalized);
                _cardCanvas.transform.rotation = Quaternion.Slerp(_cardCanvas.transform.rotation, target, Time.deltaTime * 8f);
            }
        }
        else
        {
            Vector3 dir = _playerCamera.transform.position - _cardCanvas.transform.position;
            Quaternion target = Quaternion.LookRotation(-dir.normalized);
            _cardCanvas.transform.rotation = Quaternion.Slerp(_cardCanvas.transform.rotation, target, Time.deltaTime * 8f);
        }
    }

    void Update()
    {
        // Billboard suave
        ApplyBillboardRotation();

        if (_cardPanel != null)
        {
            // Flotación sutil
            float floating = Mathf.Sin(Time.time * 1.5f) * floatAmplitude;
            Vector3 targetPos = _originalPosition + new Vector3(0, floating, 0);
            _cardPanel.transform.position = Vector3.Lerp(_cardPanel.transform.position, targetPos, Time.deltaTime * 3f);

            // Escala con hover; en Top-Down parte de una base mayor (1.35x)
            Vector3 baseScale = _topDownEnabled ? _originalScale * topDownScaleMultiplier : _originalScale;
            Vector3 targetScale = _isHovering ? baseScale * hoverScale : baseScale;
            _cardPanel.transform.localScale = Vector3.Lerp(_cardPanel.transform.localScale, targetScale, Time.deltaTime * animationSpeed);

            // Efecto hover (brillo sutil)
            if (_backgroundImage != null)
            {
                var baseColor = GetAreaColor(areaData.overallResult);
                var targetColor = _isHovering
                    ? new Color(baseColor.r * 1.08f, baseColor.g * 1.08f, baseColor.b * 1.08f, baseColor.a)
                    : baseColor;
                _backgroundImage.color = Color.Lerp(_backgroundImage.color, targetColor, Time.deltaTime * 12f);
            }
        }

        UpdateBalloonPointer();
    }

    // ------------------- Interacción -------------------
    public void OnPointerEnter(PointerEventData eventData) => _isHovering = true;
    public void OnPointerExit(PointerEventData eventData)  => _isHovering = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Notificar al AreaManager
        AreaManager mgr = FindFirstObjectByType<AreaManager>();
        if (mgr != null)
        {
            mgr.OnAreaClicked(this);
        }
    }

    // ------------------- API para Top-Down -------------------
    public void SetTopDownMode(bool enabled)
    {
        _topDownEnabled = enabled;

        // ¿Vista top-down fija?
        var topDownController = Camera.main?.GetComponent<TopDownCameraController>();
        bool isStaticView = topDownController != null &&
                            topDownController.IsUsingFixedStaticView() && enabled;

        // Ocultar tarjeta en vista estática para evitar jitter
        if (isStaticView)
        {
            if (_cardCanvas != null) _cardCanvas.gameObject.SetActive(false);
            if (_pointerLine != null) _pointerLine.gameObject.SetActive(false);

            Debug.Log($"AreaCard {areaData.areaName}: Ocultada en vista estática");
            return;
        }
        else
        {
            if (_cardCanvas != null) _cardCanvas.gameObject.SetActive(true);
            if (_pointerLine != null) _pointerLine.gameObject.SetActive(true);

            Debug.Log($"AreaCard {areaData.areaName}: Mostrada - no vista estática");
        }

        // Ajustes de escala y texto según modo
        if (_cardPanel != null)
        {
            Vector3 baseScale = enabled ? _originalScale * topDownScaleMultiplier : _originalScale;
            _cardPanel.transform.localScale = baseScale;
        }

        if (_areaNameText != null)
        {
            _areaNameText.resizeTextMaxSize = enabled ? 120 : 110;
            _areaNameText.fontSize = enabled ? 92 : 84;
        }
        if (_overallResultText != null)
        {
            _overallResultText.resizeTextMaxSize = enabled ? 260 : 240;
            _overallResultText.fontSize = enabled ? 210 : 190;
        }

        if (_backgroundImage != null)
        {
            var c = GetAreaColor(areaData.overallResult);
            _backgroundImage.color = c;
        }
    }

    // ------------------- Rounded 9-slice generator -------------------
    static Sprite GetOrCreateRoundedSprite(int texSize, int radiusPx)
    {
        if (sRoundedSpriteCache != null && sCachedSize == texSize && sCachedRadius == radiusPx)
            return sRoundedSpriteCache;

        texSize  = Mathf.Max(32, texSize);
        radiusPx = Mathf.Clamp(radiusPx, 2, texSize / 2);

        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32 opaque = new Color32(255, 255, 255, 255);
        Color32 clear  = new Color32(255, 255, 255, 0);

        int w = tex.width, h = tex.height, r = radiusPx;

        // Dibuja un rectángulo con esquinas redondeadas
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inCorner =
                    (x < r && y < r) || (x < r && y >= h - r) ||
                    (x >= w - r && y < r) || (x >= w - r && y >= h - r);

                if (!inCorner)
                {
                    tex.SetPixel(x, y, opaque);
                }
                else
                {
                    // Distancia al centro de la esquina correspondiente
                    Vector2 center =
                        (x < r && y < r) ? new Vector2(r, r) :
                        (x < r && y >= h - r) ? new Vector2(r, h - r - 1) :
                        (x >= w - r && y < r) ? new Vector2(w - r - 1, r) :
                        new Vector2(w - r - 1, h - r - 1);

                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    tex.SetPixel(x, y, dist <= r ? opaque : clear);
                }
            }
        }

        tex.Apply();

        // Sprite con 9-slice
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r) // border
        );

        sRoundedSpriteCache = sprite;
        sCachedSize = texSize;
        sCachedRadius = radiusPx;
        return sprite;
    }
}

