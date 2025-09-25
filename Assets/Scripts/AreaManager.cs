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

    [Header("Configuraci�n de �reas")]
    public List<GameObject> areaObjects = new List<GameObject>();

    [Header("Configuraci�n de Debug")]
    public bool enableDebugMode = true;

    // ===== Datos por �rea =====
    private Dictionary<string, AreaData> areaDataDict = new Dictionary<string, AreaData>();
    private Dictionary<string, Vector3> realAreaPositions = new Dictionary<string, Vector3>();

    private readonly Dictionary<GameObject, Bounds> areaBoundsByObject = new Dictionary<GameObject, Bounds>();
    private AreaOverlayPainter overlayPainter;


    [Header("Colliders Precisos")]
    public List<PreciseColliderData> preciseColliders = new List<PreciseColliderData>();

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

    [System.Serializable]
    public class PreciseColliderData
    {
        public string areaKey;
        public Vector3 cubePosition; // Posicion exacta del cubo
        public Vector3 cubeSize;     // Dimensiones del cubo
        public string cubeChildName; // Nombre del cubo hijo
        public float heightOffset = 1f; // Altura del collider
    }

    // ====== Integraci�n Top-Down ======
    [Header("Vista Top-Down")]
    public bool enableTopDownView = true;
    public float fitPadding = 2.0f;

    private TopDownCameraController topDownController;
    private bool isInTopDownMode = false;
    private readonly List<AreaCard> areaCards = new List<AreaCard>();

    // ====== Bot�n UI para alternar c�mara ======
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
                Debug.LogError("No se encontr� IndustrialDashboard en la escena");
                return;
            }
            if (enableDebugMode) Debug.Log("Dashboard encontrado autom�ticamente");
        }

        dashboard.ProvideDetail = (areaDisplayName, kpi) => GenerateDetailText(areaDisplayName, kpi);

        overlayPainter = FindObjectOfType<AreaOverlayPainter>();


        if (areaObjects.Count == 0)
        {
            FindAreasAutomatically();
        }

        SetupAreaLayers();

        InitializePreciseColliders();

        var fixer = FindFirstObjectByType<AreaPositionFixerV2>();
        if (fixer != null)
        {
            fixer.FixAreaPositionsAndChildren();
            if (enableDebugMode) Debug.Log("Posiciones de �reas corregidas antes de registrar");
        }

        RegisterRealAreaPositions();

        SetupCollidersByChildCubes();

        SanitizeAreaColliders();          // normaliza tamaños/capa
        DisableLabelRaycastsAndLayer();   // evita que labels bloqueen

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

        if (enableDebugMode) Debug.Log($"[TopDown] Centro {plantCenter} | Tama�o {plantSize} | Padding {fitPadding}");
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
            Debug.Log("EventSystem creado autom�ticamente para UI.");
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
        if (overlayPainter) overlayPainter.SetTopDownMode(true);   // ← NUEVO
        if (cameraToggleText) cameraToggleText.text = "Vista: Mapa";

        // Ya lo haces: actualizar labels
        NotifyManualLabelsUpdate();

        if (enableDebugMode) Debug.Log("📍 Vista Top-Down");
    }
    else
    {
        topDownController.SetFreeMode();
        ApplyCardsMode(false);
        if (overlayPainter) overlayPainter.SetTopDownMode(false);  // ← NUEVO
        if (cameraToggleText) cameraToggleText.text = "Vista: Libre";

        // Ya lo haces: actualizar labels
        NotifyManualLabelsUpdate();

        if (enableDebugMode) Debug.Log("📍 Vista Libre");
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

            if (enableDebugMode) Debug.Log($"AreaManager: No se encontr� ManualLabelsManager, usando fallback para {manualLabels.Length} labels");
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

            if (enableDebugMode) Debug.Log($"? �rea seleccionada: {data.displayName}  @ {focusPosition}");
        }
    }

    public void OnAreaClicked(AreaCard areaCard)
    {
        if (areaCard != null && areaCard.gameObject != null) OnAreaClicked(areaCard.gameObject);
    }

    // ===== El resto de m�todos permanecen igual =====
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
            return "Detalle de " + kpi.name + "\n�rea: " + areaDisplayName + "\nActual: " + kpi.value.ToString("F1") + unit;
        }

        var d = areaDataDict[areaKey];
        string n = (kpi.name ?? "").ToLowerInvariant();

        if (n.Contains("delivery"))
            return "Delivery � " + d.displayName + "\n"
                 + "Actual: " + d.delivery.ToString("F1") + "%\n"
                 + "� �rdenes planificadas: " + GetEstOrders(d.delivery) + "\n"
                 + "� Incumplimientos: " + GetIncidences(d.delivery) + "\n"
                 + "� Retraso promedio: " + GetDelayMins(d.delivery) + " min\n"
                 + "Acci�n: asegurar JIT, balanceo de l�nea y seguimiento de transporte.";

        if (n.Contains("quality"))
            return "Quality � " + d.displayName + "\n"
                 + "Actual: " + d.quality.ToString("F1") + "%\n"
                 + "� PPM estimado: " + GetPpm(d.quality) + "\n"
                 + "� Top defectos: " + GetTopDefects() + "\n"
                 + "� Retrabajos/d�a: " + GetReworks(d.quality) + "\n"
                 + "Acci�n: Gemba + 5-Why sobre el defecto principal; contenci�n si PPM > objetivo.";

        // ... resto de casos similares

        string unit2 = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
        return "Detalle de " + kpi.name + "\n�rea: " + d.displayName + "\nActual: " + kpi.value.ToString("F1") + unit2;
    }

    // M�todos auxiliares
    int GetEstOrders(float delivery) => Mathf.Clamp(Mathf.RoundToInt(50f * (delivery / 100f) + 5), 5, 60);
    int GetIncidences(float delivery) => delivery < 50 ? 5 : (delivery < 80 ? 2 : 0);
    int GetDelayMins(float delivery) => delivery < 50 ? 35 : (delivery < 80 ? 12 : 3);
    int GetPpm(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) * 120f), 0, 12000);
    string GetTopDefects() => "Faltante, Cosm�tico, Torque";
    int GetReworks(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) / 5f), 0, 6);

    void Update()
    {
        // DEBUG: Mostrar que hay bajo el cursor
        if (Input.GetMouseButtonDown(0))
        {
            var eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            Debug.Log("=== RAYCAST DEBUG ===");
            Debug.Log($"Elementos bajo cursor: {results.Count}");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var hasButton = r.gameObject.GetComponentInParent<Button>() != null;
                var raycastTarget = r.gameObject.GetComponent<Graphic>()?.raycastTarget ?? false;
                Debug.Log($"  [{i}] {r.gameObject.name} | Button: {hasButton} | RaycastTarget: {raycastTarget}");
            }
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverBlockingUI())
            HandleAreaClickSimplified();

        if (Input.GetKeyDown(KeyCode.I) && enableDebugMode) ShowAreaDebugInfo();
        if (Input.GetKeyDown(KeyCode.Escape)) dashboard?.HideInterface();
        if (Input.GetKeyDown(KeyCode.F8)) DebugCurrentPositions();
        if (Input.GetKeyDown(KeyCode.F9)) SetupAreaLayers();
