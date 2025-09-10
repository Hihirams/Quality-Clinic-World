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
    [SerializeField] private int nameFontSize = 48;
    [SerializeField] private int percentFontSize = 80;
    [SerializeField] private Color textColor = Color.white;

    [Header("References")]
    [SerializeField] private AreaManager areaManager;
    [SerializeField] private IndustrialDashboard industrialDashboard;

    private readonly Dictionary<GameObject, AreaOverlayData> areaOverlays = new Dictionary<GameObject, AreaOverlayData>();
    private bool isTopDownMode = false;

    [System.Serializable]
    public class AreaOverlayData
    {
        public List<GameObject> overlayMeshes = new List<GameObject>(); // exact mesh clones
        public GameObject overlayQuad;                                   // fallback quad
        public Canvas textCanvas;
        public Text nameText;
        public Text percentageText;
        public Collider clickCollider;
        public AreaCard areaCard;
    }

    void Start()
    {
        if (areaManager == null) areaManager = FindFirstObjectByType<AreaManager>();
        if (industrialDashboard == null) industrialDashboard = FindFirstObjectByType<IndustrialDashboard>();

        InitializeOverlays();
        SetOverlaysActive(false); // empezamos ocultos (modo libre)
    }

    // Llamado desde el AreaManager / TopDownCamera al entrar/salir de Top-Down
    public void SetTopDownMode(bool isTopDown)
    {
        isTopDownMode = isTopDown;
        SetOverlaysActive(isTopDownMode);
        if (isTopDownMode) RefreshOverlays();
    }

    // ===================== Construcción =====================
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
        // Usa el manager como fuente de verdad
        return areaManager != null ? (areaManager.GetAreaObjects() ?? new List<GameObject>()) : new List<GameObject>();
    }

    void CreateOverlayForArea(GameObject area)
    {
        var od = new AreaOverlayData();
        od.areaCard = area.GetComponentInChildren<AreaCard>(true);

        if (useExactMeshOverlay)
            BuildExactMeshOverlays(area, od);
        else
            BuildQuadOverlay(area, od); // fallback

        BuildTextCanvas(area, od);
        SetupClickOnMain(od, area);

        // primer fill de color/texto
        UpdateOverlayContent(od, area);

        areaOverlays[area] = od;
    }

    // ====== Opción 1: Forma exacta (clonar meshes hijos) ======
    void BuildExactMeshOverlays(GameObject area, AreaOverlayData od)
    {
        var meshFilters = area.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            var srcRenderer = mf.GetComponent<MeshRenderer>();
            if (srcRenderer == null) continue;

            // Crea un hijo pegado al mismo transform local
            var ov = new GameObject("OverlayMesh_" + mf.name);
            ov.transform.SetParent(mf.transform, false);
            ov.transform.localPosition += Vector3.up * overlayHeight; // lo elevamos un pelín
            ov.transform.localRotation = Quaternion.identity;
            ov.transform.localScale = Vector3.one;

            var newMF = ov.AddComponent<MeshFilter>();
            newMF.sharedMesh = mf.sharedMesh;

            var newMR = ov.AddComponent<MeshRenderer>();
            var mat = overlayMaterial != null ? new Material(overlayMaterial) : new Material(Shader.Find("Unlit/Color"));
            if (!mat.HasProperty("_Color")) mat.color = new Color(1, 1, 1, 0.3f);
            newMR.sharedMaterial = mat;

            // sin sombras ni light probes
            newMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            newMR.receiveShadows = false;
            newMR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            newMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            newMR.allowOcclusionWhenDynamic = false;

            od.overlayMeshes.Add(ov);
        }
    }

    // ====== Opción 2: Quad por bounds (rectangular) ======
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

    // ====== Textos ======
    void BuildTextCanvas(GameObject area, AreaOverlayData od)
    {
        Bounds b = CalculateAreaBounds(area);
        GameObject canvasObj = new GameObject("OverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(area.transform, false);
        canvasObj.transform.position = new Vector3(b.center.x, b.min.y + overlayHeight * 1.1f, b.center.z);
        canvasObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        canvasObj.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5000;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(b.size.x, b.size.z);

        // Nombre
        GameObject nameObj = new GameObject("NameText", typeof(Text));
        nameObj.transform.SetParent(canvasObj.transform, false);
        od.nameText = nameObj.GetComponent<Text>();
        var nameRT = od.nameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.55f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
        od.nameText.font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        od.nameText.fontSize = nameFontSize;
        od.nameText.alignment = TextAnchor.MiddleCenter;
        od.nameText.color = textColor;
        od.nameText.resizeTextForBestFit = true;
        od.nameText.resizeTextMinSize = Mathf.Max(12, nameFontSize / 2);
        od.nameText.resizeTextMaxSize = nameFontSize;
        // 🔒 Que no bloquee clicks:
        od.nameText.raycastTarget = false;

        // Porcentaje
        GameObject pctObj = new GameObject("PercentageText", typeof(Text));
        pctObj.transform.SetParent(canvasObj.transform, false);
        od.percentageText = pctObj.GetComponent<Text>();
        var pctRT = od.percentageText.GetComponent<RectTransform>();
        pctRT.anchorMin = new Vector2(0f, 0f);
        pctRT.anchorMax = new Vector2(1f, 0.5f);
        pctRT.offsetMin = pctRT.offsetMax = Vector2.zero;
        od.percentageText.font = od.nameText.font;
        od.percentageText.fontSize = percentFontSize;
        od.percentageText.alignment = TextAnchor.MiddleCenter;
        od.percentageText.color = textColor;
        od.percentageText.resizeTextForBestFit = true;
        od.percentageText.resizeTextMinSize = Mathf.Max(14, percentFontSize / 2);
        od.percentageText.resizeTextMaxSize = percentFontSize;
        // 🔒 Que no bloquee clicks:
        od.percentageText.raycastTarget = false;

        od.textCanvas = canvas;
    }

    void SetupClickOnMain(AreaOverlayData od, GameObject area)
    {
        // Agrega un collider al objeto "clickeable" principal
        GameObject clickGO = null;
        if (useExactMeshOverlay && od.overlayMeshes.Count > 0) clickGO = od.overlayMeshes[0];
        else if (od.overlayQuad != null) clickGO = od.overlayQuad;
        else clickGO = area; // ⬅️ Fallback: raíz del área (por si no hubo overlay)

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

    // ===================== Contenido =====================
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

        var data = areaManager.GetAreaData(key); // tu AreaManager expone los datos por área
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

        // aplicar color a todos los meshes/clones
        if (useExactMeshOverlay)
        {
            foreach (var ov in od.overlayMeshes)
            {
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

        if (od.nameText) od.nameText.text = display.ToUpper();
        if (od.percentageText) od.percentageText.text = $"{overall:F0}%";
    }

    public void RefreshOverlays()
    {
        foreach (var kv in areaOverlays)
            UpdateOverlayContent(kv.Value, kv.Key);
    }

    void SetOverlaysActive(bool active)
    {
        foreach (var od in areaOverlays.Values)
        {
            if (useExactMeshOverlay)
            {
                foreach (var ov in od.overlayMeshes) if (ov) ov.SetActive(active);
            }
            if (od.overlayQuad) od.overlayQuad.SetActive(active);
            if (od.textCanvas) od.textCanvas.gameObject.SetActive(active);
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

    // Click → delega en tu flujo existente del manager
    public void HandleAreaClick(GameObject area)
    {
        if (areaManager != null)
            areaManager.OnAreaClicked(area);
    }
}

// Auxiliar para click
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
