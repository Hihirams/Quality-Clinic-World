using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // para UI guard
using UnityEngine.UI;          // para botón de alternar

public class AreaManager : MonoBehaviour
{
    [Header("Referencias del Sistema")]
    public IndustrialDashboard dashboard;

    [Header("Configuración de Áreas")]
    public List<GameObject> areaObjects = new List<GameObject>();

    [Header("Configuración de Debug")]
    public bool enableDebugMode = true;

    // ===== Datos por área =====
    private Dictionary<string, AreaData> areaDataDict = new Dictionary<string, AreaData>();

    // Sólo registrar posiciones REALES (no mover nada)
    private Dictionary<string, Vector3> realAreaPositions = new Dictionary<string, Vector3>();

    [System.Serializable]
    public class AreaData
    {
        public string areaName;
        public string displayName;
        public float delivery;
        public float quality;
        public float parts;
        public float processManufacturing;
        public float trainingDNA;
        public float mtto;
        public float overallResult;
        public string status;
        public Color statusColor;
    }

    // ====== Integración Top-Down ======
    [Header("Vista Top-Down")]
    public bool enableTopDownView = true;

    [Tooltip("Margen para que el mapa quepa holgado en Top-Down (1.0 exacto, >1 = más aire)")]
    public float fitPadding = 2.0f;

    private TopDownCameraController topDownController;
    private bool isInTopDownMode = false;
    private readonly List<AreaCard> areaCards = new List<AreaCard>();

    // ====== Overlays estáticos ======
    [Header("Overlays estáticos (Top-Down)")]
    public bool enableOverlays = true;
    public AreaOverlayPainter overlayPainter; // se auto-resuelve si está en null

    // ====== Botón UI para alternar cámara ======
    private Button cameraToggleButton;
    private Text cameraToggleText;

    void Start()
    {
        InitializeAreaData();

        // Hallar dashboard si no está asignado
        if (dashboard == null)
        {
            dashboard = FindFirstObjectByType<IndustrialDashboard>();
            if (dashboard == null)
            {
                Debug.LogError("No se encontró IndustrialDashboard en la escena");
                return;
            }
            if (enableDebugMode) Debug.Log("Dashboard encontrado automáticamente");
        }

        // Delegado de detalle dinámico
        dashboard.ProvideDetail = (areaDisplayName, kpi) => GenerateDetailText(areaDisplayName, kpi);

        // Si no tienes áreas llenadas a mano, intenta encontrarlas
        if (areaObjects.Count == 0)
        {
            FindAreasAutomatically();
        }

        // Registrar SOLO posiciones actuales (no modificar nada en escena)
        RegisterRealAreaPositions();

        // Crear/asegurar tarjetas por área
        CreateAreaCards();

        // Ocultar dashboard al inicio
        dashboard.HideInterface();

        // Dejar un frame para que exista UI y luego configurar cámara/overlays/botón
        StartCoroutine(SetupTopDownLate());
    }

    IEnumerator SetupTopDownLate()
    {
        yield return null;

        if (enableTopDownView)
        {
            SetupTopDownCamera();
            CollectAreaCardsAuto();
            BuildCameraToggleButton();
            ApplyCardsMode(false); // iniciamos en modo Libre
        }

        if (enableOverlays)
            EnsureOverlayPainterReady(false); // arrancar oculto (no Top-Down)
    }

    void EnsureOverlayPainterReady(bool startInTopDown)
    {
        if (overlayPainter == null)
        {
            overlayPainter = FindFirstObjectByType<AreaOverlayPainter>();
            if (overlayPainter == null)
            {
                var go = new GameObject("OverlayManager");
                overlayPainter = go.AddComponent<AreaOverlayPainter>();
            }
        }

        // Mostrar/ocultar overlays según modo
        overlayPainter.SetTopDownMode(startInTopDown);
    }