if (Input.GetKeyDown(KeyCode.F10)) DetectRealCubeDimensions();
// F11 ahora aplica los colliders “mejorados”
if (Input.GetKeyDown(KeyCode.F11)) SetupMultipleCubeColliders();
// F12 te deja comparar contra el método original por datos precalculados
if (Input.GetKeyDown(KeyCode.F12)) SetupPreciseAreaColliders();
// (Opcional) mueve el método por hijos a F6 si quieres conservarlo:
// if (Input.GetKeyDown(KeyCode.F6)) SetupCollidersByChildCubes();

    }

    // En AreaManager.cs, reemplaza el m�todo IsPointerOverBlockingUI():

    bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null) return false;

        var eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // Si HAY cualquier Graphic con raycastTarget activo bajo el cursor, bloquear
        foreach (var r in results)
        {
            var graphic = r.gameObject.GetComponentInParent<Graphic>();
            if (graphic != null && graphic.raycastTarget) return true;
            if (r.gameObject.GetComponentInParent<Button>() != null) return true;
            if (r.gameObject.GetComponentInParent<Scrollbar>() != null) return true;
        }
        return false;
    }


    bool ShouldBlockClick(GameObject uiElement)
    {
        // Nombres de elementos que S� deben bloquear clicks al mundo 3D
        string[] blockingElements = {
        "CloseButton",
        "KPIs_Button",
        "Predicciones_Button",
        "Btn_ToggleCamera",
        "CloseDetail",
        "VerticalScrollbar"
    };

        // Si es uno de los elementos bloqueantes
        foreach (string blockingName in blockingElements)
        {
            if (uiElement.name.Contains(blockingName))
                return true;
        }

        // Si es el panel principal pero NO es contenido interno
        if (uiElement.name == "MainPanel")
            return true;

        // Si es el panel de detalle pero NO es contenido interno
        if (uiElement.name == "DetailPanel")
            return true;

        // Los botones "Ver detalle" NO deben bloquear (para que pasen al 3D)
        if (uiElement.name.Contains("Btn_VerDetalle"))
            return false;

        // Contenido interno del dashboard NO debe bloquear
        if (uiElement.name.Contains("KPI_") ||
            uiElement.name.Contains("Content") ||
            uiElement.name.Contains("ProgressBar") ||
            uiElement.name.Contains("Text") ||
            uiElement.name.Contains("AlertBox"))
            return false;

        return false;
    }


    Bounds CalculateAreaBounds(GameObject areaObj)
    {
        var renderers = areaObj.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(areaObj.transform.position, new Vector3(4f, 2f, 4f));

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void RegisterRealAreaPositions()
    {
        if (enableDebugMode) Debug.Log("=== REGISTRANDO POSICIONES REALES ===");
        areaBoundsByObject.Clear();
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string areaKey = GetAreaKey(areaObj.name);
            var bounds = CalculateAreaBounds(areaObj);
            realAreaPositions[areaKey] = bounds.center;
            areaBoundsByObject[areaObj] = bounds;
            if (enableDebugMode) Debug.Log($"[Bounds] {areaObj.name} (Key: {areaKey}) -> center {bounds.center}");
        }
    }



