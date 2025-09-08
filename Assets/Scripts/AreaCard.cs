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
    [Range(2, 64)] public int cornerRadiusPx = 18;
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
    public float hoverScale = 1.05f;
    public float animationSpeed = 8f;
    public float floatAmplitude = 0.3f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isHovering = false;
    private Camera playerCamera;

    // Paleta por estado
    private Color optimusColor = new Color(0.20f, 0.80f, 0.30f, 1f);
    private Color healthyColor = new Color(0.10f, 0.60f, 0.90f, 1f);
    private Color sickColor = new Color(1.00f, 0.70f, 0.10f, 1f);
    private Color highRiskColor = new Color(0.95f, 0.30f, 0.30f, 1f);

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

    // ------------------- Datos demo -------------------
    void InitializeAreaData()
    {
        switch (areaName.ToUpper())
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
        backgroundImage.color = GetAreaColor(areaData.overallResult);

        // Título
        GameObject nameObj = new GameObject("AreaName");
        nameObj.transform.SetParent(cardPanel.transform, false);
        var nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.4f);
        nameRT.anchoredPosition = new Vector2(0, cardHeight * 0.15f);

        areaNameText = nameObj.AddComponent<Text>();
        areaNameText.text = areaData.areaName.ToUpper();
        areaNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        areaNameText.fontSize = 80;
        areaNameText.fontStyle = FontStyle.Bold;
        areaNameText.color = Color.white;
        areaNameText.alignment = TextAnchor.MiddleCenter;

        // Porcentaje
        GameObject resObj = new GameObject("OverallResult");
        resObj.transform.SetParent(cardPanel.transform, false);
        var resRT = resObj.AddComponent<RectTransform>();
        resRT.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.55f);
        resRT.anchoredPosition = new Vector2(0, -cardHeight * 0.15f);

        overallResultText = resObj.AddComponent<Text>();
        overallResultText.text = $"{areaData.overallResult:F0}%";
        overallResultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overallResultText.fontSize = 180;
        overallResultText.fontStyle = FontStyle.Bold;
        overallResultText.color = Color.white;
        overallResultText.alignment = TextAnchor.MiddleCenter;

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
        Vector3 startWorld = GetCardBottomWorld() - Vector3.up * pointerAttachInset;
        Vector3 endWorld = transform.position + Vector3.up * 0.15f;

        Vector3 forward = (startWorld - endWorld).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 c1 = startWorld + Vector3.down * (pointerCurveHeight * 0.35f) + side * (pointerBend * 0.6f);
        Vector3 c2 = Vector3.Lerp(startWorld, endWorld, 0.6f) + Vector3.down * pointerCurveHeight + side * (-pointerBend);

        int n = pointerLine.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t = i / (n - 1f);
            Vector3 p = CubicBezier(startWorld, c1, c2, endWorld, t);
            pointerLine.SetPosition(i, p);
        }
    }

    static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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

    Color GetAreaColor(float result)
    {
        if (result >= 90f) return optimusColor;
        else if (result >= 80f) return healthyColor;
        else if (result >= 70f) return sickColor;
        else return highRiskColor;
    }

    IEnumerator LookAtCamera()
    {
        while (true)
        {
            if (playerCamera != null && cardCanvas != null)
            {
                Vector3 dir = playerCamera.transform.position - cardCanvas.transform.position;
                cardCanvas.transform.rotation = Quaternion.LookRotation(-dir);
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    void Update()
    {
        if (cardPanel != null)
        {
            float floating = Mathf.Sin(Time.time * 1.5f) * floatAmplitude;
            Vector3 targetPos = originalPosition + new Vector3(0, floating, 0);
            cardPanel.transform.position = Vector3.Lerp(cardPanel.transform.position, targetPos, Time.deltaTime * 3f);

            Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;
            cardPanel.transform.localScale = Vector3.Lerp(cardPanel.transform.localScale, targetScale, Time.deltaTime * animationSpeed);
        }
        UpdateBalloonPointer();
    }

    // ------------------- Interacción -------------------
    public void OnPointerEnter(PointerEventData eventData) => isHovering = true;
    public void OnPointerExit(PointerEventData eventData) => isHovering = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        // 🔗 Al hacer click, notificar al AreaManager
        AreaManager mgr = FindFirstObjectByType<AreaManager>();
        if (mgr != null)
        {
            mgr.OnAreaClicked(this);
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
