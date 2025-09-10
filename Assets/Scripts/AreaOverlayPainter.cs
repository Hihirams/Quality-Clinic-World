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
    [SerializeField] private int nameFontSize = 64;           // ← AUMENTADO
    [SerializeField] private int percentFontSize = 120;       // ← AUMENTADO
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float canvasScale = 0.005f;      // ← REDUCIDO para texto más grande
    [SerializeField] private bool debugMode = true;

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
        if (debugMode)
            Debug.Log($"[AreaOverlayPainter] SetTopDownMode llamado con: {isTopDown}");

        isTopDownMode = isTopDown;
        SetOverlaysActive(isTopDownMode);

        if (isTopDownMode)
        {
            // FORZAR actualización inmediata y reconfigurar textos
            RefreshOverlaysForStaticView();

            // También forzar después de un frame
            StartCoroutine(RefreshCanvasesDelayed());
        }
    }

    // NUEVO: Configuración específica para vista estática
    private void RefreshOverlaysForStaticView()
    {
        foreach (var kv in areaOverlays)
        {
            var area = kv.Key;
            var od = kv.Value;

            if (od.textCanvas != null)
            {
                // Asegurar que el canvas esté activo y visible
                od.textCanvas.gameObject.SetActive(true);
                od.textCanvas.enabled = true;

                // Configurar para vista estática (desde arriba)
                ConfigureCanvasForTopDown(od, area);
            }

            UpdateOverlayContent(od, area);
        }
    }

    private void ConfigureCanvasForTopDown(AreaOverlayData od, GameObject area)
    {
        if (od.textCanvas == null) return;

        // Posición y rotación optimizada para vista desde arriba
        Bounds b = CalculateAreaBounds(area);

        // Posicionar el canvas más arriba y sin rotación
        od.textCanvas.transform.position = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
        od.textCanvas.transform.rotation = Quaternion.identity; // Sin rotación
        od.textCanvas.transform.localScale = Vector3.one * canvasScale;

        // Asegurar configuración correcta del canvas
        od.textCanvas.renderMode = RenderMode.WorldSpace;
        od.textCanvas.sortingOrder = 200; // Más alto que antes

        // Canvas más grande para mejor visibilidad
        RectTransform canvasRT = od.textCanvas.GetComponent<RectTransform>();
        float canvasWidth = Mathf.Max(b.size.x / canvasScale, 2000f);  // ← MÁS GRANDE
        float canvasHeight = Mathf.Max(b.size.z / canvasScale, 1600f); // ← MÁS GRANDE
        canvasRT.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        if (debugMode)
        {
            Debug.Log($"[TopDown] Canvas reconfigurado para {area.name}:");
            Debug.Log($"  - Posición: {od.textCanvas.transform.position}");
            Debug.Log($"  - Escala: {od.textCanvas.transform.localScale}");
            Debug.Log($"  - Size: {canvasRT.sizeDelta}");
        }
    }

    private System.Collections.IEnumerator RefreshCanvasesDelayed()
    {
        yield return null; // Esperar un frame
        yield return null; // Esperar otro frame más

        foreach (var od in areaOverlays.Values)
        {
            if (od.textCanvas != null)
            {
                // Forzar reactivación
                od.textCanvas.enabled = false;
                yield return null;
                od.textCanvas.enabled = true;

                // Forzar rebuild
                Canvas.ForceUpdateCanvases();

                if (debugMode)
                    Debug.Log($"[Delayed] Canvas reactivado: {od.textCanvas.name}");
            }
        }
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

    // ====== Textos - VERSION MEJORADA PARA VISTA ESTÁTICA ======
    void BuildTextCanvas(GameObject area, AreaOverlayData od)
    {
        Bounds b = CalculateAreaBounds(area);

        // Crear el canvas como WorldSpace
        GameObject canvasObj = new GameObject("OverlayCanvas");
        canvasObj.transform.SetParent(area.transform, false);

        // Canvas horizontal y más arriba
        float canvasY = b.max.y + overlayHeight * 5f; // ← MÁS ALTURA
        canvasObj.transform.position = new Vector3(b.center.x, canvasY, b.center.z);
        canvasObj.transform.rotation = Quaternion.identity; // Sin rotación = horizontal
        canvasObj.transform.localScale = Vector3.one * canvasScale;

        // Configurar Canvas
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200; // ← MÁS ALTO

        // Configurar CanvasScaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // Configurar GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();

        // Canvas MÁS GRANDE para mejor visibilidad
        RectTransform canvasRT = canvasObj.GetComponent<RectTransform>();
        float canvasWidth = Mathf.Max(b.size.x / canvasScale, 2000f);   // ← AUMENTADO
        float canvasHeight = Mathf.Max(b.size.z / canvasScale, 1600f);  // ← AUMENTADO
        canvasRT.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        // ===== TEXTO DEL NOMBRE - MEJORADO =====
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(canvasObj.transform, false);

        Text nameText = nameObj.AddComponent<Text>();
        nameText.font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = Mathf.Max(nameFontSize, 64);      // ← AUMENTADO
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = textColor;
        nameText.raycastTarget = false;

        // Configuración mejorada del RectTransform
        RectTransform nameRT = nameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.6f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.anchoredPosition = Vector2.zero;
        nameRT.sizeDelta = Vector2.zero;
        nameRT.offsetMin = new Vector2(50, nameRT.offsetMin.y); // márgenes
        nameRT.offsetMax = new Vector2(-50, nameRT.offsetMax.y);

        // ===== TEXTO DEL PORCENTAJE - MEJORADO =====
        GameObject pctObj = new GameObject("PercentageText");
        pctObj.transform.SetParent(canvasObj.transform, false);

        Text pctText = pctObj.AddComponent<Text>();
        pctText.font = nameText.font;
        pctText.fontSize = Mathf.Max(percentFontSize, 120);   // ← AUMENTADO
        pctText.fontStyle = FontStyle.Bold;
        pctText.alignment = TextAnchor.MiddleCenter;
        pctText.color = textColor;
        pctText.raycastTarget = false;

        RectTransform pctRT = pctText.GetComponent<RectTransform>();
        pctRT.anchorMin = new Vector2(0f, 0f);
        pctRT.anchorMax = new Vector2(1f, 0.6f);
        pctRT.anchoredPosition = Vector2.zero;
        pctRT.sizeDelta = Vector2.zero;
        pctRT.offsetMin = new Vector2(50, pctRT.offsetMin.y); // márgenes
        pctRT.offsetMax = new Vector2(-50, pctRT.offsetMax.y);

        // Guardar referencias
        od.textCanvas = canvas;
        od.nameText = nameText;
        od.percentageText = pctText;

        if (debugMode)
        {
            Debug.Log($"[AreaOverlay] Canvas creado para {area.name}:");
            Debug.Log($"  - Posición: {canvasObj.transform.position}");
            Debug.Log($"  - Escala: {canvasObj.transform.localScale}");
            Debug.Log($"  - Canvas Size: {canvasRT.sizeDelta}");
            Debug.Log($"  - Font sizes: name={nameText.fontSize}, pct={pctText.fontSize}");
        }
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

        if (debugMode)
        {
            Debug.Log($"[AreaOverlay] Actualizando {area.name}: display='{display}', overall={overall}%, color={c}");
        }

        // aplicar color a todos los meshes/clones
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

        // Actualizar textos con validación MEJORADA
        if (od.nameText)
        {
            string nameToShow = display.ToUpper();
            od.nameText.text = nameToShow;
            od.nameText.enabled = true;
            od.nameText.gameObject.SetActive(true); // ← FORZAR ACTIVO

            if (debugMode)
                Debug.Log($"[AreaOverlay] Texto nombre FORZADO: '{nameToShow}' - active: {od.nameText.gameObject.activeInHierarchy}");
        }

        if (od.percentageText)
        {
            string pctToShow = $"{overall:F0}%";
            od.percentageText.text = pctToShow;
            od.percentageText.enabled = true;
            od.percentageText.gameObject.SetActive(true); // ← FORZAR ACTIVO

            if (debugMode)
                Debug.Log($"[AreaOverlay] Texto porcentaje FORZADO: '{pctToShow}' - active: {od.percentageText.gameObject.activeInHierarchy}");
        }

        // Forzar rebuild del canvas
        if (od.textCanvas != null)
        {
            od.textCanvas.gameObject.SetActive(true);
            od.textCanvas.enabled = true;
            Canvas.ForceUpdateCanvases();
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

            // TEXTO SIEMPRE ACTIVO en Top-Down
            if (od.textCanvas)
            {
                od.textCanvas.gameObject.SetActive(active);
                if (active && isTopDownMode)
                {
                    // Configuración especial para vista estática
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

                    if (debugMode)
                        Debug.Log($"[SetActive] Canvas y textos activados para top-down: {od.textCanvas.name}");
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

    // Click → delega en tu flujo existente del manager
    public void HandleAreaClick(GameObject area)
    {
        if (areaManager != null)
            areaManager.OnAreaClicked(area);
    }

    // Método para debug - llamar desde el inspector
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
                Debug.Log($"  Canvas escala: {od.textCanvas.transform.localScale}");
                Debug.Log($"  Canvas size: {od.textCanvas.GetComponent<RectTransform>().sizeDelta}");
            }
        }
    }

    // NUEVO: Método para forzar visibilidad desde inspector
    [ContextMenu("Force Show Overlays")]
    void ForceShowOverlays()
    {
        SetTopDownMode(true);
        RefreshOverlaysForStaticView();
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