void InitializePreciseColliders()
{
    preciseColliders.Clear();

    // ATHONDA - Dimensiones detectadas
    preciseColliders.Add(new PreciseColliderData
    {
        areaKey = "ATHONDA",
        cubePosition = new Vector3(-58.15f, 1.5f, 109.06f),
        cubeSize = new Vector3(42.50f, 3.00f, 24.60f),
        cubeChildName = "Cube (8)",
        heightOffset = 5f
    });

    // VCTL4 - Dimensiones detectadas
    preciseColliders.Add(new PreciseColliderData
    {
        areaKey = "VCTL4",
        cubePosition = new Vector3(-1.85f, 1.5f, 24.32f),
        cubeSize = new Vector3(32.00f, 3.00f, 43.23f),
        cubeChildName = "Cube (52)",
        heightOffset = 5f
    });

    // BUZZERL2 - Dimensiones detectadas
    preciseColliders.Add(new PreciseColliderData
    {
        areaKey = "BUZZERL2",
        cubePosition = new Vector3(0.35f, 1.5f, -15.01f),
        cubeSize = new Vector3(27.90f, 3.00f, 8.70f),
        cubeChildName = "Cube (49)",
        heightOffset = 5f
    });

    // VBL1 - cubo principal (60) - Dimensiones detectadas
    preciseColliders.Add(new PreciseColliderData
    {
        areaKey = "VBL1",
        cubePosition = new Vector3(-0.92f, 1.5f, 146.04f),
        cubeSize = new Vector3(31.30f, 3.00f, 32.30f),
        cubeChildName = "Cube (60)", // ignoramos (61) y (62) aquí
        heightOffset = 5f
    });

    Debug.Log($"[PreciseColliders] Inicializados con dimensiones REALES: {preciseColliders.Count} colliders");
}

    void SetupPreciseAreaColliders()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            PreciseColliderData colliderData = preciseColliders.Find(x => x.areaKey == areaKey);

            if (colliderData == null)
            {
                Debug.LogWarning($"[PreciseColliders] No se encontraron datos para {areaKey}");
                continue;
            }

            var existingColliders = areaObj.GetComponents<Collider>();
            for (int i = existingColliders.Length - 1; i >= 0; i--)
            {
                var existing = existingColliders[i];
                if (existing == null) continue;
                if (Application.isPlaying) Destroy(existing);
                else DestroyImmediate(existing);
            }

            BoxCollider preciseCollider = areaObj.AddComponent<BoxCollider>();

            Vector3 localCenter = areaObj.transform.InverseTransformPoint(colliderData.cubePosition);
            preciseCollider.center = localCenter;

            Vector3 localSize = colliderData.cubeSize;
            localSize.y = colliderData.heightOffset;
            preciseCollider.size = localSize;

            preciseCollider.isTrigger = false;

            Debug.Log($"[PreciseColliders] {areaKey}: Centro local {localCenter}, Tamano {localSize}");

            Transform cubeChild = areaObj.transform.Find(colliderData.cubeChildName);
            if (cubeChild == null)
            {
                Debug.LogWarning($"[PreciseColliders] Cubo hijo '{colliderData.cubeChildName}' no encontrado en {areaObj.name}");
            }
        }
    }

    // Método mejorado que soporta áreas con múltiples cubos (VBL1)
