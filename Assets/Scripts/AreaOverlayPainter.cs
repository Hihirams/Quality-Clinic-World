using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// Pinta overlays de áreas en Top-Down.
/// Modo exacto: duplica los Meshes de cada hijo del área (sin tocar el original)
/// y aplica un material Unlit/transparente por encima para que la forma sea perfecta.
public class AreaOverlayPainter : MonoBehaviour
{
    [Header("Overlay Settings")]
    [SerializeField] private bool useExactMeshOverlay = true; // ← forma exacta por defecto
    [SerializeField] private Material overlayMaterial;        // Unlit/Transparent (URP o Built-in)
    [SerializeField] private float overlayHeight = 0.02f;     // Altura para evitar z-fighting
    [SerializeField] private float padding = 0f;              // Solo usado en modo Quad

    [Header("Text Settings")]
    [SerializeField] private Font textFont;
    [SerializeField] private int nameFontSize = 84;
    [SerializeField] private int percentFontSize = 160;
    [SerializeField] private Color textColor = Color.black; // ⚫ CAMBIADO A NEGRO
    [SerializeField] private float canvasScale = 0.003f;
    [SerializeField] private bool debugMode = true;

    [Header("Text Auto-Size")]
    [SerializeField] private bool autoSizeTexts = true;
    [SerializeField, Range(0.20f, 0.80f)] private float nameBand = 0.55f;
    [SerializeField] private int minNameFont = 250;
    [SerializeField] private int minPercentFont = 360;
    [SerializeField] private int maxFont = 1100;
    [SerializeField] private float globalTextBoost = 1.35f;

    [Header("Text Elevation")]
    [SerializeField] private float textLiftStatic = 3f;
    [SerializeField] private float textLiftDynamic = 5f;

    [Header("References")]
    [SerializeField] private AreaManager areaManager;
    [SerializeField] private IndustrialDashboard industrialDashboard;

    private readonly Dictionary<GameObject, AreaOverlayData> areaOverlays = new Dictionary<GameObject, AreaOverlayData>();
    private bool isTopDownMode = false;

    [System.Serializable]
    public class AreaOverlayData
    {
        public List<GameObject> overlayMeshes = new List<GameObject>();
        public GameObject overlayQuad;
        public Canvas textCanvas;
        public Text nameText;
        public Text percentageText;
        public Collider clickCollider;
        public AreaCard areaCard;
    }

    void Start()
    {
        if (areaManager == null) areaManager = FindObjectOfType<AreaManager>();
        if (industrialDashboard == null) industrialDashboard = FindObjectOfType<IndustrialDashboard>();

        InitializeOverlays();
        SetOverlaysActive(false);
    }

    public void SetTopDownMode(bool isTopDown)
    {
        if (debugMode)
            Debug.Log($"[AreaOverlayPainter] SetTopDownMode llamado con: {isTopDown}");

        isTopDownMode = isTopDown;
        SetOverlaysActive(isTopDownMode);

        if (isTopDownMode)
        {
            RefreshOverlaysForStaticView();
            StartCoroutine(RefreshCanvasesDelayed());
        }
    }

    private void RefreshOverlaysForStaticView()
    {
        foreach (var kv in areaOverlays)
        {
            var area = kv.Key;
            var od = kv.Value;

            if (od.textCanvas != null)
            {
                od.textCanvas.gameObject.SetActive(true);
                od.textCanvas.enabled = true;
                ConfigureCanvasForTopDown(od, area);
            }
            UpdateOverlayContent(od, area);
        }
    }

