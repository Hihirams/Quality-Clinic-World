using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    public float fitPadding = 2.0f;

    private TopDownCameraController topDownController;
    private bool isInTopDownMode = false;
    private readonly List<AreaCard> areaCards = new List<AreaCard>();

    // ====== Botón UI para alternar cámara ======
    private Button cameraToggleButton;
    private Text cameraToggleText;

    void Start()
    {
        InitializeAreaData();

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

        dashboard.ProvideDetail = (areaDisplayName, kpi) => GenerateDetailText(areaDisplayName, kpi);

        if (areaObjects.Count == 0)
        {
            FindAreasAutomatically();
        }

        RegisterRealAreaPositions();
        CreateAreaCards();
        dashboard.HideInterface();

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
            ApplyCardsMode(false);
        }
    }

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

    void BuildCameraToggleButton()
    {
        EnsureEventSystem();
        Canvas canvas = FindOrCreateUICanvas();

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
        var existing = GameObject.Find("UI_Canvas");
        if (existing != null) return existing.GetComponent<Canvas>();

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
            topDownController.SetTopDownMode();
            ApplyCardsMode(true);
            if (cameraToggleText) cameraToggleText.text = "Vista: Mapa";

            // Notificar a los textos manuales
            NotifyManualLabelsUpdate();

            if (enableDebugMode) Debug.Log("→ Vista Top-Down");
        }
else
{
    topDownController.SetFreeMode();
    ApplyCardsMode(false);
    if (cameraToggleText) cameraToggleText.text = "Vista: Libre";

    // 🔧 Notificar para ocultar todos los ManualAreaLabel al salir de MAPA
    NotifyManualLabelsUpdate();

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

    // ===== NUEVO: Notificar a todos los ManualAreaLabel cuando cambie a vista top-down =====
    // ===== NUEVO: Notificar a todos los ManualAreaLabel cuando cambie a vista top-down =====
    void NotifyManualLabelsUpdate()
    {
        var labelsManager = FindObjectOfType<ManualLabelsManager>();
        if (labelsManager != null)
        {
            labelsManager.SetTopDownMode(isInTopDownMode);
            labelsManager.ForceRefreshAll();

            // Debug adicional
            if (enableDebugMode) Debug.Log($"AreaManager: Notificando modo TopDown = {isInTopDownMode} al ManualLabelsManager");
        }
        else
        {
            // Fallback - buscar directamente si no hay manager
            var manualLabels = FindObjectsOfType<ManualAreaLabel>();
            foreach (var label in manualLabels)
            {
                if (label != null)
                {
                    label.SetTopDownVisibility(isInTopDownMode);
                }
            }

            if (enableDebugMode) Debug.Log($"AreaManager: No se encontró ManualLabelsManager, usando fallback para {manualLabels.Length} labels");
        }
    }

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

        Transform target = areaObject.transform;

        if (isInTopDownMode && topDownController != null)
        {
            topDownController.FocusOnAreaTopDown(target, 0.75f);
        }
        else
        {
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

    // ===== El resto de métodos permanecen igual =====
    string GenerateDetailText(string areaDisplayName, KPIData kpi)
    {
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

        // ... resto de casos similares

        string unit2 = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
        return "Detalle de " + kpi.name + "\nÁrea: " + d.displayName + "\nActual: " + kpi.value.ToString("F1") + unit2;
    }

    // Métodos auxiliares
    int GetEstOrders(float delivery) => Mathf.Clamp(Mathf.RoundToInt(50f * (delivery / 100f) + 5), 5, 60);
    int GetIncidences(float delivery) => delivery < 50 ? 5 : (delivery < 80 ? 2 : 0);
    int GetDelayMins(float delivery) => delivery < 50 ? 35 : (delivery < 80 ? 12 : 3);
    int GetPpm(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) * 120f), 0, 12000);
    string GetTopDefects() => "Faltante, Cosmético, Torque";
    int GetReworks(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) / 5f), 0, 6);

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverBlockingUI())
            HandleAreaClickSimplified();

        if (Input.GetKeyDown(KeyCode.I) && enableDebugMode) ShowAreaDebugInfo();
        if (Input.GetKeyDown(KeyCode.Escape)) dashboard?.HideInterface();
    }

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

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return true;
        }
        return false;
    }

    void RegisterRealAreaPositions()
    {
        if (enableDebugMode) Debug.Log("=== REGISTRANDO POSICIONES REALES ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string areaKey = GetAreaKey(areaObj.name);
            realAreaPositions[areaKey] = areaObj.transform.position;
            if (enableDebugMode) Debug.Log($"✓ {areaObj.name} (Key: {areaKey}) - Posición REAL: {areaObj.transform.position}");
        }
    }

    void HandleAreaClickSimplified()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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

    public List<GameObject> GetAreaObjects() => areaObjects;

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
