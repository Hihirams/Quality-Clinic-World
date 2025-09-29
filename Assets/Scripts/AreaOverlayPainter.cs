using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif

/// <summary>
/// Pinta overlays de color sobre las áreas (mesh exacto o quad) y reacciona
/// a cambios de vista (Top-Down vs Libre). No toca tus ManualAreaLabel.
/// - Color de estado unificado con AppleTheme.Status(percent) cuando está habilitado
///   <see cref="useAppleCardPalette"/> para asegurar consistencia con AreaCard.
/// - Encapsula creación/ocultado/reciclado de overlays por área y reenvía clics
///   al <see cref="AreaManager"/> sin romper colisiones de UI.
/// </summary>
[DisallowMultipleComponent]
public class AreaOverlayPainter : MonoBehaviour
{
    #region Serialized Fields
    [Header("Apple-style Colors (match AreaCard)")]
    [SerializeField] private bool useAppleCardPalette = true;
    // Se conservan para no romper escenas; si no usas AppleTheme, sirven de fallback local.
    [SerializeField] private Color appleDarkGreen = new Color(0.00f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color appleLightGreen = new Color(0.40f, 0.70f, 0.30f, 1f);
    [SerializeField] private Color appleYellow    = new Color(1.00f, 0.80f, 0.20f, 1f);
    [SerializeField] private Color appleRed       = new Color(0.90f, 0.20f, 0.20f, 1f);
    [SerializeField, Range(0f, 1f)] private float overlayAlpha = 1f;

    [Header("Overlay Settings")]
    [SerializeField] private bool useExactMeshOverlay = true;      // Mesh duplicado o Quad
    [SerializeField] private Material overlayMaterial;              // Unlit/Transparent o URP/Lit
    [SerializeField] private float overlayHeight = 0.02f;           // Evitar z-fighting
    [SerializeField] private float padding = 0f;                    // (solo quad)

    [Header("Textos automáticos del Painter")]
    [Tooltip("Oculta SOLO textos automáticos del painter en vista MAPA (Top-Down fija). No afecta tus ManualAreaLabel.")]
    [SerializeField] private bool hideTextsInStaticTopDown = true;

    [Header("References")]
    [SerializeField] private AreaManager areaManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;
    #endregion

    #region Private State
    private bool _isTopDownMode = false;

    // Datos por área
    private readonly Dictionary<GameObject, AreaOverlayData> _areaOverlays = new();

    // Cache camera/main para checks rápidos
    private Camera _cachedMainCamera;

    [System.Serializable]
    public class AreaOverlayData
    {
        public List<GameObject> overlayMeshes = new(); // cuando useExactMeshOverlay = true
        public GameObject overlayQuad;                  // cuando useExactMeshOverlay = false
        public Collider clickCollider;                  // para click en el overlay
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Cache rápido de cámara
        _cachedMainCamera = Camera.main ?? FindObjectOfType<Camera>();
    }

    private void Start()
    {
        if (areaManager == null) areaManager = FindObjectOfType<AreaManager>();
        InitializeOverlays();
        // Por defecto oculto hasta entrar a MAPA (se conserva tu comportamiento)
        SetOverlaysActive(false);
        if (enableDebug) QCLog.Info("[AreaOverlayPainter] Start -> overlays inicializados y ocultos.");
    }

    private void OnDestroy()
    {
        // Limpieza explícita de objetos creados
        foreach (var od in _areaOverlays.Values)
        {
            if (od.overlayMeshes != null)
                foreach (var ov in od.overlayMeshes) if (ov) DestroyImmediate(ov);
            if (od.overlayQuad) DestroyImmediate(od.overlayQuad);
        }
        _areaOverlays.Clear();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Habilita/deshabilita modo Top-Down. Gestiona visibilidad de overlays
    /// y refresca color según KPI. Además, oculta SOLO textos automáticos del painter
    /// en vista estática para no duplicar info con tus ManualAreaLabel.
    /// </summary>
    public void SetTopDownMode(bool enable)
    {
        if (enableDebug) QCLog.Info($"[AreaOverlayPainter] SetTopDownMode: {enable}");
        _isTopDownMode = enable;

        SetOverlaysActive(_isTopDownMode);
        if (_isTopDownMode) RefreshOverlaysForTopDown();

        // En MAPA ocultamos SOLO textos automáticos del painter (si existieran)
        bool isStaticMap = IsStaticTopDownView();
        if (hideTextsInStaticTopDown && isStaticMap) SetOverlayTextsEnabled(false);
        else SetOverlayTextsEnabled(true);
    }

    /// <summary>
    /// Forward del click al AreaManager conservando tu flujo.
    /// </summary>
    public void HandleAreaClick(GameObject area)
    {
        if (areaManager != null) areaManager.OnAreaClicked(area);
        else if (enableDebug) QCLog.Warn("[AreaOverlayPainter] HandleAreaClick sin AreaManager.");
    }
    #endregion

    #region Internal Helpers
    private void InitializeOverlays()
    {
        foreach (var area in GetAreasFromManager())
        {
            if (!area) continue;
            CreateOverlayForArea(area);
        }
    }

    private List<GameObject> GetAreasFromManager()
        => areaManager != null ? (areaManager.GetAreaObjects() ?? new List<GameObject>()) : new List<GameObject>();

    private void CreateOverlayForArea(GameObject area)
    {
        var od = new AreaOverlayData();

        if (useExactMeshOverlay) BuildExactMeshOverlays(area, od);
        else BuildQuadOverlay(area, od);

        SetupClick(od, area);
        UpdateOverlayColorFromManager(od, area); // color inicial según status

        _areaOverlays[area] = od;
    }

    private void BuildExactMeshOverlays(GameObject area, AreaOverlayData od)
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
            // Siempre transparente + alpha externo controlado
            SetMatColor(mat, Color.white);

            newMR.sharedMaterial = mat;

            newMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            newMR.receiveShadows = false;
            newMR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            newMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            newMR.allowOcclusionWhenDynamic = false;

            od.overlayMeshes.Add(ov);
        }
    }