    private void ConfigureCanvasForTopDown(AreaOverlayData od, GameObject area)
    {
        if (od.textCanvas == null) return;

        var topDownController = Camera.main != null ? Camera.main.GetComponent<TopDownCameraController>() : null;
        bool isStaticView = topDownController != null && topDownController.IsUsingFixedStaticView();

        Bounds b = CalculateAreaBounds(area);

        if (isStaticView)
        {
            od.textCanvas.transform.position = new Vector3(b.center.x, b.max.y + textLiftStatic, b.center.z);
            od.textCanvas.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            od.textCanvas.transform.localScale = Vector3.one * canvasScale;
        }
        else
        {
            od.textCanvas.transform.position = new Vector3(b.center.x, b.max.y + textLiftDynamic, b.center.z);
            od.textCanvas.transform.rotation = Quaternion.identity;
            od.textCanvas.transform.localScale = Vector3.one * canvasScale;
        }

        od.textCanvas.renderMode = RenderMode.WorldSpace;
        od.textCanvas.sortingOrder = 300;

        RectTransform canvasRT = od.textCanvas.GetComponent<RectTransform>();
        float canvasWidth = Mathf.Max(b.size.x / canvasScale, 3000f);
        float canvasHeight = Mathf.Max(b.size.z / canvasScale, 2000f);
        canvasRT.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        AutoSizeFonts(od);

        if (debugMode)
        {
            Debug.Log($"[TopDown] Canvas reconfigurado para {area.name}: Pos: {od.textCanvas.transform.position}, Rot: {od.textCanvas.transform.rotation.eulerAngles}, Scale: {od.textCanvas.transform.localScale}, Size: {canvasRT.sizeDelta}, Static: {isStaticView}");
        }
    }

    private System.Collections.IEnumerator RefreshCanvasesDelayed()
    {
        yield return null;
        yield return null;

        foreach (var od in areaOverlays.Values)
        {
            if (od.textCanvas != null)
            {
                od.textCanvas.enabled = false;
                yield return null;
                od.textCanvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                AutoSizeFonts(od);
                if (debugMode) Debug.Log($"[Delayed] Canvas reactivado: {od.textCanvas.name}");
            }
        }
    }

    void InitializeOverlays()
    {
        var areas = GetAreasFromManager();
        foreach (var area in areas)
        {
            if (area == null) continue;
            CreateOverlayForArea(area);
        }
    }

    List<GameObject> GetAreasFromManager()
    {
        return areaManager != null ? (areaManager.GetAreaObjects() ?? new List<GameObject>()) : new List<GameObject>();
    }

    void CreateOverlayForArea(GameObject area)
    {
        var od = new AreaOverlayData();
        od.areaCard = area.GetComponentInChildren<AreaCard>(true);

        if (useExactMeshOverlay)
            BuildExactMeshOverlays(area, od);
        else
            BuildQuadOverlay(area, od);

        BuildTextCanvas(area, od);
        SetupClickOnMain(od, area);

        UpdateOverlayContent(od, area);
        areaOverlays[area] = od;
    }