    // =========================
    // Top-Down: Setup + Helpers
    // =========================
    void SetupTopDownCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("No hay Main Camera para configurar Top-Down");
            return;
        }

        topDownController = mainCamera.GetComponent<TopDownCameraController>();
        if (topDownController == null)
            topDownController = mainCamera.gameObject.AddComponent<TopDownCameraController>();

        // Calcular centro/tamaño de la planta en función de tus áreas
        Vector3 plantCenter = CalculatePlantCenter();
        Vector2 plantSize = CalculatePlantSize();

        var settings = new TopDownCameraController.TopDownSettings
        {
            cameraHeight = Mathf.Max(150f, Mathf.Max(plantSize.x, plantSize.y) * 1.1f),
            cameraAngle = 22f,
            plantCenter = plantCenter,
            viewportWidth = plantSize.x * fitPadding,
            viewportDepth = plantSize.y * fitPadding
        };

        topDownController.ApplySettings(settings);

        if (enableDebugMode) Debug.Log($"[TopDown] Centro {plantCenter} | Tamaño {plantSize} | Padding {fitPadding}");
    }

    void CollectAreaCardsAuto()
    {
        areaCards.Clear();
        foreach (var area in areaObjects)
        {
            if (area == null) continue;
            var card = area.GetComponentInChildren<AreaCard>(true);
            if (card != null) areaCards.Add(card);
        }
        if (enableDebugMode) Debug.Log($"[TopDown] Encontradas {areaCards.Count} AreaCard");
    }

    Vector3 CalculatePlantCenter()
    {
        if (areaObjects.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var a in areaObjects) { if (a) { sum += a.transform.position; n++; } }
        return n > 0 ? sum / n : Vector3.zero;
    }

    Vector2 CalculatePlantSize()
    {
        if (areaObjects.Count == 0) return new Vector2(100, 100);
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var a in areaObjects)
        {
            if (a == null) continue;
            Bounds b = GetObjectBounds(a);
            minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
            minZ = Mathf.Min(minZ, b.min.z); maxZ = Mathf.Max(maxZ, b.max.z);
        }
        float w = Mathf.Max(50f, maxX - minX);
        float d = Mathf.Max(50f, maxZ - minZ);
        return new Vector2(w, d);
    }

    Bounds GetObjectBounds(GameObject obj)
    {
        var rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 5f);
        Bounds c = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) c.Encapsulate(rends[i].bounds);
        return c;
    }

    // ===========================
    // Botón UI: alternar la cámara
    // ===========================
    void BuildCameraToggleButton()
    {
        EnsureEventSystem();
        Canvas canvas = FindOrCreateUICanvas();

        // Contenedor
        GameObject go = new GameObject("Btn_ToggleCamera", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(130, 36);
        rt.anchoredPosition = new Vector2(-18, -52);

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.sprite = CreateRoundedSprite(16);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;

        var shadow = go.GetComponent<Shadow>();
        shadow.effectDistance = new Vector2(0, -2);
        shadow.effectColor = new Color(0, 0, 0, 0.18f);

        cameraToggleButton = go.GetComponent<Button>();
        var cb = cameraToggleButton.colors;
        cb.normalColor = img.color;
        cb.highlightedColor = new Color(0.98f, 0.98f, 1f, 0.98f);
        cb.pressedColor = new Color(0.90f, 0.92f, 0.95f, 0.95f);
        cb.fadeDuration = 0.08f;
        cameraToggleButton.colors = cb;

        // Texto
        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(go.transform, false);
        var rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = rtTxt.anchorMax = new Vector2(0.5f, 0.5f);
        rtTxt.sizeDelta = new Vector2(120, 30);

        cameraToggleText = txtObj.GetComponent<Text>();
        cameraToggleText.text = "Vista: Libre";
        cameraToggleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cameraToggleText.fontSize = 14;
        cameraToggleText.fontStyle = FontStyle.Bold;
        cameraToggleText.color = new Color(0.18f, 0.18f, 0.22f, 1f);
        cameraToggleText.alignment = TextAnchor.MiddleCenter;

        cameraToggleButton.onClick.AddListener(ToggleCameraMode);
    }

    // Sprite redondeado 9-slice simple (para el botón)
    Sprite CreateRoundedSprite(int cornerRadius = 12)
    {
        int width = 64, height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        bool IsInsideRounded(int x, int y, int w, int h, int r)
        {
            bool corner = (x < r && y < r) || (x >= w - r && y < r) ||
                          (x < r && y >= h - r) || (x >= w - r && y >= h - r);
            if (!corner) return true;
            Vector2 center = (x < r && y < r) ? new Vector2(r, r) :
                             (x >= w - r && y < r) ? new Vector2(w - r, r) :
                             (x < r && y >= h - r) ? new Vector2(r, h - r) :
                                                     new Vector2(w - r, h - r);
            return Vector2.Distance(new Vector2(x, y), center) <= r;
        }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = IsInsideRounded(x, y, width, height, cornerRadius) ? Color.white : Color.clear;

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
    }

    Canvas FindOrCreateUICanvas()
    {
        // Reutiliza el Canvas del dashboard si existe (UI_Canvas).
        var existing = GameObject.Find("UI_Canvas");
        if (existing != null) return existing.GetComponent<Canvas>();

        // Si no, crea uno muy simple
        var canvasObj = new GameObject("UI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("EventSystem creado automáticamente para UI.");
        }
    }

    // ===========================
    // Alternar modos de cámara
    // ===========================
    public void ToggleCameraMode()
    {
        if (topDownController == null)
        {
            Debug.LogWarning("No hay TopDownCameraController configurado.");
            return;
        }

        isInTopDownMode = !isInTopDownMode;

        if (isInTopDownMode)
        {
            topDownController.SetTopDownMode(); // transición al mapa
            ApplyCardsMode(true);
            if (cameraToggleText) cameraToggleText.text = "Vista: Mapa";

            // activar overlays
            if (enableOverlays) EnsureOverlayPainterReady(true);
            if (enableDebugMode) Debug.Log("→ Vista Top-Down");
        }
        else
        {
            topDownController.SetFreeMode(); // volver a libre
            ApplyCardsMode(false);
            if (cameraToggleText) cameraToggleText.text = "Vista: Libre";

            // ocultar overlays
            if (enableOverlays) EnsureOverlayPainterReady(false);
            if (enableDebugMode) Debug.Log("→ Vista Libre");
        }
    }

    void ApplyCardsMode(bool topdown)
    {
        foreach (var card in areaCards)
        {
            if (card == null) continue;
            card.SetTopDownMode(topdown);
        }
    }

    // ===========================
    // Flujo de selección
    // ===========================
    public void OnAreaClicked(GameObject areaObject)
    {
        if (dashboard == null) return;

        string areaKey = GetAreaKey(areaObject.name);
        if (!areaDataDict.ContainsKey(areaKey)) return;

        var data = areaDataDict[areaKey];

        var kpis = new List<KPIData>
        {
            new KPIData("Delivery", data.delivery, "%"),
            new KPIData("Quality", data.quality, "%"),
            new KPIData("Parts", data.parts, "%"),
            new KPIData("Process Manufacturing", data.processManufacturing, "%"),
            new KPIData("Training DNA", data.trainingDNA, "%"),
            new KPIData("Mantenimiento", data.mtto, "%"),
            new KPIData("Overall Result", data.overallResult, "%")
        };

        var predicciones = GeneratePredictions(data);
        dashboard.UpdateWithAreaData(data.displayName, kpis, predicciones);

        // Focus/zoom según modo
        Transform target = areaObject.transform;

        if (isInTopDownMode && topDownController != null)
        {
            // enfoque suave en Top-Down (método del controlador)
            topDownController.FocusOnAreaTopDown(target, 0.75f);
        }
        else
        {
            // MODO LIBRE (control estilo Sims)
            Vector3 focusPosition = realAreaPositions.ContainsKey(areaKey)
                ? realAreaPositions[areaKey]
                : areaObject.transform.position;

            var cameraController = Camera.main?.GetComponent<FreeCameraController>();
            if (cameraController != null) cameraController.FocusOnArea(areaObject.transform, 25f);

            if (enableDebugMode) Debug.Log($"✅ Área seleccionada: {data.displayName}  @ {focusPosition}");
        }
    }

    public void OnAreaClicked(AreaCard areaCard)
    {
        if (areaCard != null && areaCard.gameObject != null) OnAreaClicked(areaCard.gameObject);
    }

    // ===== Heurísticas simples para enriquecer el detalle =====
    string GenerateDetailText(string areaDisplayName, KPIData kpi)
    {
        // Mapear displayName a clave
        string areaKey = null;
        foreach (var kv in areaDataDict)
        {
            if (string.Equals(kv.Value.displayName, areaDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                areaKey = kv.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(areaKey) || !areaDataDict.ContainsKey(areaKey))
        {
            string unit = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
            return "Detalle de " + kpi.name + "\nÁrea: " + areaDisplayName + "\nActual: " + kpi.value.ToString("F1") + unit;
        }

        var d = areaDataDict[areaKey];
        string n = (kpi.name ?? "").ToLowerInvariant();

        if (n.Contains("delivery"))
            return "Delivery — " + d.displayName + "\n"
                 + "Actual: " + d.delivery.ToString("F1") + "%\n"
                 + "• Órdenes planificadas: " + GetEstOrders(d.delivery) + "\n"
                 + "• Incumplimientos: " + GetIncidences(d.delivery) + "\n"
                 + "• Retraso promedio: " + GetDelayMins(d.delivery) + " min\n"
                 + "Acción: asegurar JIT, balanceo de línea y seguimiento de transporte.";

        if (n.Contains("quality"))
            return "Quality — " + d.displayName + "\n"
                 + "Actual: " + d.quality.ToString("F1") + "%\n"
                 + "• PPM estimado: " + GetPpm(d.quality) + "\n"
                 + "• Top defectos: " + GetTopDefects() + "\n"
                 + "• Retrabajos/día: " + GetReworks(d.quality) + "\n"
                 + "Acción: Gemba + 5-Why sobre el defecto principal; contención si PPM > objetivo.";

        if (n.Contains("parts"))
            return "Parts Availability — " + d.displayName + "\n"
                 + "Disponibilidad: " + d.parts.ToString("F1") + "%\n"
                 + "• SKU críticos: " + GetCriticalSkus(d.parts) + "\n"
                 + "• Backorders: " + GetBackorders(d.parts) + "\n"
                 + "• Cobertura: " + GetCoverageDays(d.parts) + " día(s)\n"
                 + "Acción: escalonar compras urgentes y validar alternos.";

        if (n.Contains("process"))
            return "Process Manufacturing — " + d.displayName + "\n"
                 + "Capacidad efectiva: " + d.processManufacturing.ToString("F1") + "%\n"
                 + "• OEE estimado: " + GetOee(d.processManufacturing) + "%\n"
                 + "• Cuellos de botella: " + GetBottlenecksCount(d.processManufacturing) + "\n"
                 + "• SMED pendientes: " + GetSmedPend(d.processManufacturing) + "\n"
                 + "Acción: Kaizen corto en cuello principal.";

        if (n.Contains("training"))
            return "Training DNA — " + d.displayName + "\n"
                 + "Cumplimiento: " + d.trainingDNA.ToString("F1") + "%\n"
                 + "• Cursos críticos vencidos: " + GetExpiredCourses(d.trainingDNA) + "\n"
                 + "• Polivalencia: " + GetPolyvalence(d.trainingDNA) + "%\n"
                 + "• Rotación mes: " + GetTurnover() + "%\n"
                 + "Acción: reentrenar estándar de trabajo en estaciones clave.";

        if (n.Contains("mantenimiento") || n.Contains("mtto"))
            return "Mantenimiento — " + d.displayName + "\n"
                 + "Cumplimiento PM: " + d.mtto.ToString("F1") + "%\n"
                 + "• WO abiertas: " + GetOpenWo(d.mtto) + "\n"
                 + "• Paros mayores: " + GetMajorStops(d.mtto) + "\n"
                 + "• Próx. PM: " + GetNextPMDate() + "\n"
                 + "Acción: cerrar WO >72h y asegurar refacciones para fallas repetitivas.";

        if (n.Contains("overall"))
            return "Overall Result — " + d.displayName + "\n"
                 + "Índice global: " + d.overallResult.ToString("F1") + "%\n"
                 + "• Estado: " + d.status + "\n"
                 + "• Palanca principal: " + GetMainLever(d) + "\n"
                 + "Acción: enfoque en la palanca para +5 pts en 2 semanas.";

        string unit2 = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
        return "Detalle de " + kpi.name + "\nÁrea: " + d.displayName + "\nActual: " + kpi.value.ToString("F1") + unit2;
    }

    // ===== Heurísticas / helpers =====
    int GetEstOrders(float delivery) => Mathf.Clamp(Mathf.RoundToInt(50f * (delivery / 100f) + 5), 5, 60);
    int GetIncidences(float delivery) => delivery < 50 ? 5 : (delivery < 80 ? 2 : 0);
    int GetDelayMins(float delivery) => delivery < 50 ? 35 : (delivery < 80 ? 12 : 3);
    int GetPpm(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) * 120f), 0, 12000);
    string GetTopDefects() => "Faltante, Cosmético, Torque";
    int GetReworks(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) / 5f), 0, 6);
    int GetCriticalSkus(float parts) => Mathf.Clamp(Mathf.RoundToInt((100f - parts) / 12f), 0, 8);
    int GetBackorders(float parts) => Mathf.Clamp(Mathf.RoundToInt((100f - parts) / 20f), 0, 5);
    int GetCoverageDays(float parts) => Mathf.Clamp(Mathf.RoundToInt(parts / 20f), 0, 7);
    int GetOee(float pm) => Mathf.Clamp(Mathf.RoundToInt(pm * 0.85f), 20, 100);
    int GetBottlenecksCount(float pm) => pm < 60 ? 2 : (pm < 85 ? 1 : 0);
    int GetSmedPend(float pm) => pm < 80 ? 3 : 1;
    int GetExpiredCourses(float t) => t < 70 ? 4 : (t < 90 ? 1 : 0);
    int GetPolyvalence(float t) => Mathf.Clamp(Mathf.RoundToInt(t * 0.6f), 30, 90);
    int GetTurnover() => 3;
    int GetOpenWo(float mtto) => mtto < 60 ? 7 : (mtto < 90 ? 3 : 1);
    int GetMajorStops(float mtto) => mtto < 60 ? 2 : 0;
    string GetNextPMDate() => System.DateTime.Now.AddDays(3).ToString("dd/MM");
    string GetMainLever(AreaData d)
    {
        float min = Mathf.Min(d.delivery, d.quality, d.parts, d.processManufacturing, d.trainingDNA, d.mtto);
        if (Mathf.Approximately(min, d.delivery)) return "Delivery";
        if (Mathf.Approximately(min, d.quality)) return "Quality";
        if (Mathf.Approximately(min, d.parts)) return "Parts";
        if (Mathf.Approximately(min, d.processManufacturing)) return "Process Manufacturing";
        if (Mathf.Approximately(min, d.trainingDNA)) return "Training DNA";
        return "Mantenimiento";
    }

    // ===== Infraestructura existente + guard de UI =====
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverBlockingUI())
            HandleAreaClickSimplified();

        if (Input.GetKeyDown(KeyCode.I) && enableDebugMode) ShowAreaDebugInfo();
        if (Input.GetKeyDown(KeyCode.P) && enableDebugMode) DebugRealAreaPositions();
        if (Input.GetKeyDown(KeyCode.U) && enableDebugMode) DebugAreaPositionsDetailed();
        if (Input.GetKeyDown(KeyCode.H) && enableDebugMode) DebugAreaHierarchy();

        if (Input.GetKeyDown(KeyCode.Escape)) dashboard?.HideInterface();
    }

    // NUEVO: solo bloquea UI de ScreenSpace (HUD). Permite clicks a través de Canvas WorldSpace (overlays).
    bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null) return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        foreach (var rr in raycastResults)
        {
            var canvas = rr.gameObject.GetComponentInParent<Canvas>();
            if (canvas == null) continue;

            // Bloquea solo si es ScreenSpace (Overlay o Camera). WorldSpace = dejar pasar.
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return true;
        }
        return false;
    }

    void RegisterRealAreaPositions()
    {
        if (enableDebugMode) Debug.Log("=== REGISTRANDO POSICIONES REALES (SIN MOVER NADA) ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string areaKey = GetAreaKey(areaObj.name);
            realAreaPositions[areaKey] = areaObj.transform.position;
            if (enableDebugMode) Debug.Log($"✓ {areaObj.name} (Key: {areaKey}) - Posición REAL preservada: {areaObj.transform.position}");
        }
    }

    void DebugAreaPositionsDetailed()
    {
        Debug.Log("=== 🔍 ANÁLISIS DETALLADO DE POSICIONES DE ÁREAS ===");
        Dictionary<Vector3, List<string>> positionGroups = new Dictionary<Vector3, List<string>>();
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            Vector3 pos = areaObj.transform.position;
            string info = $"{areaObj.name} (Key: {GetAreaKey(areaObj.name)})";
            bool found = false;
            foreach (var kvp in positionGroups)
            {
                if (Vector3.Distance(kvp.Key, pos) < 0.1f) { kvp.Value.Add(info); found = true; break; }
            }
            if (!found) positionGroups[pos] = new List<string> { info };
        }
        int conflicts = 0;
        foreach (var kvp in positionGroups)
        {
            if (kvp.Value.Count > 1) { conflicts++; Debug.Log($"⚠️ Conflicto en {kvp.Key} -> {kvp.Value.Count} áreas"); }
        }
        Debug.Log(conflicts == 0 ? "🎉 Sin conflictos" : $"⚠️ Conflictos totales: {conflicts}");
    }

    void DebugAreaHierarchy()
    {
        Debug.Log("=== 🔍 HIERARQUÍA DE ÁREAS ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string k = GetAreaKey(areaObj.name);
            Debug.Log($"🏭 {areaObj.name} (Key: {k})  pos:{areaObj.transform.position}");
            Collider col = areaObj.GetComponent<Collider>();
            if (col != null) Debug.Log($"   📦 Collider: {col.GetType().Name}");
        }
    }

    void HandleAreaClickSimplified()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // ALCANCE AMPLIADO para top-down / cámara alta
        RaycastHit[] hits = Physics.RaycastAll(ray, 750f);
        if (hits.Length == 0) return;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            GameObject go = hit.collider.gameObject;
            foreach (GameObject areaObj in areaObjects)
            {
                if (areaObj == go || IsChildOfArea(go, areaObj))
                {
                    OnAreaClicked(areaObj);
                    return;
                }
            }
        }
    }

    void DebugRealAreaPositions()
    {
        Debug.Log("=== POSICIONES REALES ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string key = GetAreaKey(areaObj.name);
            Vector3 cur = areaObj.transform.position;
            Vector3 reg = realAreaPositions.ContainsKey(key) ? realAreaPositions[key] : Vector3.zero;
            Debug.Log($"🏭 {areaObj.name} -> actual:{cur}  registrada:{reg}");
        }
    }

    void FindAreasAutomatically()
    {
        areaObjects.Clear();
        string[] names = { "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1" };
        foreach (string n in names)
        {
            var found = GameObject.Find(n);
            if (found != null) { areaObjects.Add(found); if (enableDebugMode) Debug.Log($"✓ Área encontrada: {found.name}"); }
        }
    }

    void CreateAreaCards()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            AreaCard card = areaObj.GetComponent<AreaCard>();
            if (card == null) card = areaObj.AddComponent<AreaCard>();

            string areaKey = GetAreaKey(areaObj.name);
            if (areaDataDict.ContainsKey(areaKey))
            {
                var data = areaDataDict[areaKey];
                card.areaName = data.displayName;
            }
            else
            {
                card.areaName = areaObj.name;
                Debug.LogWarning($"No se encontraron datos para el área: {areaObj.name}");
            }

            SetupAreaCollider(areaObj);
        }
    }

    bool IsChildOfArea(GameObject clickedObj, GameObject areaObj)
    {
        Transform t = clickedObj.transform;
        while (t != null)
        {
            if (t.gameObject == areaObj) return true;
            t = t.parent;
        }
        return false;
    }

    string GetAreaKey(string objectName)
    {
        string upper = (objectName ?? "").ToUpperInvariant();
        if (upper == "AREA_ATHONDA" || upper.Contains("ATHONDA") || upper.Contains("AT HONDA")) return "ATHONDA";
        if (upper == "AREA_VCTL4" || upper.Contains("VCTL4") || upper.Contains("VCT L4")) return "VCTL4";
        if (upper == "AREA_BUZZERL2" || upper.Contains("BUZZERL2") || upper.Contains("BUZZER L2")) return "BUZZERL2";

        // Más tolerante para VB L1
        if (upper == "AREA_VBL1" || upper.Contains("VBL1") || upper.Contains("VB L1") || (upper.Contains("VB") && upper.Contains("L1")))
            return "VBL1";

        return upper.Replace("AREA_", "");
    }

    List<string> GeneratePredictions(AreaData d)
    {
        List<string> p = new List<string>();
        if (d.delivery < 50) p.Add("🚨 CRÍTICO: Problemas severos de entrega detectados");
        else if (d.delivery < 80) p.Add("⚠️ Delivery bajo riesgo - Optimización recomendada");
        if (d.quality < 70) p.Add("🔧 Control de calidad requiere intervención");
        if (d.trainingDNA < 70) p.Add("📚 Personal requiere capacitación urgente");
        if (d.overallResult < 50) p.Add("🚨 ZONA ROJA: Intervención ejecutiva inmediata");
        else if (d.overallResult >= 90) p.Add("🏆 ZONA OPTIMUS: Benchmark para otras áreas");
        return p;
    }

    void SetupAreaCollider(GameObject areaObj)
    {
        if (areaObj.GetComponent<Collider>() != null) return;

        BoxCollider box = areaObj.AddComponent<BoxCollider>();
        Renderer[] renderers = areaObj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            box.center = areaObj.transform.InverseTransformPoint(b.center);
            box.size = b.size;
        }
        else
        {
            box.size = Vector3.one * 10f;
            box.center = Vector3.up * 2.5f;
        }
        Debug.Log($"✓ BoxCollider agregado a: {areaObj.name}");
    }

    void ShowAreaDebugInfo()
    {
        Debug.Log("=== INFO DE ÁREAS ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string key = GetAreaKey(areaObj.name);
            Debug.Log($"Área: {areaObj.name} (Key: {key}) - Posición: {areaObj.transform.position}");
        }
    }

    public void CloseDashboard()
    {
        dashboard?.HideInterface();
    }

    public AreaData GetAreaData(string areaKey) =>
        areaDataDict.ContainsKey(areaKey) ? areaDataDict[areaKey] : null;

    public List<GameObject> GetAreaObjects() => areaObjects; // usado por AreaOverlayPainter para construir overlays

    // ====== Datos demo (ajusta a tus reales) ======
    void InitializeAreaData()
    {
        areaDataDict["ATHONDA"] = new AreaData
        {
            areaName = "ATHONDA",
            displayName = "AT Honda",
            delivery = 100f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 100f,
            trainingDNA = 100f,
            mtto = 100f,
            overallResult = 95f,
            status = "Optimus",
            statusColor = new Color(0.0f, 0.4f, 0.0f, 1.0f)
        };

        areaDataDict["VCTL4"] = new AreaData
        {
            areaName = "VCTL4",
            displayName = "VCT L4",
            delivery = 77f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 100f,
            trainingDNA = 81f,
            mtto = 100f,
            overallResult = 92f,
            status = "Optimus",
            statusColor = new Color(0.0f, 0.4f, 0.0f, 1.0f)
        };

        areaDataDict["BUZZERL2"] = new AreaData
        {
            areaName = "BUZZERL2",
            displayName = "BUZZER L2",
            delivery = 91f,
            quality = 83f,
            parts = 81f,
            processManufacturing = 89f,
            trainingDNA = 62f,
            mtto = 100f,
            overallResult = 73f,
            status = "Sick",
            statusColor = Color.yellow
        };

        areaDataDict["VBL1"] = new AreaData
        {
            areaName = "VBL1",
            displayName = "VB L1",
            delivery = 29f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 32f,
            trainingDNA = 100f,
            mtto = 47f,
            overallResult = 49f,
            status = "High risk",
            statusColor = Color.red
        };
    }
}
