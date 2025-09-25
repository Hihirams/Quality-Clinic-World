using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AreaCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Configuración del Área")]
    public string areaName;
    public AreaData areaData;

    [Header("Referencias UI (Generadas)")]
    private Canvas cardCanvas;
    private GameObject cardPanel;
    private Text areaNameText;
    private Text overallResultText;
    private Image backgroundImage;

    [Header("Dimensiones de tarjeta")]
    public float cardWidth = 900f;
    public float cardHeight = 450f;
    public Vector3 cardOffset = new Vector3(0, 25, 0);

    [Header("Esquinas redondeadas")]
    [Range(2, 64)] public int cornerRadiusPx = 32;  // Más redondeado Apple-style

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
    private LineRenderer pointerLine;

    [Header("Animación")]
    public float hoverScale = 1.30f;
    public float animationSpeed = 13f;
    public float floatAmplitude = 1.5f;

    [Header("Top-Down")]
    [Tooltip("Aumenta la escala para legibilidad en vista top-down.")]
    public float topDownScaleMultiplier = 1.35f; // ⬆️ 1.15f -> 1.35f
    [Tooltip("En Top-Down, rota sólo sobre Y para un billboard limpio.")]
    public bool yawOnlyInTopDown = true;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isHovering = false;
    private bool topDownEnabled = false;
    private Camera playerCamera;

