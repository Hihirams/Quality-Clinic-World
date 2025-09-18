using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class AreaOverlayPainter : MonoBehaviour
{
    [Header("Overlay Settings")]
    [SerializeField] private bool useExactMeshOverlay = true;      // Mesh duplicado o Quad
    [SerializeField] private Material overlayMaterial;              // Unlit/Transparent o URP/Lit
    [SerializeField] private float overlayHeight = 0.02f;           // Evitar z-fighting
    [SerializeField] private float padding = 0f;                    // (para Quad)

    [Header("Textos automáticos del Painter")]
    [Tooltip("Oculta SOLO los textos automáticos del painter en vista MAPA (Top-Down fija). No afecta tus ManualAreaLabel.")]
    [SerializeField] private bool hideTextsInStaticTopDown = true;

    [Header("References")]
    [SerializeField] private AreaManager areaManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // Estado
    private bool isTopDownMode = false;

    // Datos por área
    private readonly Dictionary<GameObject, AreaOverlayData> areaOverlays = new();

    // --- Contenedor por área ---
    [System.Serializable]
    public class AreaOverlayData
    {
        public List<GameObject> overlayMeshes = new(); // cuando useExactMeshOverlay = true
        public GameObject overlayQuad;                  // cuando useExactMeshOverlay = false
        public Collider clickCollider;                  // para click en el overlay
    }

    void Start()
    {
        if (areaManager == null) areaManager = FindObjectOfType<AreaManager>();
        InitializeOverlays();
        SetOverlaysActive(false); // por defecto oculto hasta entrar a MAPA si así lo prefieres
    }

    // === API llamado desde TopDownCameraController / AreaManager ===
    public void SetTopDownMode(bool enable)
    {
        if (enableDebug) Debug.Log($"[AreaOverlayPainter] SetTopDownMode: {enable}");
        isTopDownMode = enable;

        SetOverlaysActive(isTopDownMode);
        if (isTopDownMode) RefreshOverlaysForTopDown();

        // En MAPA ocultamos SOLO textos automáticos del painter (si existieran)
        bool isStaticMap = IsStaticTopDownView();
        if (hideTextsInStaticTopDown && isStaticMap) SetOverlayTextsEnabled(false);
        else SetOverlayTextsEnabled(true);
    }

    // =================== Construcción de overlays ===================

    void InitializeOverlays()
    {
        foreach (var area in GetAreasFromManager())
        {
            if (!area) continue;
            CreateOverlayForArea(area);
        }
    }

    List<GameObject> GetAreasFromManager()
        => areaManager != null ? (areaManager.GetAreaObjects() ?? new List<GameObject>()) : new List<GameObject>();

    void CreateOverlayForArea(GameObject area)
    {
        var od = new AreaOverlayData();

        if (useExactMeshOverlay) BuildExactMeshOverlays(area, od);
        else BuildQuadOverlay(area, od);

        SetupClick(od, area);
        UpdateOverlayColorFromManager(od, area); // color inicial según status

        areaOverlays[area] = od;
    }

    void BuildExactMeshOverlays(GameObject area, AreaOverlayData od)
    {
        foreach (var mf in area.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf || mf.sharedMesh == null) continue;
            var srcR = mf.GetComponent<MeshRenderer>(); if (!srcR) continue;

            var ov = new GameObject("OverlayMesh_" + mf.name);
            ov.transform.SetParent(mf.transform, false);
            ov.transform.localPosition += Vector3.up * overlayHeight; // levanta un poco

            var newMF = ov.AddComponent<MeshFilter>(); newMF.sharedMesh = mf.sharedMesh;
            var newMR = ov.AddComponent<MeshRenderer>();

            var mat = overlayMaterial != null ? new Material(overlayMaterial) : new Material(Shader.Find("Unlit/Color"));
            // Alpha base suave (URP o Standard)
            SetMatColor(mat, new Color(1f, 1f, 1f, 0.35f));
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
        var b = CalculateAreaBounds(area);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Overlay_{area.name}";
        quad.transform.SetParent(area.transform, false);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.position = new Vector3(b.center.x, b.min.y + overlayHeight, b.center.z);
        quad.transform.localScale = new Vector3(Mathf.Max(0.1f, b.size.x + padding), Mathf.Max(0.1f, b.size.z + padding), 1f);

        var rend = quad.GetComponent<Renderer>();
        var mat = overlayMaterial != null ? new Material(overlayMaterial) : new Material(Shader.Find("Unlit/Color"));
        SetMatColor(mat, new Color(1f, 1f, 1f, 0.35f));
        rend.sharedMaterial = mat;

        // Quitar collider del primitive si no lo necesitas
        var col = quad.GetComponent<Collider>(); if (col) Destroy(col);

        od.overlayQuad = quad;
    }

    Bounds CalculateAreaBounds(GameObject area)
    {
        var rs = area.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(area.transform.position, new Vector3(1, 0.1f, 1));

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // =================== Coloreo desde AreaManager ===================

    struct OverlayInfo { public float overall; public Color color; }

    OverlayInfo? GetInfoFromManager(GameObject area)
    {
        if (!areaManager) return null;
        string key = NormalizeKey(area.name);
        var data = areaManager.GetAreaData(key);
        if (data == null) return null;
        return new OverlayInfo { overall = data.overallResult, color = data.statusColor };
    }

    string NormalizeKey(string objectName)
    {
        string u = (objectName ?? "").ToUpperInvariant();
        if (u.StartsWith("AREA_")) u = u.Substring(5);
        return u.Replace('_', ' ').Trim();
    }

    void UpdateOverlayColorFromManager(AreaOverlayData od, GameObject area)
    {
        var info = GetInfoFromManager(area);
        var baseCol = info.HasValue ? info.Value.color : new Color(1, 1, 1, 1);
        var finalCol = new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f); // alpha fijo para overlay

        if (useExactMeshOverlay)
        {
            foreach (var ov in od.overlayMeshes)
            {
                if (!ov) continue;
                var mr = ov.GetComponent<MeshRenderer>();
                if (mr?.sharedMaterial != null) SetMatColor(mr.sharedMaterial, finalCol);
            }
        }
        else if (od.overlayQuad)
        {
            var mr = od.overlayQuad.GetComponent<Renderer>();
            if (mr?.sharedMaterial != null) SetMatColor(mr.sharedMaterial, finalCol);
        }
    }

    // Soporte de shaders URP/Standard/Unlit
    static void SetMatColor(Material m, Color c)
    {
        if (!m) return;

        // Transparencia amigable URP (ignorado si el shader no lo usa)
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 0 Opaque, 1 Transparent
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c; // fallback
    }

    // =================== Mostrar/Ocultar y refresco ===================

    void SetOverlaysActive(bool active)
    {
        foreach (var od in areaOverlays.Values)
        {
            if (useExactMeshOverlay)
                foreach (var ov in od.overlayMeshes) if (ov) ov.SetActive(active);
            if (od.overlayQuad) od.overlayQuad.SetActive(active);
        }
    }

    void RefreshOverlaysForTopDown()
    {
        foreach (var kv in areaOverlays)
            UpdateOverlayColorFromManager(kv.Value, kv.Key);
    }

    // =================== Click forwarding (opcional) ===================

    void SetupClick(AreaOverlayData od, GameObject area)
    {
        // Usa un collider del propio área (o crea uno si no hay) para propagar clicks
        var target = area;
        var col = target.GetComponent<Collider>();
        if (!col)
        {
            var box = target.AddComponent<BoxCollider>();
            var b = CalculateAreaBounds(area);
            box.center = target.transform.InverseTransformPoint(b.center);
            box.size = b.size;
            col = box;
        }
        od.clickCollider = col;

        var click = target.GetComponent<AreaOverlayClick>();
        if (!click) click = target.AddComponent<AreaOverlayClick>();
        click.Initialize(area, this);
    }

    public void HandleAreaClick(GameObject area)
    {
        if (areaManager != null) areaManager.OnAreaClicked(area);
    }

    // =================== Utilidades ===================

    private bool IsStaticTopDownView()
    {
        var cam = Camera.main;
        if (!cam) return false;
        var top = cam.GetComponent<TopDownCameraController>();
        return top != null && top.IsUsingFixedStaticView();
    }

    /// Oculta/Muestra SOLO componentes de texto bajo este GameObject (por si
    /// quedó algún texto automático de versiones previas del painter).
    private void SetOverlayTextsEnabled(bool enabled)
    {
        int count = 0;
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (!tr || tr == transform) continue;

            var uText = tr.GetComponent<Text>();
            if (uText) { uText.enabled = enabled; count++; }

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
            var tmpUGUI = tr.GetComponent<TextMeshProUGUI>();
            if (tmpUGUI) { tmpUGUI.enabled = enabled; count++; }

            var tmp = tr.GetComponent<TextMeshPro>();
            if (tmp) { tmp.enabled = enabled; count++; }
#endif
        }
        if (enableDebug) Debug.Log($"[AreaOverlayPainter] Textos del painter {(enabled ? "ON" : "OFF")} ({count}).");
    }

    void OnDestroy()
    {
        foreach (var od in areaOverlays.Values)
        {
            foreach (var ov in od.overlayMeshes) if (ov) DestroyImmediate(ov);
            if (od.overlayQuad) DestroyImmediate(od.overlayQuad);
        }
        areaOverlays.Clear();
    }
}

// Enrutador simple de click al AreaManager
public class AreaOverlayClick : MonoBehaviour
{
    private GameObject targetArea;
    private AreaOverlayPainter overlayPainter;

    public void Initialize(GameObject area, AreaOverlayPainter painter)
    { targetArea = area; overlayPainter = painter; }

    void OnMouseDown()
    {
        if (overlayPainter != null && targetArea != null)
            overlayPainter.HandleAreaClick(targetArea);
    }
}