void SetupMultipleCubeColliders()
{
    foreach (GameObject areaObj in areaObjects)
    {
        if (areaObj == null) continue;

        string areaKey = GetAreaKey(areaObj.name);

        // Remover colliders existentes del área
        var existingColliders = areaObj.GetComponents<Collider>();
        foreach (var col in existingColliders)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        if (areaKey == "VBL1")
        {
            SetupVBL1MultipleColliders(areaObj);
        }
        else
        {
            SetupSingleCubeCollider(areaObj, areaKey);
        }
    }
}

void SetupVBL1MultipleColliders(GameObject areaObj)
{
    var vbl1Cubes = new[]
    {
        new { name = "Cube (60)", center = new Vector3(-0.92f, 1.5f, 146.04f), size = new Vector3(31.30f, 5f, 32.30f) },
        new { name = "Cube (61)", center = new Vector3(-17.12f, 1.5f, 134.74f), size = new Vector3(20.60f, 5f, 9.80f) },
        new { name = "Cube (62)", center = new Vector3(-17.12f, 1.5f, 159.44f), size = new Vector3(20.60f, 5f, 5.90f) }
    };

    foreach (var cubeData in vbl1Cubes)
    {
        GameObject colliderObj = new GameObject($"Collider_{cubeData.name}");
        colliderObj.transform.SetParent(areaObj.transform, false);
        colliderObj.transform.position = cubeData.center;

        var box = colliderObj.AddComponent<BoxCollider>();
        box.size = cubeData.size;
        box.center = Vector3.zero;

        int areasLayer = LayerMask.NameToLayer("Areas");
        if (areasLayer != -1) colliderObj.layer = areasLayer;

        Debug.Log($"[VBL1] Collider {cubeData.name}: Centro {cubeData.center}, Tamaño {cubeData.size}");
    }
}