// Apple-style theme colors (muted/soft for elegance)
private Color optimusColor   = new Color(0.15f, 0.65f, 0.25f, 1f);  // >=90% Dark green
private Color healthyColor   = new Color(0.45f, 0.75f, 0.35f, 1f);  // 70–89% Light green
private Color sickColor      = new Color(0.85f, 0.65f, 0.15f, 1f);  // 60–69% Yellow
private Color highRiskColor  = new Color(0.75f, 0.25f, 0.25f, 1f);  // <60% Red


    private static Sprite sRoundedSpriteCache;
    private static int sCachedSize;
    private static int sCachedRadius;

    void Start()
    {
        playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        InitializeAreaData();
        CreateFloatingCard();
        SetupAreaCollider();
        CreateConnection();

        originalScale = cardPanel.transform.localScale;
        originalPosition = cardPanel.transform.position;
    }

    // Permite mostrar/ocultar la tarjeta completa desde otros scripts (OverlayPainter)
    public void SetCardActive(bool active)
    {
        if (cardCanvas != null) cardCanvas.gameObject.SetActive(active);
        if (pointerLine != null) pointerLine.gameObject.SetActive(active);
    }


    // ------------------- Datos demo -------------------
    void InitializeAreaData()
    {
        switch ((areaName ?? "").ToUpper())
        {
            case "AT HONDA":
            case "ATHONDA":
            case "AREA_ATHONDA":
                areaData = new AreaData { areaName = "AT HONDA", overallResult = 95f };
                break;
            case "VCT L4":
            case "VCTL4":
            case "AREA_VCTL4":
                areaData = new AreaData { areaName = "VCT L4", overallResult = 92f };
                break;
            case "BUZZER L2":
            case "BUZZERL2":
            case "AREA_BUZZERL2":
                areaData = new AreaData { areaName = "BUZZER L2", overallResult = 73f };
                break;
            case "VB L1":
            case "VBL1":
            case "AREA_VBL1":
                areaData = new AreaData { areaName = "VB L1", overallResult = 49f };
                break;
            default:
                areaData = new AreaData { areaName = string.IsNullOrEmpty(areaName) ? "ÁREA" : areaName, overallResult = 81f };
                break;
        }
    }

    // ------------------- Tarjeta -------------------
    void CreateFloatingCard()
    {
        GameObject canvasObj = new GameObject($"Canvas_{areaData.areaName}");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = cardOffset;

        cardCanvas = canvasObj.AddComponent<Canvas>();
        cardCanvas.renderMode = RenderMode.WorldSpace;
        cardCanvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        canvasRect.localScale = Vector3.one * 0.02f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel principal
        cardPanel = new GameObject("CardPanel");
        cardPanel.transform.SetParent(canvasObj.transform, false);
        var panelRT = cardPanel.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(cardWidth, cardHeight);
        panelRT.anchoredPosition = Vector2.zero;

        backgroundImage = cardPanel.AddComponent<Image>();
        Sprite rounded = GetOrCreateRoundedSprite(roundedBaseSize, cornerRadiusPx);
        backgroundImage.sprite = rounded;
        backgroundImage.type = Image.Type.Sliced;
        var baseColor = GetAreaColor(areaData.overallResult);
        backgroundImage.color = baseColor;
        AddWhiteOutline(backgroundImage);


        // Título
        GameObject nameObj = new GameObject("AreaName");
        nameObj.transform.SetParent(cardPanel.transform, false);
        var nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.4f);
        nameRT.anchoredPosition = new Vector2(0, cardHeight * 0.15f);

        areaNameText = nameObj.AddComponent<Text>();
        areaNameText.text = areaData.areaName.ToUpper();
        areaNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        areaNameText.fontSize = 84;                 // ⬆️ un poco mayor base
        areaNameText.fontStyle = FontStyle.Bold;
        areaNameText.color = Color.white;
        areaNameText.alignment = TextAnchor.MiddleCenter;
        areaNameText.resizeTextForBestFit = true;
        areaNameText.resizeTextMinSize = 26;
        areaNameText.resizeTextMaxSize = 110;       // ⬆️ max para Top-Down

        // Porcentaje
        GameObject resObj = new GameObject("OverallResult");
        resObj.transform.SetParent(cardPanel.transform, false);
        var resRT = resObj.AddComponent<RectTransform>();
        resRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.55f);
        resRT.anchoredPosition = new Vector2(0, -cardHeight * 0.15f);

        overallResultText = resObj.AddComponent<Text>();
        overallResultText.text = $"{areaData.overallResult:F0}%";
        overallResultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overallResultText.fontSize = 190;           // ⬆️ base
        overallResultText.fontStyle = FontStyle.Bold;
        overallResultText.color = Color.white;
        overallResultText.alignment = TextAnchor.MiddleCenter;
        overallResultText.resizeTextForBestFit = true;
        overallResultText.resizeTextMinSize = 68;
        overallResultText.resizeTextMaxSize = 240;  // ⬆️ max para Top-Down

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

        pointerLine = go.AddComponent<LineRenderer>();
        pointerLine.material = CreateLineMaterial();

        Color col = inheritCardColor && backgroundImage != null ? backgroundImage.color : connectionColor;
        col.a = connectionOpacity;
        pointerLine.startColor = col;
        pointerLine.endColor = col;

        pointerLine.numCornerVertices = 8;
        pointerLine.numCapVertices = 8;
        pointerLine.textureMode = LineTextureMode.Stretch;
        pointerLine.alignment = LineAlignment.View;
        pointerLine.useWorldSpace = true;
        pointerLine.positionCount = Mathf.Max(8, pointerSegments);

        var w = new AnimationCurve();
        w.AddKey(0f, connectionWidth);
        w.AddKey(0.85f, connectionWidth * 0.35f);
        w.AddKey(1f, 0.0f);
        pointerLine.widthCurve = w;

        UpdateBalloonPointer();
    }

    void UpdateBalloonPointer()
    {
        if (pointerLine == null || cardPanel == null) return;

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
        pointerLine.positionCount = segs;
        for (int i = 0; i < segs; i++)
        {
            float t = i / (segs - 1f);
            pointerLine.SetPosition(i, CubicBezier(p0, p1, p2, p3, t));
        }

        // Color dinámico según fondo
        if (inheritCardColor && backgroundImage != null)
        {
            var c = backgroundImage.color; c.a = connectionOpacity;
            pointerLine.startColor = c; pointerLine.endColor = c;
        }
    }

    Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    Vector3 GetCardBottomWorld()
    {
        var rt = cardPanel.GetComponent<RectTransform>();
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

    // (4) Colores más contrastantes en Top-Down
// (4) Colores más contrastantes en Top-Down
Color GetAreaColor(float result)
{
    Color baseCol;

    if (result >= 95f)
        baseCol = optimusColor;     // Verde oscuro
    else if (result >= 80f)
        baseCol = healthyColor;     // Verde claro
    else if (result >= 70f)
        baseCol = sickColor;        // Amarillo
    else
        baseCol = highRiskColor;    // Rojo

    // Apple-style: consistencia cromática; solo variamos alpha muy sutil
    float a = topDownEnabled ? 0.88f : 0.92f;
    return new Color(baseCol.r, baseCol.g, baseCol.b, a);
}



    IEnumerator LookAtCamera()
    {
        while (true)
        {
            if (playerCamera != null && cardCanvas != null)
            {
                if (topDownEnabled)
                {
                    if (yawOnlyInTopDown)
                    {
                        // --- ROTACIÓN PLANA (vista desde arriba) ---
                        // Siempre orientar el canvas hacia arriba y sin inclinación
                        cardCanvas.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    else
                    {
                        // Billboard clásico pero con solo yaw
                        Vector3 toCam = playerCamera.transform.position - cardCanvas.transform.position;
                        toCam.y = 0f; // sólo giro sobre Y
                        if (toCam.sqrMagnitude > 0.0001f)
                            cardCanvas.transform.rotation = Quaternion.LookRotation(-toCam);
                    }
                }
                else
                {
                    // --- MODO LIBRE normal ---
                    Vector3 dir = playerCamera.transform.position - cardCanvas.transform.position;
                    cardCanvas.transform.rotation = Quaternion.LookRotation(-dir);
                }
            }
            yield return new WaitForSeconds(0.05f);
        }
    }


    // (2) Billboard más robusto: también lo aplicamos desde Update para vistas estáticas o si el coroutine se queda desfasado
    void ApplyBillboardRotation()
    {
        if (playerCamera == null || cardCanvas == null) return;

        if (topDownEnabled && yawOnlyInTopDown)
        {
            // Proyectar la dirección a la cámara en el plano XZ para giro solo en Y
            Vector3 toCam = playerCamera.transform.position - cardCanvas.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(-toCam.normalized);
                // slerp suave para evitar jitter cuando la cámara está casi estática
                cardCanvas.transform.rotation = Quaternion.Slerp(cardCanvas.transform.rotation, target, Time.deltaTime * 8f);
            }
        }
        else
        {
            Vector3 dir = playerCamera.transform.position - cardCanvas.transform.position;
            Quaternion target = Quaternion.LookRotation(-dir.normalized);
            cardCanvas.transform.rotation = Quaternion.Slerp(cardCanvas.transform.rotation, target, Time.deltaTime * 8f);
        }
    }

    void Update()
    {
        // (2) Refuerzo del billboard en cada frame — útil en vista estática Top-Down
        ApplyBillboardRotation();

        if (cardPanel != null)
        {
            // Flotación sutil
            float floating = Mathf.Sin(Time.time * 1.5f) * floatAmplitude;
            Vector3 targetPos = originalPosition + new Vector3(0, floating, 0);
            cardPanel.transform.position = Vector3.Lerp(cardPanel.transform.position, targetPos, Time.deltaTime * 3f);

            // Escala con hover; en Top-Down parte de una base mayor (1.35x)
            Vector3 baseScale = topDownEnabled ? originalScale * topDownScaleMultiplier : originalScale;
            Vector3 targetScale = isHovering ? baseScale * hoverScale : baseScale;
            cardPanel.transform.localScale = Vector3.Lerp(cardPanel.transform.localScale, targetScale, Time.deltaTime * animationSpeed);

            // Apple-style hover effect - subtle brightness change
        if (backgroundImage != null)
        {
        var baseColor = GetAreaColor(areaData.overallResult);
        var targetColor = isHovering
            ? new Color(baseColor.r * 1.08f, baseColor.g * 1.08f, baseColor.b * 1.08f, baseColor.a)
        : baseColor;
        backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, Time.deltaTime * 12f);
        }

        }

        UpdateBalloonPointer();
    }

    // ------------------- Interacción -------------------
    public void OnPointerEnter(PointerEventData eventData) => isHovering = true;
    public void OnPointerExit(PointerEventData eventData) => isHovering = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        // 🔗 Al hacer click, notificar al AreaManager (usa la firma que espera tu manager)
        AreaManager mgr = FindFirstObjectByType<AreaManager>();
        if (mgr != null)
        {
            mgr.OnAreaClicked(this);
        }
    }

    // ------------------- API para Top-Down -------------------
    // REEMPLAZADO COMPLETAMENTE SEGÚN TU INDICACIÓN:
    // ------------------- API para Top-Down -------------------
    // REEMPLAZADO COMPLETAMENTE SEGÚN TU INDICACIÓN:
    public void SetTopDownMode(bool enabled)
    {
        topDownEnabled = enabled;

        // Verificar si estamos usando vista fija estática
        var topDownController = Camera.main?.GetComponent<TopDownCameraController>();
        bool isStaticView = topDownController != null &&
                            topDownController.IsUsingFixedStaticView() && enabled;

        // Ocultar completamente las tarjetas en vista estática para evitar temblores
        if (isStaticView)
        {
            if (cardCanvas != null) cardCanvas.gameObject.SetActive(false);
            if (pointerLine != null) pointerLine.gameObject.SetActive(false);

            Debug.Log($"AreaCard {areaData.areaName}: Ocultada en vista estática");
            return;
        }
        else
        {
            // Mostrar tarjetas cuando no estemos en vista estática
            if (cardCanvas != null) cardCanvas.gameObject.SetActive(true);
            if (pointerLine != null) pointerLine.gameObject.SetActive(true);

            Debug.Log($"AreaCard {areaData.areaName}: Mostrada - no vista estática");
        }

        // Resto del código existente para vista top-down dinámica
        if (cardPanel != null)
        {
            Vector3 baseScale = enabled ? originalScale * topDownScaleMultiplier : originalScale;
            cardPanel.transform.localScale = baseScale;
        }

        if (areaNameText != null)
        {
            areaNameText.resizeTextMaxSize = enabled ? 120 : 110;
            areaNameText.fontSize = enabled ? 92 : 84;
        }
        if (overallResultText != null)
        {
            overallResultText.resizeTextMaxSize = enabled ? 260 : 240;
            overallResultText.fontSize = enabled ? 210 : 190;
        }

        if (backgroundImage != null)
        {
            var c = GetAreaColor(areaData.overallResult);
            backgroundImage.color = c;
        }
    }

    // ------------------- Rounded 9-slice generator -------------------
    static Sprite GetOrCreateRoundedSprite(int texSize, int radiusPx)
    {
        if (sRoundedSpriteCache != null && sCachedSize == texSize && sCachedRadius == radiusPx)
            return sRoundedSpriteCache;

        texSize = Mathf.Max(32, texSize);
        radiusPx = Mathf.Clamp(radiusPx, 2, texSize / 2);

        var tex = new Texture2D(texSize, texSize, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32 opaque = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(255, 255, 255, 0);

        int w = tex.width, h = tex.height, r = radiusPx;
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
                    int cx = (x < r) ? r - 1 : (x >= w - r ? w - r : x);
                    int cy = (y < r) ? r - 1 : (y >= h - r ? h - r : y);

                    if (x < r && y < r) { cx = r - 1; cy = r - 1; }
                    else if (x < r && y >= h - r) { cx = r - 1; cy = h - r; }
                    else if (x >= w - r && y < r) { cx = w - r; cy = r - 1; }
                    else if (x >= w - r && y >= h - r) { cx = w - r; cy = h - r; }

                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= (r - 1) * (r - 1))
                        tex.SetPixel(x, y, opaque);
                    else
                        tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();

        Vector4 border = new Vector4(radiusPx, radiusPx, radiusPx, radiusPx);
        var sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

        sRoundedSpriteCache = sprite;
        sCachedSize = texSize;
        sCachedRadius = radiusPx;
        return sprite;
    }
}

// ------------------- Datos mínimos -------------------
[System.Serializable]
public class AreaData
{
    public string areaName;
    public float overallResult;
}