    void BuildExactMeshOverlays(GameObject area, AreaOverlayData od)
    {
        var meshFilters = area.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            var srcRenderer = mf.GetComponent<MeshRenderer>();
            if (srcRenderer == null) continue;

            var ov = new GameObject("OverlayMesh_" + mf.name);
            ov.transform.SetParent(mf.transform, false);
            ov.transform.localPosition += Vector3.up * overlayHeight;
            ov.transform.localRotation = Quaternion.identity;
            ov.transform.localScale = Vector3.one;

            var newMF = ov.AddComponent<MeshFilter>();
            newMF.sharedMesh = mf.sharedMesh;

            var newMR = ov.AddComponent<MeshRenderer>();
            var mat = overlayMaterial != null ? new Material(overlayMaterial) : new Material(Shader.Find("Unlit/Color"));
            if (!mat.HasProperty("_Color")) mat.color = new Color(1, 1, 1, 0.3f);
            newMR.sharedMaterial = mat;

            newMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            newMR.receiveShadows = false;
            newMR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            newMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            newMR.allowOcclusionWhenDynamic = false;

            od.overlayMeshes.Add(ov);
        }
    }

    void BuildQuadOverlay(GameObject area, AreaOverlayData od)
    {
        Bounds b = CalculateAreaBounds(area);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Overlay_{area.name}";
        quad.transform.SetParent(area.transform, false);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.position = new Vector3(b.center.x, b.min.y + overlayHeight, b.center.z);
        quad.transform.localScale = new Vector3(b.size.x + padding, b.size.z + padding, 1f);
        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = overlayMaterial != null ? new Material(overlayMaterial) : new Material(Shader.Find("Unlit/Color"));
        od.overlayQuad = quad;
    }

    Bounds CalculateAreaBounds(GameObject area)
    {
        var renderers = area.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(area.transform.position, Vector3.zero);
        bool init = false;
        foreach (var r in renderers)
        {
            if (!init) { b = r.bounds; init = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!init) b = new Bounds(area.transform.position, new Vector3(1, 0.1f, 1));
        return b;
    }

    void BuildTextCanvas(GameObject area, AreaOverlayData od)
    {
        Bounds b = CalculateAreaBounds(area);
        GameObject canvasObj = new GameObject("OverlayCanvas");
        canvasObj.transform.SetParent(area.transform, false);

        float canvasY = b.max.y + textLiftStatic;
        canvasObj.transform.position = new Vector3(b.center.x, canvasY, b.center.z);
        canvasObj.transform.rotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * canvasScale;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRT = canvasObj.GetComponent<RectTransform>();
        float canvasWidth = Mathf.Max(b.size.x / canvasScale, 3000f);
        float canvasHeight = Mathf.Max(b.size.z / canvasScale, 2000f);
        canvasRT.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        // TEXTO DEL NOMBRE
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(canvasObj.transform, false);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = Mathf.Max(nameFontSize, 84);
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = textColor; // Usa el color negro definido arriba
        nameText.raycastTarget = false;

        // ✨ AÑADIDO: Contorno para el nombre
        Outline nameOutline = nameObj.AddComponent<Outline>();
        nameOutline.effectColor = new Color(1f, 1f, 1f, 0.8f); // Contorno blanco
        nameOutline.effectDistance = new Vector2(12, -12);      // Grosor del contorno

        RectTransform nameRT = nameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f - nameBand);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(100, 40);
        nameRT.offsetMax = new Vector2(-100, -40);

        // TEXTO DEL PORCENTAJE
        GameObject pctObj = new GameObject("PercentageText");
        pctObj.transform.SetParent(canvasObj.transform, false);
        Text pctText = pctObj.AddComponent<Text>();
        pctText.font = nameText.font;
        pctText.fontSize = Mathf.Max(percentFontSize, 300);
        pctText.fontStyle = FontStyle.Bold;
        pctText.alignment = TextAnchor.MiddleCenter;
        pctText.color = textColor; // Usa el color negro
        pctText.raycastTarget = false;

        // ✨ AÑADIDO: Contorno para el porcentaje
        Outline pctOutline = pctObj.AddComponent<Outline>();
        pctOutline.effectColor = nameOutline.effectColor;   // Mismo color
        pctOutline.effectDistance = new Vector2(20, -20);    // Más grueso para el número

        RectTransform pctRT = pctText.GetComponent<RectTransform>();
        pctRT.anchorMin = new Vector2(0f, 0f);
        pctRT.anchorMax = new Vector2(1f, 1f - nameBand);
        pctRT.offsetMin = new Vector2(100, 40);
        pctRT.offsetMax = new Vector2(-100, -40);

        od.textCanvas = canvas;
        od.nameText = nameText;
        od.percentageText = pctText;
        AutoSizeFonts(od);
    }

    // El resto de tu script (SetupClickOnMain, NormalizeKey, etc.) permanece igual.
    // ... (copia y pega el resto de tu script original aquí)
    // ...
    #region RestoDelCodigo
    void SetupClickOnMain(AreaOverlayData od, GameObject area)
    {
        GameObject clickGO = null;
        if (useExactMeshOverlay && od.overlayMeshes.Count > 0) clickGO = od.overlayMeshes[0];
        else if (od.overlayQuad != null) clickGO = od.overlayQuad;
        else clickGO = area;

        if (clickGO != null)
        {
            var col = clickGO.GetComponent<Collider>();
            if (col == null) col = clickGO.AddComponent<BoxCollider>();

            var click = clickGO.GetComponent<AreaOverlayClick>();
            if (click == null) click = clickGO.AddComponent<AreaOverlayClick>();
            click.Initialize(area, this);

            od.clickCollider = col;
        }
    }

    string NormalizeKey(string objectName)
    {
        string u = (objectName ?? "").ToUpper();
        if (u.StartsWith("AREA_")) u = u.Substring(5);
        return u.Replace('_', ' ').Trim();
    }

    public class OverlayInfo { public string display; public float overall; public Color color; }

    OverlayInfo GetInfoFromManager(GameObject area)
    {
        if (areaManager == null) return null;
        string key = NormalizeKey(area.name);

        var data = areaManager.GetAreaData(key);
        if (data == null) return null;

        return new OverlayInfo
        {
            display = string.IsNullOrEmpty(data.displayName) ? key : data.displayName,
            overall = data.overallResult,
            color = data.statusColor
        };
    }

    void UpdateOverlayContent(AreaOverlayData od, GameObject area)
    {
        var info = GetInfoFromManager(area);
        string display = info != null ? info.display : NormalizeKey(area.name);
        float overall = info != null ? info.overall : 0f;
        Color c = info != null ? info.color : new Color(1, 1, 1, 0.3f);

        if (debugMode)
        {
            Debug.Log($"[AreaOverlay] Actualizando {area.name}: display='{display}', overall={overall}%, color={c}");
        }

        if (useExactMeshOverlay)
        {
            foreach (var ov in od.overlayMeshes)
            {
                if (ov == null) continue;
                var mr = ov.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    if (mr.sharedMaterial.HasProperty("_Color"))
                    {
                        var cc = c; cc.a = 0.35f;
                        mr.sharedMaterial.color = cc;
                    }
                }
            }
        }
        else if (od.overlayQuad != null)
        {
            var mr = od.overlayQuad.GetComponent<Renderer>();
            if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_Color"))
            {
                var cc = c; cc.a = 0.35f;
                mr.sharedMaterial.color = cc;
            }
        }

        if (od.nameText)
        {
            string nameToShow = display.ToUpper();
            od.nameText.text = nameToShow;
            od.nameText.enabled = true;
            od.nameText.gameObject.SetActive(true);
        }

        if (od.percentageText)
        {
            string pctToShow = $"{overall:F0}%";
            od.percentageText.text = pctToShow;
            od.percentageText.enabled = true;
            od.percentageText.gameObject.SetActive(true);
        }

        if (od.textCanvas != null)
        {
            od.textCanvas.gameObject.SetActive(true);
            od.textCanvas.enabled = true;
            Canvas.ForceUpdateCanvases();
            AutoSizeFonts(od);
        }
    }

    public void RefreshOverlays()
    {
        foreach (var kv in areaOverlays)
        {
            UpdateOverlayContent(kv.Value, kv.Key);
        }
    }

    void SetOverlaysActive(bool active)
    {
        if (debugMode)
            Debug.Log($"[AreaOverlayPainter] SetOverlaysActive: {active}");

        foreach (var od in areaOverlays.Values)
        {
            if (useExactMeshOverlay)
            {
                foreach (var ov in od.overlayMeshes)
                    if (ov) ov.SetActive(active);
            }
            if (od.overlayQuad) od.overlayQuad.SetActive(active);

            if (od.textCanvas)
            {
                od.textCanvas.gameObject.SetActive(active);
                if (active && isTopDownMode)
                {
                    od.textCanvas.enabled = true;

                    if (od.nameText)
                    {
                        od.nameText.gameObject.SetActive(true);
                        od.nameText.enabled = true;
                    }
                    if (od.percentageText)
                    {
                        od.percentageText.gameObject.SetActive(true);
                        od.percentageText.enabled = true;
                    }
                }
            }
        }
    }

    void OnDestroy()
    {
        foreach (var od in areaOverlays.Values)
        {
            foreach (var ov in od.overlayMeshes) if (ov) DestroyImmediate(ov);
            if (od.overlayQuad) DestroyImmediate(od.overlayQuad);
            if (od.textCanvas) DestroyImmediate(od.textCanvas.gameObject);
        }
        areaOverlays.Clear();
    }

    public void HandleAreaClick(GameObject area)
    {
        if (areaManager != null)
            areaManager.OnAreaClicked(area);
    }

    void LayoutTextRects(RectTransform canvasRT, RectTransform nameRT, RectTransform pctRT)
    {
        float split = Mathf.Clamp01(nameBand);
        nameRT.anchorMin = new Vector2(0f, 1f - split);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.anchoredPosition = Vector2.zero;
        nameRT.sizeDelta = Vector2.zero;
        nameRT.offsetMin = new Vector2(100, 40);
        nameRT.offsetMax = new Vector2(-100, -40);

        pctRT.anchorMin = new Vector2(0f, 0f);
        pctRT.anchorMax = new Vector2(1f, 1f - split);
        pctRT.anchoredPosition = Vector2.zero;
        pctRT.sizeDelta = Vector2.zero;
        pctRT.offsetMin = new Vector2(100, 40);
        pctRT.offsetMax = new Vector2(-100, -40);
    }

    void AutoSizeFonts(AreaOverlayData od)
    {
        if (!autoSizeTexts || od == null || od.textCanvas == null || od.nameText == null || od.percentageText == null) return;

        RectTransform canvasRT = od.textCanvas.GetComponent<RectTransform>();
        RectTransform nameRT = od.nameText.rectTransform;
        RectTransform pctRT = od.percentageText.rectTransform;

        LayoutTextRects(canvasRT, nameRT, pctRT);

        float totalH = canvasRT.sizeDelta.y;
        float nameH = Mathf.Max((nameRT.anchorMax.y - nameRT.anchorMin.y) * totalH - 80f, 50f);
        float pctH = Mathf.Max((pctRT.anchorMax.y - pctRT.anchorMin.y) * totalH - 80f, 50f);

        int nameSize = Mathf.Clamp(Mathf.RoundToInt(nameH * 0.60f * globalTextBoost), minNameFont, maxFont);
        int pctSize = Mathf.Clamp(Mathf.RoundToInt(pctH * 0.75f * globalTextBoost), minPercentFont, maxFont);

        if (od.nameText.fontSize != nameSize) od.nameText.fontSize = nameSize;
        if (od.percentageText.fontSize != pctSize) od.percentageText.fontSize = pctSize;

        if (debugMode)
            Debug.Log($"[AutoSize] {od.textCanvas.name} -> name:{nameSize}px  pct:{pctSize}px  (totalH:{totalH})");
    }

    [ContextMenu("Debug Canvas Info")]
    void DebugCanvasInfo()
    {
        foreach (var kv in areaOverlays)
        {
            var area = kv.Key;
            var od = kv.Value;
            Debug.Log($"Área: {area.name}");
            Debug.Log($"  Canvas activo: {od.textCanvas != null && od.textCanvas.gameObject.activeInHierarchy}");
            Debug.Log($"  Canvas enabled: {od.textCanvas != null && od.textCanvas.enabled}");
            Debug.Log($"  Texto nombre activo: {od.nameText != null && od.nameText.gameObject.activeInHierarchy}");
            Debug.Log($"  Texto porcentaje activo: {od.percentageText != null && od.percentageText.gameObject.activeInHierarchy}");
            if (od.textCanvas != null)
            {
                Debug.Log($"  Canvas posición: {od.textCanvas.transform.position}");
                Debug.Log($"  Canvas rotación: {od.textCanvas.transform.rotation.eulerAngles}");
                Debug.Log($"  Canvas escala: {od.textCanvas.transform.localScale}");
                Debug.Log($"  Canvas size: {od.textCanvas.GetComponent<RectTransform>().sizeDelta}");
            }
        }
    }

    [ContextMenu("Force Show Overlays")]
    void ForceShowOverlays()
    {
        SetTopDownMode(true);
        RefreshOverlaysForStaticView();
    }
    #endregion
}

public class AreaOverlayClick : MonoBehaviour
{
    private GameObject targetArea;
    private AreaOverlayPainter overlayPainter;

    public void Initialize(GameObject area, AreaOverlayPainter painter)
    {
        targetArea = area;
        overlayPainter = painter;
    }

    void OnMouseDown()
    {
        if (overlayPainter != null && targetArea != null)
            overlayPainter.HandleAreaClick(targetArea);
    }
}