﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gestor central de Ã¡reas industriales en Quality Clinic.
/// Responsabilidades:
/// - Administrar datos y posiciones de Ã¡reas (ATHONDA, VCTL4, BUZZERL2, VBL1)
/// - Detectar clicks en Ã¡reas 3D y activar dashboard
/// - Alternar entre vista libre (Sims) y top-down (mapa estÃ¡tico)
/// - Coordinar AreaCards, labels y overlays segÃºn modo de cÃ¡mara
/// - Proveer API de datos a IndustrialDashboard
/// </summary>
public class AreaManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias del Sistema")]
    public IndustrialDashboard dashboard;

    [Header("ConfiguraciÃ³n de Ãreas")]
    public List<GameObject> areaObjects = new List<GameObject>();

    [Header("ConfiguraciÃ³n de Debug")]
    public bool enableDebugMode = true;

    [Header("Colliders Precisos")]
    public List<PreciseColliderData> preciseColliders = new List<PreciseColliderData>();

    [Header("Vista Top-Down")]
    public bool enableTopDownView = true;
    public float fitPadding = 2.0f;

    #endregion

    #region Data Structures

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
        public Vector3 cubePosition;
        public Vector3 cubeSize;
        public string cubeChildName;
        public float heightOffset = 1f;
    }

    #endregion

    #region Private State

    // Datos por Ã¡rea
    private Dictionary<string, AreaData> areaDataDict = new Dictionary<string, AreaData>();
    private Dictionary<string, Vector3> realAreaPositions = new Dictionary<string, Vector3>();
    private readonly Dictionary<GameObject, Bounds> areaBoundsByObject = new Dictionary<GameObject, Bounds>();

    // Referencias cacheadas
    private AreaOverlayPainter overlayPainter;
    private TopDownCameraController topDownController;
    private Camera cachedMainCamera;
    private FreeCameraController freeCameraController;
    private ManualLabelsManager labelsManager;

    // Estado de vista
    private bool isInTopDownMode = false;
    private readonly List<AreaCard> areaCards = new List<AreaCard>();

    // UI Toggle Button
    private Button cameraToggleButton;
    private Text cameraToggleText;

    // OptimizaciÃ³n de raycast
    private readonly List<RaycastResult> raycastResultsCache = new List<RaycastResult>();

    #endregion

    #region Unity Callbacks

    void Start()
    {
        CacheComponents();
        InitializeAreaData();
        SetupDashboard();
        SetupAreas();
        StartCoroutine(SetupTopDownLate());
    }

    void Update()
    {
        // Click en Ã¡reas (solo si no estÃ¡ sobre UI bloqueante)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverBlockingUI())
        {
            HandleAreaClickSimplified();
        }

        // Hotkeys de debug (solo si enableDebugMode estÃ¡ activo)
        if (!enableDebugMode) return;

        if (Input.GetKeyDown(KeyCode.I)) ShowAreaDebugInfo();
        if (Input.GetKeyDown(KeyCode.Escape)) dashboard?.HideInterface();
        if (Input.GetKeyDown(KeyCode.F8)) DebugCurrentPositions();
        if (Input.GetKeyDown(KeyCode.F9)) SetupAreaLayers();
        if (Input.GetKeyDown(KeyCode.F10)) DetectRealCubeDimensions();
        if (Input.GetKeyDown(KeyCode.F11)) SetupMultipleCubeColliders();
        if (Input.GetKeyDown(KeyCode.F12)) SetupPreciseAreaColliders();
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

    #endregion

    #region Public API

    /// <summary>
    /// Maneja el click en un Ã¡rea desde collider 3D o AreaCard.
    /// Actualiza dashboard y mueve cÃ¡mara segÃºn modo activo (libre/top-down).
    /// </summary>
    public void OnAreaClicked(GameObject areaObject)
    {
        if (dashboard == null || areaObject == null) return;

        string areaKey = GetAreaKey(areaObject.name);
        if (!areaDataDict.ContainsKey(areaKey))
        {
            QCLog.Warn($"No se encontraron datos para Ã¡rea: {areaKey}");
            return;
        }

        var data = areaDataDict[areaKey];

        // Preparar KPIs para dashboard
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

        // Mover cÃ¡mara segÃºn modo
        Transform target = areaObject.transform;

        if (isInTopDownMode && topDownController != null)
        {
            topDownController.FocusOnAreaTopDown(target, 0.75f);
        }
        else
        {
            // Vista libre: usar posiciÃ³n real registrada
            Vector3 focusPosition = realAreaPositions.ContainsKey(areaKey)
                ? realAreaPositions[areaKey]
                : areaObject.transform.position;

            if (freeCameraController != null)
            {
                freeCameraController.FocusOnArea(areaObject.transform, 25f);
            }

            QCLog.Info($"Ãrea seleccionada: {data.displayName} @ {focusPosition}");
        }
    }

    /// <summary>
    /// Sobrecarga para clicks desde AreaCard.
    /// </summary>
    public void OnAreaClicked(AreaCard areaCard)
    {
        if (areaCard != null && areaCard.gameObject != null)
        {
            OnAreaClicked(areaCard.gameObject);
        }
    }

    /// <summary>
    /// Alterna entre vista libre (Sims) y top-down (mapa).
    /// Coordina cÃ¡mara, cards, overlays y labels.
    /// </summary>
    public void ToggleCameraMode()
    {
        if (topDownController == null)
        {
            QCLog.Warn("No hay TopDownCameraController configurado.");
            return;
        }

        isInTopDownMode = !isInTopDownMode;

        if (isInTopDownMode)
        {
            // Cambiar a vista top-down
            topDownController.SetTopDownMode();
            ApplyCardsMode(true);
            
            if (overlayPainter != null)
                overlayPainter.SetTopDownMode(true);
            
            if (cameraToggleText != null)
                cameraToggleText.text = "Vista: Mapa";

            NotifyManualLabelsUpdate();
            QCLog.Info("ðŸ—ºï¸ Vista Top-Down activada");
        }
        else
        {
            // Cambiar a vista libre
            topDownController.SetFreeMode();
            ApplyCardsMode(false);
            
            if (overlayPainter != null)
                overlayPainter.SetTopDownMode(false);
            
            if (cameraToggleText != null)
                cameraToggleText.text = "Vista: Libre";

            NotifyManualLabelsUpdate();
            QCLog.Info("ðŸŽ® Vista Libre activada");
        }
    }

    /// <summary>
    /// Cierra el dashboard y retorna cÃ¡mara a home si estÃ¡ en top-down.
    /// </summary>
    public void CloseDashboard()
    {
        dashboard?.HideInterface();

        // Regresar a home solo si estamos en top-down
        if (isInTopDownMode && topDownController != null)
        {
            topDownController.ReturnToStaticHome(0.6f);
        }
    }

    /// <summary>
    /// Obtiene los datos de un Ã¡rea por su clave.
    /// </summary>
    public AreaData GetAreaData(string areaKey)
    {
        return areaDataDict.ContainsKey(areaKey) ? areaDataDict[areaKey] : null;
    }

    /// <summary>
    /// Retorna la lista de GameObjects de Ã¡reas.
    /// </summary>
    public List<GameObject> GetAreaObjects() => areaObjects;

    #endregion

    #region Internal Helpers - Initialization

    /// <summary>
    /// Cachea componentes principales para evitar GetComponent repetidos.
    /// </summary>
    private void CacheComponents()
    {
        cachedMainCamera = Camera.main;
        
        if (cachedMainCamera != null)
        {
            freeCameraController = cachedMainCamera.GetComponent<FreeCameraController>();
        }

        overlayPainter = FindObjectOfType<AreaOverlayPainter>();
        labelsManager = FindObjectOfType<ManualLabelsManager>();

        QCLog.Info("Componentes cacheados en AreaManager");
    }

    /// <summary>
    /// Configura el dashboard y su callback de detalles.
    /// </summary>
    private void SetupDashboard()
    {
        if (dashboard == null)
        {
            dashboard = FindFirstObjectByType<IndustrialDashboard>();
            if (dashboard == null)
            {
                Debug.LogError("No se encontrÃ³ IndustrialDashboard en la escena");
                return;
            }
            QCLog.Info("Dashboard encontrado automÃ¡ticamente");
        }

        // Proveer callback para generar texto de detalles de KPIs
        dashboard.ProvideDetail = (areaDisplayName, kpi) => GenerateDetailText(areaDisplayName, kpi);
    }

    /// <summary>
    /// Setup completo de Ã¡reas: encontrar, corregir posiciones, configurar colliders y cards.
    /// </summary>
    private void SetupAreas()
    {
        // Buscar Ã¡reas automÃ¡ticamente si no hay ninguna asignada
        if (areaObjects.Count == 0)
        {
            FindAreasAutomatically();
        }

        SetupAreaLayers();
        InitializePreciseColliders();

        // Aplicar correcciÃ³n de posiciones si existe el fixer
        var fixer = FindFirstObjectByType<AreaPositionFixerV2>();
        if (fixer != null)
        {
            fixer.FixAreaPositionsAndChildren();
            QCLog.Info("Posiciones de Ã¡reas corregidas antes de registrar");
        }

        RegisterRealAreaPositions();
        SetupCollidersByChildCubes();
        SanitizeAreaColliders();
        DisableLabelRaycastsAndLayer();
        CreateAreaCards();
        
        dashboard.HideInterface();
    }

    /// <summary>
    /// Busca automÃ¡ticamente los GameObjects de Ã¡reas en la escena.
    /// </summary>
    private void FindAreasAutomatically()
    {
        areaObjects.Clear();
        string[] names = { "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1" };
        
        foreach (string n in names)
        {
            var found = GameObject.Find(n);
            if (found != null)
            {
                areaObjects.Add(found);
                QCLog.Info($"âœ“ Ãrea encontrada: {found.name}");
            }
        }
    }

    /// <summary>
    /// Asigna todas las Ã¡reas y sus hijos a la layer "Areas".
    /// </summary>
    private void SetupAreaLayers()
    {
        int areasLayer = LayerMask.NameToLayer("Areas");
        if (areasLayer == -1)
        {
            Debug.LogError("Layer 'Areas' no existe. ConfigÃºrala en Project Settings > Tags and Layers.");
            return;
        }

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            
            foreach (Transform t in areaObj.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = areasLayer;
            }
            
            QCLog.Info($"[Areas] {areaObj.name} asignada a layer 'Areas'");
        }
    }

    /// <summary>
    /// Calcula y registra las posiciones reales (bounds center) de cada Ã¡rea.
    /// Usado para focus preciso de cÃ¡mara.
    /// </summary>
    private void RegisterRealAreaPositions()
    {
        QCLog.Info("=== REGISTRANDO POSICIONES REALES ===");
        areaBoundsByObject.Clear();
        
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            
            string areaKey = GetAreaKey(areaObj.name);
            var bounds = CalculateAreaBounds(areaObj);
            
            realAreaPositions[areaKey] = bounds.center;
            areaBoundsByObject[areaObj] = bounds;
            
            QCLog.Info($"[Bounds] {areaObj.name} (Key: {areaKey}) -> center {bounds.center}");
        }
    }

    /// <summary>
    /// Crea AreaCard component en cada Ã¡rea para UI de labels 3D.
    /// </summary>
    private void CreateAreaCards()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            AreaCard card = areaObj.GetComponent<AreaCard>();
            if (card == null)
            {
                card = areaObj.AddComponent<AreaCard>();
            }

            string areaKey = GetAreaKey(areaObj.name);
            if (areaDataDict.ContainsKey(areaKey))
            {
                var data = areaDataDict[areaKey];
                card.areaName = data.displayName;
            }
            else
            {
                card.areaName = areaObj.name;
                Debug.LogWarning($"No se encontraron datos para el Ã¡rea: {areaObj.name}");
            }

            SetupAreaCollider(areaObj);
        }
    }

    #endregion

    #region Internal Helpers - Top-Down Camera

    private void SetupTopDownCamera()
    {
        if (cachedMainCamera == null)
        {
            Debug.LogError("No hay Main Camera para configurar Top-Down");
            return;
        }

        topDownController = cachedMainCamera.GetComponent<TopDownCameraController>();
        if (topDownController == null)
        {
            topDownController = cachedMainCamera.gameObject.AddComponent<TopDownCameraController>();
        }

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

        QCLog.Info($"[TopDown] Centro {plantCenter} | TamaÃ±o {plantSize} | Padding {fitPadding}");
    }

    private void CollectAreaCardsAuto()
    {
        areaCards.Clear();
        
        foreach (var area in areaObjects)
        {
            if (area == null) continue;
            
            var card = area.GetComponentInChildren<AreaCard>(true);
            if (card != null)
            {
                areaCards.Add(card);
            }
        }
        
        QCLog.Info($"[TopDown] Encontradas {areaCards.Count} AreaCard");
    }

    private Vector3 CalculatePlantCenter()
    {
        if (areaObjects.Count == 0) return Vector3.zero;
        
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        foreach (var area in areaObjects)
        {
            if (area != null)
            {
                sum += area.transform.position;
                count++;
            }
        }
        
        return count > 0 ? sum / count : Vector3.zero;
    }

    private Vector2 CalculatePlantSize()
    {
        if (areaObjects.Count == 0) return new Vector2(100, 100);
        
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var area in areaObjects)
        {
            if (area == null) continue;
            
            Bounds bounds = GetObjectBounds(area);
            minX = Mathf.Min(minX, bounds.min.x);
            maxX = Mathf.Max(maxX, bounds.max.x);
            minZ = Mathf.Min(minZ, bounds.min.z);
            maxZ = Mathf.Max(maxZ, bounds.max.z);
        }
        
        float width = Mathf.Max(50f, maxX - minX);
        float depth = Mathf.Max(50f, maxZ - minZ);
        
        return new Vector2(width, depth);
    }

    private Bounds GetObjectBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.one * 5f);
        }
        
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        
        return combined;
    }

    private void ApplyCardsMode(bool topdown)
    {
        foreach (var card in areaCards)
        {
            if (card == null) continue;
            card.SetTopDownMode(topdown);
        }
    }

    /// <summary>
    /// Notifica a ManualLabelsManager del cambio de modo de cÃ¡mara.
    /// Labels ajustan su visibilidad y comportamiento segÃºn vista libre/top-down.
    /// </summary>
    private void NotifyManualLabelsUpdate()
    {
        if (labelsManager != null)
        {
            labelsManager.SetTopDownMode(isInTopDownMode);
            labelsManager.ForceRefreshAll();
            
            QCLog.Info($"AreaManager: Modo TopDown = {isInTopDownMode} notificado al ManualLabelsManager");
        }
        else
        {
            // Fallback: buscar labels directamente si no hay manager
            var manualLabels = FindObjectsOfType<ManualAreaLabel>();
            foreach (var label in manualLabels)
            {
                if (label != null)
                {
                    label.SetTopDownVisibility(isInTopDownMode);
                }
            }
            
            QCLog.Info($"AreaManager: Fallback usado para {manualLabels.Length} labels");
        }
    }

    #endregion

    #region Internal Helpers - UI Toggle Button

    /// <summary>
    /// Construye el botÃ³n UI para alternar entre vista libre y top-down.
    /// </summary>
    private void BuildCameraToggleButton()
    {
        EnsureEventSystem();
        Canvas canvas = FindOrCreateUICanvas();

        GameObject buttonObj = new GameObject("Btn_ToggleCamera", 
            typeof(RectTransform), 
            typeof(Image), 
            typeof(Button), 
            typeof(Shadow));
        buttonObj.transform.SetParent(canvas.transform, false);

        // Configurar RectTransform
        var rectTransform = buttonObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.sizeDelta = new Vector2(130, 36);
        rectTransform.anchoredPosition = new Vector2(-18, -52);

        // Configurar imagen base
        var image = buttonObj.GetComponent<Image>();
        image.color = Color.white;
        image.sprite = CreateRoundedSprite(16);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        // Configurar sombra sutil
        var shadow = buttonObj.GetComponent<Shadow>();
        shadow.effectDistance = new Vector2(0, -2);
        shadow.effectColor = new Color(0, 0, 0, 0.18f);

        // Configurar botÃ³n y colores
        cameraToggleButton = buttonObj.GetComponent<Button>();
        var colorBlock = cameraToggleButton.colors;
        colorBlock.normalColor = image.color;
        colorBlock.highlightedColor = new Color(0.98f, 0.98f, 1f, 0.98f);
        colorBlock.pressedColor = new Color(0.90f, 0.92f, 0.95f, 0.95f);
        colorBlock.fadeDuration = 0.08f;
        cameraToggleButton.colors = colorBlock;

        // Crear texto del botÃ³n
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(buttonObj.transform, false);
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(120, 30);

        cameraToggleText = textObj.GetComponent<Text>();
        cameraToggleText.text = "Vista: Libre";
        cameraToggleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cameraToggleText.fontSize = 14;
        cameraToggleText.fontStyle = FontStyle.Bold;
        cameraToggleText.color = new Color(0.18f, 0.18f, 0.22f, 1f);
        cameraToggleText.alignment = TextAnchor.MiddleCenter;

        // Conectar evento
        cameraToggleButton.onClick.AddListener(ToggleCameraMode);
    }

    /// <summary>
    /// Crea un sprite con esquinas redondeadas para botones estilo iOS.
    /// </summary>
    private Sprite CreateRoundedSprite(int cornerRadius = 12)
    {
        int width = 64, height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        // Helper local: determina si un pixel estÃ¡ dentro del Ã¡rea redondeada
        bool IsInsideRounded(int x, int y, int w, int h, int r)
        {
            bool isCorner = (x < r && y < r) || 
                           (x >= w - r && y < r) ||
                           (x < r && y >= h - r) || 
                           (x >= w - r && y >= h - r);
            
            if (!isCorner) return true;
            
            Vector2 cornerCenter = (x < r && y < r) ? new Vector2(r, r) :
                                   (x >= w - r && y < r) ? new Vector2(w - r, r) :
                                   (x < r && y >= h - r) ? new Vector2(r, h - r) :
                                                           new Vector2(w - r, h - r);
            
            return Vector2.Distance(new Vector2(x, y), cornerCenter) <= r;
        }

        // Generar pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = IsInsideRounded(x, y, width, height, cornerRadius) 
                    ? Color.white 
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture, 
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 
            100f, 
            0, 
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
        );
    }

    private Canvas FindOrCreateUICanvas()
    {
        var existing = GameObject.Find("UI_Canvas");
        if (existing != null)
        {
            return existing.GetComponent<Canvas>();
        }

        var canvasObj = new GameObject("UI_Canvas", 
            typeof(Canvas), 
            typeof(CanvasScaler), 
            typeof(GraphicRaycaster));
        
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            QCLog.Info("EventSystem creado automÃ¡ticamente para UI");
        }
    }

    #endregion

    #region Internal Helpers - Collider Setup

    /// <summary>
    /// Inicializa datos de colliders precisos con dimensiones reales detectadas.
    /// Estos datos se usan para crear colliders ajustados a los cubos visuales.
    /// </summary>
    private void InitializePreciseColliders()
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

        // VBL1 - cubo principal (60)
        preciseColliders.Add(new PreciseColliderData
        {
            areaKey = "VBL1",
            cubePosition = new Vector3(-0.92f, 1.5f, 146.04f),
            cubeSize = new Vector3(31.30f, 3.00f, 32.30f),
            cubeChildName = "Cube (60)",
            heightOffset = 5f
        });

        QCLog.Info($"[PreciseColliders] Inicializados {preciseColliders.Count} colliders con dimensiones reales");
    }

    /// <summary>
    /// Configura colliders precisos usando datos precalculados en InitializePreciseColliders.
    /// MÃ©todo invocable con F12 para debugging.
    /// </summary>
    private void SetupPreciseAreaColliders()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            PreciseColliderData colliderData = preciseColliders.Find(x => x.areaKey == areaKey);

            if (colliderData == null)
            {
                QCLog.Warn($"[PreciseColliders] No se encontraron datos para {areaKey}");
                continue;
            }

            // Remover colliders existentes
            var existingColliders = areaObj.GetComponents<Collider>();
            for (int i = existingColliders.Length - 1; i >= 0; i--)
            {
                var existing = existingColliders[i];
                if (existing == null) continue;
                
                if (Application.isPlaying)
                    Destroy(existing);
                else
                    DestroyImmediate(existing);
            }

            // Crear collider preciso
            BoxCollider preciseCollider = areaObj.AddComponent<BoxCollider>();
            Vector3 localCenter = areaObj.transform.InverseTransformPoint(colliderData.cubePosition);
            preciseCollider.center = localCenter;

            Vector3 localSize = colliderData.cubeSize;
            localSize.y = colliderData.heightOffset;
            preciseCollider.size = localSize;
            preciseCollider.isTrigger = false;

            QCLog.Info($"[PreciseColliders] {areaKey}: Centro local {localCenter}, TamaÃ±o {localSize}");
        }
    }

    /// <summary>
    /// MÃ©todo mejorado que soporta Ã¡reas con mÃºltiples cubos (caso especial VBL1).
    /// Invocable con F11 para debugging.
    /// </summary>
    private void SetupMultipleCubeColliders()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);

            // Remover colliders existentes del Ã¡rea
            var existingColliders = areaObj.GetComponents<Collider>();
            foreach (var col in existingColliders)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }

            // VBL1 necesita mÃºltiples colliders
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

    /// <summary>
    /// Configura los 3 colliders separados para VBL1 (Cubes 60, 61, 62).
    /// </summary>
    private void SetupVBL1MultipleColliders(GameObject areaObj)
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
            if (areasLayer != -1)
            {
                colliderObj.layer = areasLayer;
            }

            QCLog.Info($"[VBL1] Collider {cubeData.name}: Centro {cubeData.center}, TamaÃ±o {cubeData.size}");
        }
    }

    /// <summary>
    /// Configura un collider Ãºnico para Ã¡reas estÃ¡ndar (ATHONDA, VCTL4, BUZZERL2).
    /// </summary>
    private void SetupSingleCubeCollider(GameObject areaObj, string areaKey)
    {
        PreciseColliderData colliderData = preciseColliders.Find(x => x.areaKey == areaKey);
        if (colliderData == null)
        {
            QCLog.Warn($"[SingleCube] No hay datos para {areaKey}");
            return;
        }

        var box = areaObj.AddComponent<BoxCollider>();

        Vector3 localCenter = areaObj.transform.InverseTransformPoint(colliderData.cubePosition);
        box.center = localCenter;

        Vector3 size = colliderData.cubeSize;
        size.y = colliderData.heightOffset;

        // Compensar por escala del objeto
        Vector3 lossyScale = areaObj.transform.lossyScale;
        box.size = new Vector3(
            size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
            size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
            size.z / Mathf.Max(0.001f, Mathf.Abs(lossyScale.z))
        );

        QCLog.Info($"[SingleCube] {areaKey}: Centro local {box.center}, TamaÃ±o {box.size}");
    }

    /// <summary>
    /// Configura colliders basÃ¡ndose en los bounds de cubos hijos especÃ­ficos.
    /// MÃ©todo alternativo que detecta automÃ¡ticamente dimensiones.
    /// </summary>
    private void SetupCollidersByChildCubes()
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
                QCLog.Warn($"[ChildCube] No se encontrÃ³ {colliderData.cubeChildName} en {areaObj.name}");
                continue;
            }

            Renderer cubeRenderer = cubeChild.GetComponent<Renderer>();
            if (cubeRenderer == null)
            {
                QCLog.Warn($"[ChildCube] No hay Renderer en {colliderData.cubeChildName}");
                continue;
            }

            // Remover colliders existentes
            var existingColliders = areaObj.GetComponents<Collider>();
            for (int i = existingColliders.Length - 1; i >= 0; i--)
            {
                var existing = existingColliders[i];
                if (existing == null) continue;
                
                if (Application.isPlaying)
                    Destroy(existing);
                else
                    DestroyImmediate(existing);
            }

            // Crear collider basado en bounds del cubo hijo
            BoxCollider box = areaObj.AddComponent<BoxCollider>();
            Bounds cubeBounds = cubeRenderer.bounds;

            box.center = areaObj.transform.InverseTransformPoint(cubeBounds.center);

            Vector3 size = cubeBounds.size;
            size.y = Mathf.Max(size.y, colliderData.heightOffset);

            // Compensar por escala
            Vector3 lossyScale = areaObj.transform.lossyScale;
            box.size = new Vector3(
                size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
                size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
                size.z / Mathf.Max(0.001f, Mathf.Abs(lossyScale.z))
            );

            QCLog.Info($"[ChildCube] {areaKey}: Collider basado en {colliderData.cubeChildName}");
        }
    }

    /// <summary>
    /// Normaliza colliders: asegura que no sean trigger, estÃ©n en layer correcta,
    /// y tengan tamaÃ±os vÃ¡lidos (no negativos).
    /// </summary>
    private void SanitizeAreaColliders()
    {
        int areasLayer = LayerMask.NameToLayer("Areas");
        
        foreach (var areaObj in areaObjects)
        {
            if (!areaObj) continue;

            foreach (var col in areaObj.GetComponentsInChildren<Collider>(true))
            {
                col.isTrigger = false; // Click sÃ³lido
                col.gameObject.layer = areasLayer;

                var box = col as BoxCollider;
                if (box != null)
                {
                    var size = box.size;
                    // CRÃTICO: nunca permitir tamaÃ±os negativos + altura mÃ­nima
                    box.size = new Vector3(
                        Mathf.Abs(size.x), 
                        Mathf.Max(1f, Mathf.Abs(size.y)), 
                        Mathf.Abs(size.z)
                    );
                }
            }
        }
    }

    /// <summary>
    /// Desactiva raycast en labels para que no bloqueen clicks a Ã¡reas 3D.
    /// </summary>
    private void DisableLabelRaycastsAndLayer()
    {
        var canvases = FindObjectsOfType<Canvas>(true);
        
        foreach (var canvas in canvases)
        {
            if (canvas.GetComponentInParent<ManualAreaLabel>() != null)
            {
                canvas.gameObject.layer = LayerMask.NameToLayer("Default");
                
                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }
        }
    }

    /// <summary>
    /// Configura collider bÃ¡sico para un Ã¡rea si no existe ninguno.
    /// Fallback usado en CreateAreaCards.
    /// </summary>
    private void SetupAreaCollider(GameObject areaObj)
    {
        if (areaObj.GetComponent<Collider>() != null) return;

        BoxCollider box = areaObj.AddComponent<BoxCollider>();
        Renderer[] renderers = areaObj.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            
            box.center = areaObj.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
        }
        else
        {
            box.size = Vector3.one * 10f;
            box.center = Vector3.up * 2.5f;
        }
        
        QCLog.Info($"BoxCollider agregado a: {areaObj.name}");
    }

    #endregion

    #region Internal Helpers - Click Handling

    /// <summary>
    /// Detecta clicks en Ã¡reas 3D usando raycast.
    /// Prioriza colliders fÃ­sicos (precisiÃ³n), luego bounds de renderers (fallback).
    /// </summary>
    private void HandleAreaClickSimplified()
    {
        if (cachedMainCamera == null) return;

        Ray ray = cachedMainCamera.ScreenPointToRay(Input.mousePosition);
        int layerMask = LayerMask.GetMask("Areas");

        // 1) Raycast a colliders primero (mÃ©todo mÃ¡s preciso)
        var hits = Physics.RaycastAll(ray, 2000f, layerMask);
        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            foreach (var hit in hits)
            {
                var area = FindAreaForGameObject(hit.collider.gameObject);
                if (area != null)
                {
                    QCLog.Info($"[PHY] Hit {hit.collider.name} -> {area.name} d={hit.distance:F2}");
                    OnAreaClicked(area);
                    return;
                }
            }
        }

        // 2) Fallback a bounds por renderers (por si falta algÃºn collider)
        if (areaBoundsByObject != null && areaBoundsByObject.Count > 0)
        {
            GameObject bestArea = null;
            float bestDistance = float.MaxValue;

            foreach (var kvp in areaBoundsByObject)
            {
                var bounds = kvp.Value;
                if (bounds.IntersectRay(ray, out float distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestArea = kvp.Key;
                }
            }

            if (bestArea != null)
            {
                QCLog.Info($"[BND] {bestArea.name} d={bestDistance:F2}");
                OnAreaClicked(bestArea);
            }
        }
    }

    /// <summary>
    /// Verifica si el cursor estÃ¡ sobre UI que debe bloquear clicks al mundo 3D.
    /// </summary>
    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null) return false;

        var eventData = new PointerEventData(EventSystem.current) 
        { 
            position = Input.mousePosition 
        };
        
        raycastResultsCache.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResultsCache);

        // Si HAY cualquier Graphic con raycastTarget activo bajo el cursor, bloquear
        foreach (var result in raycastResultsCache)
        {
            var graphic = result.gameObject.GetComponentInParent<Graphic>();
            if (graphic != null && graphic.raycastTarget) return true;
            
            if (result.gameObject.GetComponentInParent<Button>() != null) return true;
            if (result.gameObject.GetComponentInParent<Scrollbar>() != null) return true;
        }
        
        return false;
    }

    /// <summary>
    /// Encuentra el GameObject de Ã¡rea padre para un GameObject clickeado.
    /// </summary>
    private GameObject FindAreaForGameObject(GameObject clickedObj)
    {
        if (clickedObj == null) return null;

        // Buscar directamente en lista
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            if (areaObj == clickedObj) return areaObj;
        }

        // Buscar en jerarquÃ­a padre
        Transform current = clickedObj.transform;
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

    private Bounds CalculateAreaBounds(GameObject areaObj)
    {
        var renderers = areaObj.GetComponentsInChildren<Renderer>();
        
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(areaObj.transform.position, new Vector3(4f, 2f, 4f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        
        return bounds;
    }

    #endregion

    #region Internal Helpers - Data Generation

    /// <summary>
    /// Genera texto detallado de KPI para dashboard segÃºn Ã¡rea y mÃ©trica.
    /// Incluye contexto operacional especÃ­fico por tipo de KPI.
    /// </summary>
    private string GenerateDetailText(string areaDisplayName, KPIData kpi)
    {
        // Buscar Ã¡rea por displayName
        string areaKey = null;
        foreach (var kvp in areaDataDict)
        {
            if (string.Equals(kvp.Value.displayName, areaDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                areaKey = kvp.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(areaKey) || !areaDataDict.ContainsKey(areaKey))
        {
            string unit = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
            return $"Detalle de {kpi.name}\nÃrea: {areaDisplayName}\nActual: {kpi.value:F1}{unit}";
        }

        var data = areaDataDict[areaKey];
        string kpiName = (kpi.name ?? "").ToLowerInvariant();

        // Contexto especÃ­fico por tipo de KPI
        if (kpiName.Contains("delivery"))
        {
            return $"Delivery â€“ {data.displayName}\n" +
                   $"Actual: {data.delivery:F1}%\n" +
                   $"ðŸ“¦ Ã“rdenes planificadas: {GetEstOrders(data.delivery)}\n" +
                   $"âš  Incumplimientos: {GetIncidences(data.delivery)}\n" +
                   $"â± Retraso promedio: {GetDelayMins(data.delivery)} min\n" +
                   $"AcciÃ³n: asegurar JIT, balanceo de lÃ­nea y seguimiento de transporte.";
        }

        if (kpiName.Contains("quality"))
        {
            return $"Quality â€“ {data.displayName}\n" +
                   $"Actual: {data.quality:F1}%\n" +
                   $"ðŸ“Š PPM estimado: {GetPpm(data.quality)}\n" +
                   $"ðŸ” Top defectos: {GetTopDefects()}\n" +
                   $"ðŸ”§ Retrabajos/dÃ­a: {GetReworks(data.quality)}\n" +
                   $"AcciÃ³n: Gemba + 5-Why sobre el defecto principal; contenciÃ³n si PPM > objetivo.";
        }

        // GenÃ©rico para otros KPIs
        string unit2 = string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit;
        return $"Detalle de {kpi.name}\nÃrea: {data.displayName}\nActual: {kpi.value:F1}{unit2}";
    }

    // Helpers de contexto operacional
    private int GetEstOrders(float delivery) => 
        Mathf.Clamp(Mathf.RoundToInt(50f * (delivery / 100f) + 5), 5, 60);
    
    private int GetIncidences(float delivery) => 
        delivery < 50 ? 5 : (delivery < 80 ? 2 : 0);
    
    private int GetDelayMins(float delivery) => 
        delivery < 50 ? 35 : (delivery < 80 ? 12 : 3);
    
    private int GetPpm(float quality) => 
        Mathf.Clamp(Mathf.RoundToInt((100f - quality) * 120f), 0, 12000);
    
    private string GetTopDefects() => "Faltante, CosmÃ©tico, Torque";
    
    private int GetReworks(float quality) => 
        Mathf.Clamp(Mathf.RoundToInt((100f - quality) / 5f), 0, 6);

    /// <summary>
    /// Genera predicciones/alertas basadas en mÃ©tricas del Ã¡rea.
    /// Usado para mostrar avisos contextuales en dashboard.
    /// </summary>
    private List<string> GeneratePredictions(AreaData data)
    {
        List<string> predictions = new List<string>();

        if (data.delivery < 50)
            predictions.Add("ðŸš¨ CRÃTICO: Problemas severos de entrega detectados");
        else if (data.delivery < 80)
            predictions.Add("âš ï¸ Delivery bajo riesgo - OptimizaciÃ³n recomendada");

        if (data.quality < 70)
            predictions.Add("ðŸ” Control de calidad requiere intervenciÃ³n");

        if (data.trainingDNA < 70)
            predictions.Add("ðŸ“š Personal requiere capacitaciÃ³n urgente");

        if (data.overallResult < 50)
            predictions.Add("ðŸ”´ ZONA ROJA: IntervenciÃ³n ejecutiva inmediata");
        else if (data.overallResult >= 90)
            predictions.Add("ðŸŸ¢ ZONA OPTIMUS: Benchmark para otras Ã¡reas");

        return predictions;
    }

    /// <summary>
    /// Convierte nombre de GameObject a clave interna de Ã¡rea.
    /// </summary>
    private string GetAreaKey(string objectName)
    {
        string upper = (objectName ?? "").ToUpperInvariant();
        
        if (upper == "AREA_ATHONDA" || upper.Contains("ATHONDA") || upper.Contains("AT HONDA"))
            return "ATHONDA";
        
        if (upper == "AREA_VCTL4" || upper.Contains("VCTL4") || upper.Contains("VCT L4"))
            return "VCTL4";
        
        if (upper == "AREA_BUZZERL2" || upper.Contains("BUZZERL2") || upper.Contains("BUZZER L2"))
            return "BUZZERL2";
        
        if (upper == "AREA_VBL1" || upper.Contains("VBL1") || upper.Contains("VB L1") || 
            (upper.Contains("VB") && upper.Contains("L1")))
            return "VBL1";
        
        return upper.Replace("AREA_", "");
    }

    /// <summary>
    /// Inicializa datos estÃ¡ticos de todas las Ã¡reas.
    /// Integra AppleTheme para colores consistentes.
    /// </summary>
    private void InitializeAreaData()
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
            statusColor = AppleTheme.Status(95f) // Usa AppleTheme
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
            statusColor = AppleTheme.Status(92f)
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
            statusColor = AppleTheme.Status(73f)
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
            statusColor = AppleTheme.Status(49f)
        };
    }

    #endregion

    #region Debug

    /// <summary>
    /// [F8] Muestra posiciones actuales vs registradas de todas las Ã¡reas.
    /// </summary>
    [ContextMenu("Debug Posiciones Actuales")]
    private void DebugCurrentPositions()
    {
        Debug.Log("=== POSICIONES ACTUALES DE ÃREAS ===");
        
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            
            string areaKey = GetAreaKey(areaObj.name);
            realAreaPositions.TryGetValue(areaKey, out var registeredPos);
            
            Vector3 currentPos = areaObj.transform.position;
            float diff = Vector3.Distance(currentPos, registeredPos);
            string layerName = LayerMask.LayerToName(areaObj.layer);
            
            Debug.Log($"- {areaObj.name} (Key: {areaKey})\n" +
                     $"   Actual: {currentPos}\n" +
                     $"   Registrada: {registeredPos}\n" +
                     $"   Diferencia: {diff:F2}\n" +
                     $"   Layer: {layerName}");
        }
    }

    /// <summary>
    /// [F10] Detecta y muestra dimensiones reales de cubos hijos.
    /// Ãštil para ajustar InitializePreciseColliders.
    /// </summary>
    [ContextMenu("Detectar Dimensiones Reales")]
    private void DetectRealCubeDimensions()
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
                        Debug.Log($"[Detect]   Bounds TamaÃ±o: {bounds.size}");
                        Debug.Log($"[Detect]   Transform Scale: {child.localScale}");
                    }
                }
            }
        }

        Debug.Log("=== USA ESTOS DATOS PARA AJUSTAR preciseColliders ===");
    }

    /// <summary>
    /// [I] Muestra informaciÃ³n bÃ¡sica de todas las Ã¡reas.
    /// </summary>
    private void ShowAreaDebugInfo()
    {
        Debug.Log("=== INFO DE ÃREAS ===");
        
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;
            
            string key = GetAreaKey(areaObj.name);
            Debug.Log($"Ãrea: {areaObj.name} (Key: {key}) - PosiciÃ³n: {areaObj.transform.position}");
        }
    }

    #endregion
}