void SetupSingleCubeCollider(GameObject areaObj, string areaKey)
{
    PreciseColliderData colliderData = preciseColliders.Find(x => x.areaKey == areaKey);
    if (colliderData == null)
    {
        Debug.LogWarning($"[SingleCube] No hay datos para {areaKey}");
        return;
    }

    var box = areaObj.AddComponent<BoxCollider>();

    Vector3 localCenter = areaObj.transform.InverseTransformPoint(colliderData.cubePosition);
    box.center = localCenter;

    Vector3 size = colliderData.cubeSize;
    size.y = colliderData.heightOffset;

    Vector3 lossyScale = areaObj.transform.lossyScale;
    box.size = new Vector3(
        size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
        size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
        size.z / Mathf.Max(0.001f, Mathf.Abs(lossyScale.z))
    );

    Debug.Log($"[SingleCube] {areaKey}: Centro local {box.center}, Tamaño {box.size}");
}


    void SetupCollidersByChildCubes()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            PreciseColliderData colliderData = preciseColliders.Find(x => x.areaKey == areaKey);

            if (colliderData == null) continue;

            Transform cubeChild = areaObj.transform.Find(colliderData.cubeChildName);
            if (cubeChild == null)
            {
                Debug.LogWarning($"[ChildCube] No se encontro {colliderData.cubeChildName} en {areaObj.name}");
                continue;
            }

            Renderer cubeRenderer = cubeChild.GetComponent<Renderer>();
            if (cubeRenderer == null)
            {
                Debug.LogWarning($"[ChildCube] No hay Renderer en {colliderData.cubeChildName}");
                continue;
            }

            var existingColliders = areaObj.GetComponents<Collider>();
            for (int i = existingColliders.Length - 1; i >= 0; i--)
            {
                var existing = existingColliders[i];
                if (existing == null) continue;
                if (Application.isPlaying) Destroy(existing);
                else DestroyImmediate(existing);
            }

            BoxCollider box = areaObj.AddComponent<BoxCollider>();
            Bounds cubeBounds = cubeRenderer.bounds;

            box.center = areaObj.transform.InverseTransformPoint(cubeBounds.center);

            Vector3 size = cubeBounds.size;
            size.y = Mathf.Max(size.y, colliderData.heightOffset);

            Vector3 lossyScale = areaObj.transform.lossyScale;
            box.size = new Vector3(
                size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
                size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
                size.z / Mathf.Max(0.001f, Mathf.Abs(lossyScale.z))
            );

            Debug.Log($"[ChildCube] {areaKey}: Collider basado en {colliderData.cubeChildName}");
            Debug.Log($"[ChildCube] Centro: {box.center}, Tamano: {box.size}");
        }
    }

[ContextMenu("Detectar Dimensiones Reales")]
void DetectRealCubeDimensions()
{
    Debug.Log("=== DETECTANDO DIMENSIONES REALES DE CUBOS ===");

    foreach (GameObject areaObj in areaObjects)
    {
        if (areaObj == null) continue;

        string areaKey = GetAreaKey(areaObj.name);
        Debug.Log($"\n[Detect] Area: {areaObj.name} (Key: {areaKey})");

        foreach (Transform child in areaObj.transform)
        {
            if (child.name.Contains("Cube"))
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    Debug.Log($"[Detect] - {child.name}:");
                    Debug.Log($"[Detect]   Posicion: {child.position}");
                    Debug.Log($"[Detect]   Bounds Centro: {bounds.center}");
                    Debug.Log($"[Detect]   Bounds Tamano: {bounds.size}");
                    Debug.Log($"[Detect]   Transform Scale: {child.localScale}");
                }
            }
        }
    }

    Debug.Log("=== USA ESTOS DATOS PARA AJUSTAR preciseColliders ===");
}

void HandleAreaClickSimplified()
{
    Camera cam = Camera.main;
    if (cam == null) return;

    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
    int mask = LayerMask.GetMask("Areas");

    // 1) COLLIDERS primero (preciso)
    var hits = Physics.RaycastAll(ray, 2000f, mask);
    if (hits.Length > 0)
    {
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            var area = FindAreaForGameObject(h.collider.gameObject);
            if (area != null)
            {
                if (enableDebugMode) Debug.Log($"[PHY] Hit {h.collider.name} -> {area.name} d={h.distance:F2}");
                OnAreaClicked(area);
                return;
            }
        }
    }

    // 2) Fallback a BOUNDS por Renderers (por si falta algún collider)
    if (areaBoundsByObject != null && areaBoundsByObject.Count > 0)
    {
        GameObject bestArea = null;
        float bestDistance = float.MaxValue;

        foreach (var kvp in areaBoundsByObject)
        {
            var bounds = kvp.Value;
            if (bounds.IntersectRay(ray, out float d) && d < bestDistance)
            {
                bestDistance = d;
                bestArea = kvp.Key;
            }
        }

        if (bestArea != null)
        {
            if (enableDebugMode) Debug.Log($"[BND] {bestArea.name} d={bestDistance:F2}");
            OnAreaClicked(bestArea);
        }
    }
}