    private void BuildQuadOverlay(GameObject area, AreaOverlayData od)
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

    private Bounds CalculateAreaBounds(GameObject area)
    {
        var rs = area.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(area.transform.position, new Vector3(1, 0.1f, 1));

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private struct OverlayInfo { public float overall; public Color color; }

    private OverlayInfo? GetInfoFromManager(GameObject area)
    {
        if (!areaManager) return null;
        string key = NormalizeKey(area.name);
        var data = areaManager.GetAreaData(key);
        if (data == null) return null;
        return new OverlayInfo { overall = data.overallResult, color = data.statusColor };
    }

    private string NormalizeKey(string objectName)
    {
        string u = (objectName ?? "").ToUpperInvariant();
        if (u.StartsWith("AREA_")) u = u.Substring(5);
        return u.Replace('_', ' ').Trim();
    }

    /// <summary>
    /// Determina el color base para el overlay según los datos del AreaManager.
    /// Si <see cref="useAppleCardPalette"/> está activo, usa AppleTheme.Status(percent).
    /// Si no, usa el color del AreaManager. En última instancia, blanco.
    /// </summary>
    private Color ResolveStatusColor(float percent, Color? managerColorFallback)
    {
        if (useAppleCardPalette)
        {
            // Unificación de tema Apple entre tarjetas y overlays
            return AppleTheme.Status(percent);
        }

        if (managerColorFallback.HasValue) return managerColorFallback.Value;

        // Fallback final a paleta local mantenida para no romper escenas antiguas
        if (percent >= 95f) return appleDarkGreen;
        if (percent >= 80f) return appleLightGreen;
        if (percent >= 70f) return appleYellow;
        return appleRed;
    }

    private void UpdateOverlayColorFromManager(AreaOverlayData od, GameObject area)
    {
        var info = GetInfoFromManager(area);

        // Color base seleccionado
        Color baseCol = info.HasValue
            ? ResolveStatusColor(info.Value.overall, info.Value.color)
            : Color.white;

        var finalCol = new Color(baseCol.r, baseCol.g, baseCol.b, overlayAlpha);

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

    /// <remarks>
    /// Ajusta propiedades URP/Standard/Unlit para transparencia sin z-writing
    /// y aplica el color indicado respetando "_BaseColor" o "_Color".
    /// </remarks>
    private static void SetMatColor(Material m, Color c)
    {
        if (!m) return;

        // URP: Transparent + blending típico
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 0 Opaque, 1 Transparent
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
    }

    private void SetOverlaysActive(bool active)
    {
        foreach (var od in _areaOverlays.Values)
        {
            if (useExactMeshOverlay)
                foreach (var ov in od.overlayMeshes) if (ov) ov.SetActive(active);
            if (od.overlayQuad) od.overlayQuad.SetActive(active);
        }
    }

    private void RefreshOverlaysForTopDown()
    {
        foreach (var kv in _areaOverlays)
            UpdateOverlayColorFromManager(kv.Value, kv.Key);
    }

    private void SetupClick(AreaOverlayData od, GameObject area)
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

    /// <summary>
    /// ¿Estamos en top-down estático? Lo usamos para ocultar textos propios del painter.
    /// </summary>
    private bool IsStaticTopDownView()
    {
        var cam = _cachedMainCamera != null ? _cachedMainCamera : (_cachedMainCamera = Camera.main);
        if (!cam) return false;
        var top = cam.GetComponent<TopDownCameraController>();
        return top != null && top.IsUsingFixedStaticView();
    }

    /// <summary>
    /// Oculta/Muestra SOLO componentes de texto bajo este GameObject (por si
    /// quedó algún texto automático de versiones previas del painter).
    /// </summary>
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
        if (enableDebug) QCLog.Info($"[AreaOverlayPainter] Textos del painter {(enabled ? "ON" : "OFF")} ({count}).");
    }
    #endregion

    #region Debug
    // Clase interna para enrutar el click desde el collider del área
    public class AreaOverlayClick : MonoBehaviour
    {
        private GameObject _targetArea;
        private AreaOverlayPainter _overlayPainter;

        public void Initialize(GameObject area, AreaOverlayPainter painter)
        { _targetArea = area; _overlayPainter = painter; }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (_overlayPainter != null && _targetArea != null)
                _overlayPainter.HandleAreaClick(_targetArea);
        }
    }
    #endregion
}