void SanitizeAreaColliders()
{
    int areasLayer = LayerMask.NameToLayer("Areas");
    foreach (var areaObj in areaObjects)
    {
        if (!areaObj) continue;

        foreach (var col in areaObj.GetComponentsInChildren<Collider>(true))
        {
            col.isTrigger = false; // clic sólido
            col.gameObject.layer = areasLayer;

            var box = col as BoxCollider;
            if (box != null)
            {
                var s = box.size;
                // IMPORTANTE: nunca permitir tamaños negativos + altura mínima
                box.size = new Vector3(Mathf.Abs(s.x), Mathf.Max(1f, Mathf.Abs(s.y)), Mathf.Abs(s.z));
            }
        }
    }
}

void DisableLabelRaycastsAndLayer()
{
    // Desactivar raycast en todos los gráficos de labels y sacarlos de la layer "Areas"
    var canvases = FindObjectsOfType<Canvas>(true);
    foreach (var c in canvases)
    {
        if (c.GetComponentInParent<ManualAreaLabel>() != null)
        {
            c.gameObject.layer = LayerMask.NameToLayer("Default");
            foreach (var g in c.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }
    }
}


    void SetupAreaLayers()
    {
        int areasLayer = LayerMask.NameToLayer("Areas");
        if (areasLayer == -1)
        {
            Debug.LogError("Layer 'Areas' no existe. Configurala en Project Settings > Tags and Layers.");
            return;
        }

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            foreach (Transform t in areaObj.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = areasLayer;
            }
            if (enableDebugMode) Debug.Log($"[Areas] {areaObj.name} asignada a la layer 'Areas'");
        }
    }

    GameObject FindAreaForGameObject(GameObject go)
    {
        if (go == null) return null;

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            if (areaObj == go) return areaObj;
        }

        Transform current = go.transform;
        while (current != null)
        {
            foreach (GameObject areaObj in areaObjects)
            {
                if (areaObj == null) continue;
                if (current.gameObject == areaObj) return areaObj;
            }
            current = current.parent;
        }

        return null;
    }

    [ContextMenu("Debug Posiciones Actuales")]
    void DebugCurrentPositions()
    {
        Debug.Log("=== POSICIONES ACTUALES DE AREAS ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string areaKey = GetAreaKey(areaObj.name);
            realAreaPositions.TryGetValue(areaKey, out var registeredPos);
            Vector3 currentPos = areaObj.transform.position;
            float diff = Vector3.Distance(currentPos, registeredPos);
            string layerName = LayerMask.LayerToName(areaObj.layer);
            Debug.Log($"- {areaObj.name} (Key: {areaKey})\n   Actual: {currentPos}\n   Registrada: {registeredPos}\n   Diferencia: {diff:F2}\n   Layer: {layerName}");
        }
    }

    void FindAreasAutomatically()
    {
        areaObjects.Clear();
        string[] names = { "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1" };
        foreach (string n in names)
        {
            var found = GameObject.Find(n);
            if (found != null) { areaObjects.Add(found); if (enableDebugMode) Debug.Log($"? �rea encontrada: {found.name}"); }
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
                Debug.LogWarning($"No se encontraron datos para el �rea: {areaObj.name}");
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
        if (d.delivery < 50) p.Add("?? CR�TICO: Problemas severos de entrega detectados");
        else if (d.delivery < 80) p.Add("?? Delivery bajo riesgo - Optimizaci�n recomendada");
        if (d.quality < 70) p.Add("?? Control de calidad requiere intervenci�n");
        if (d.trainingDNA < 70) p.Add("?? Personal requiere capacitaci�n urgente");
        if (d.overallResult < 50) p.Add("?? ZONA ROJA: Intervenci�n ejecutiva inmediata");
        else if (d.overallResult >= 90) p.Add("?? ZONA OPTIMUS: Benchmark para otras �reas");
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
        Debug.Log($"? BoxCollider agregado a: {areaObj.name}");
    }

    void ShowAreaDebugInfo()
    {
        Debug.Log("=== INFO DE �REAS ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            string key = GetAreaKey(areaObj.name);
            Debug.Log($"�rea: {areaObj.name} (Key: {key}) - Posici�n: {areaObj.transform.position}");
        }
    }

    public void CloseDashboard()
    {
        dashboard?.HideInterface();
        // Al cerrar dashboard, regresar la c�mara a la vista est�tica original (home)
        if (isInTopDownMode && topDownController != null)
        {
            topDownController.ReturnToStaticHome(0.6f);
        }

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




