using UnityEngine;
using TMPro;

/// <summary>
/// Textos manuales que se ven solo en la vista MAPA (top-down fija).
/// VERSIÓN CORREGIDA para consistencia Editor/Runtime
/// </summary>
public class ManualAreaLabel : MonoBehaviour
{
    [Header("Referencias de Textos")]
    [Tooltip("TextMeshProUGUI del nombre")]
    public TextMeshProUGUI nameText;
    [Tooltip("TextMeshProUGUI del porcentaje")]
    public TextMeshProUGUI percentText;

    [Header("Configuración")]
    [Tooltip("Clave del área (ATHONDA, VCTL4, BUZZERL2, VBL1). Se auto-detecta si está vacío.")]
    public string areaKey = "ATHONDA";
    [Tooltip("Forzar nombre personalizado")]
    public bool useCustomName = false;
    public string customNameText = "AT HONDA";

    [Header("Vista")]
    [Tooltip("Solo mostrar en vista top-down fija")]
    public bool onlyShowInStaticTopDown = true;

    [Header("Colores de Texto")]
    [Tooltip("Forzar texto negro")]
    public bool keepTextBlack = true;

    [Header("Canvas Settings - CLAVE PARA CONSISTENCIA")]
    [Tooltip("Altura Y fija para el label")]
    public float labelHeightY = 0.25f;
    [Tooltip("Escala base del canvas (ajustar según necesidad)")]
    public float canvasBaseScale = 1.0f;
    [Tooltip("Escala específica para vista mapa")]
    public float mapModeScale = 1.2f;
    [Tooltip("Orden de sorting")]
    public int canvasSortingOrder = 5000;

    [Header("🔧 CORRECCIÓN DE POSICIÓN")]
    [Tooltip("Offset en X para ajustar posición final")]
    public float positionOffsetX = 0f;
    [Tooltip("Offset en Z para ajustar posición final")]
    public float positionOffsetZ = 0f;
    [Tooltip("Forzar recalculo de posición en cada frame")]
    public bool continuousPositionUpdate = true;

    [Header("Autodetección")]
    [Tooltip("Detectar área por jerarquía")]
    public bool autoDetectAreaFromHierarchy = true;

    [Header("Debug")]
    public bool enableDebug = false;

    // Internos
    private AreaManager areaManager;
    private TopDownCameraController topDownController;
    private bool currentlyVisible = false;
    private float lastOverallResult = -1f;
    private bool isRegistered = false;
    private Canvas cachedCanvas;
    private Vector3 originalWorldPosition;
    private bool hasBeenPositioned = false;

    void OnValidate()
    {
        // Guardar posición solo si no está en play mode
        if (!Application.isPlaying)
        {
            originalWorldPosition = transform.position;
        }
    }

    void Awake()
    {
        // Autodetectar clave
        if (autoDetectAreaFromHierarchy)
        {
            string detected = DetectAreaKeyFromParents();
            if (!string.IsNullOrEmpty(detected))
            {
                areaKey = detected;
            }
        }

        // Guardar posición original
        originalWorldPosition = transform.position;

        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Awake - areaKey = {areaKey}, pos = {originalWorldPosition}");
    }

    void Start()
    {
        ManualLabelsManager.Register(this);
        isRegistered = true;

        areaManager = FindObjectOfType<AreaManager>();
        var mainCamera = Camera.main ?? FindObjectOfType<Camera>();
        if (mainCamera != null) topDownController = mainCamera.GetComponent<TopDownCameraController>();

        // Setup crítico del canvas
        SetupCanvasForConsistency();

        // Colores iniciales
        if (keepTextBlack)
        {
            if (nameText) nameText.color = Color.black;
            if (percentText) percentText.color = Color.black;
        }

        // Nombre personalizado
        if (useCustomName && nameText && !string.IsNullOrEmpty(customNameText))
            nameText.text = customNameText;

        // Estado inicial
        SetVisibility(false);
        UpdateTexts();

        if (enableDebug)
        {
            Debug.Log($"[ManualAreaLabel:{name}] Start completado");
        }
    }

    void Update()
    {
        // 🔧 CORRECCIÓN CLAVE: Asegurar cámara y posición consistente
        EnsureCanvasConsistency();

        // Actualizar visibilidad
        bool shouldShow = ShouldShowTexts();
        if (shouldShow != currentlyVisible)
        {
            currentlyVisible = shouldShow;
            SetVisibility(currentlyVisible);
            if (currentlyVisible) UpdateTexts();

            if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Visibilidad: {shouldShow}");
        }

        // Actualizar datos si cambian
        if (currentlyVisible)
        {
            var data = GetAreaData();
            if (data != null && data.overallResult != lastOverallResult)
            {
                UpdatePercentage();
                lastOverallResult = data.overallResult;
            }
        }

        // 🔧 Actualización continua de posición si está habilitada
        if (continuousPositionUpdate && currentlyVisible)
        {
            UpdateCanvasPosition();
        }
    }

    // ========== MÉTODOS CORREGIDOS ==========

    void SetupCanvasForConsistency()
    {
        cachedCanvas = GetComponentInChildren<Canvas>(true);
        if (!cachedCanvas)
        {
            if (enableDebug) Debug.LogError($"[ManualAreaLabel:{name}] No se encontró Canvas hijo!");
            return;
        }

        // Configuración básica
        cachedCanvas.renderMode = RenderMode.WorldSpace;
        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = canvasSortingOrder;

        // Asegurar cámara inmediatamente
        AssignCameraToCanvas();

        // Posición inicial
        UpdateCanvasPosition();

        // Escala inicial
        UpdateCanvasScale();

        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Canvas configurado - Camera: {cachedCanvas.worldCamera?.name}");
    }

    void EnsureCanvasConsistency()
    {
        if (!cachedCanvas) return;

        // Verificar y reasignar cámara si es necesario
        if (cachedCanvas.renderMode == RenderMode.WorldSpace)
        {
            if (cachedCanvas.worldCamera == null)
            {
                AssignCameraToCanvas();
            }
        }
    }

    void AssignCameraToCanvas()
    {
        if (!cachedCanvas) return;

        Camera targetCamera = null;

        // Prioridad: Camera.main > FindObjectOfType<Camera>
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            cachedCanvas.worldCamera = targetCamera;
            if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Cámara asignada: {targetCamera.name}");
        }
        else
        {
            if (enableDebug) Debug.LogWarning($"[ManualAreaLabel:{name}] No se encontró cámara!");
        }
    }

    void UpdateCanvasPosition()
    {
        if (!cachedCanvas) return;

        // Posicionar el GameObject base (para referencias)
        Vector3 finalPosition = originalWorldPosition;
        finalPosition.x += positionOffsetX;
        finalPosition.y = labelHeightY;
        finalPosition.z += positionOffsetZ;
        transform.position = finalPosition;

        // Resetear Canvas local
        cachedCanvas.transform.localPosition = Vector3.zero;
    }

    void UpdateCanvasScale()
    {
        if (!cachedCanvas) return;

        bool isInMapMode = ShouldShowTexts() && currentlyVisible;
        float targetScale = isInMapMode ? (canvasBaseScale * mapModeScale) : canvasBaseScale;

        var rt = cachedCanvas.transform as RectTransform;
        if (rt != null)
        {
            rt.localScale = Vector3.one * targetScale;
        }
    }

    void CenterChildTexts()
    {
        if (nameText)
        {
            var rt = nameText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
        }
        if (percentText)
        {
            var rt = percentText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
        }
    }

    // ========== RESTO DE MÉTODOS IGUAL ==========

    bool ShouldShowTexts()
    {
        if (!onlyShowInStaticTopDown) return true;

        var mgr = FindObjectOfType<ManualLabelsManager>();
        bool managerSaysTopDown = (mgr != null) && mgr.GetCurrentTopDownMode();

        bool topDownOk = false;
        bool staticOk = false;

        if (topDownController != null)
        {
            topDownOk = (topDownController.GetCurrentMode() == TopDownCameraController.CameraMode.TopDown);
            staticOk = topDownController.IsUsingFixedStaticView();
        }

        return managerSaysTopDown || (topDownOk && staticOk);
    }

    void SetVisibility(bool visible)
    {
        if (nameText) nameText.gameObject.SetActive(visible);
        if (percentText) percentText.gameObject.SetActive(visible);

        if (visible)
        {
            UpdateCanvasScale();
            EnsureCanvasConsistency();
        }

        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] SetVisibility({visible})");
    }

    void UpdateTexts()
    {
        UpdateName();
        UpdatePercentage();
    }

    void UpdateName()
    {
        if (!nameText) return;

        if (useCustomName && !string.IsNullOrEmpty(customNameText))
        {
            nameText.text = customNameText;
        }
        else
        {
            var data = GetAreaData();
            if (data != null) nameText.text = data.displayName;
        }

        if (keepTextBlack) nameText.color = Color.black;
    }

    void UpdatePercentage()
    {
        if (!percentText) return;

        var data = GetAreaData();
        if (data != null)
        {
            percentText.text = $"{data.overallResult:F0}%";
            if (keepTextBlack) percentText.color = Color.black;
        }
        else
        {
            percentText.text = "0%";
            if (keepTextBlack) percentText.color = Color.black;
        }
    }

    AreaManager.AreaData GetAreaData()
    {
        if (!areaManager) return null;
        string normalizedKey = NormalizeAreaKey(areaKey);
        return areaManager.GetAreaData(normalizedKey);
    }

    string NormalizeAreaKey(string key)
    {
        string upper = (key ?? "").ToUpperInvariant();
        upper = upper.Replace("AREA_", "").Replace(" ", "").Replace("_", "");

        if (upper.Contains("ATHONDA") || upper == "ATHONDA") return "ATHONDA";
        if (upper.Contains("VCTL4") || upper == "VCTL4") return "VCTL4";
        if (upper.Contains("BUZZERL2") || upper == "BUZZERL2") return "BUZZERL2";
        if (upper.Contains("VBL1") || upper == "VBL1") return "VBL1";
        return upper;
    }

    string DetectAreaKeyFromParents()
    {
        Transform t = transform;
        while (t != null)
        {
            string n = t.name.ToUpperInvariant();
            if (n.StartsWith("AREA_"))
            {
                string k = n.Substring(5);
                k = k.Replace(" ", "").Replace("_", "");
                if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Detectado: {k}");
                return NormalizeAreaKey(k);
            }
            t = t.parent;
        }
        return null;
    }

    // API para manager
    public void ForceRefresh()
    {
        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] ForceRefresh()");
        UpdateTexts();
        UpdateCanvasPosition();
    }

    public void SetTopDownVisibility(bool visible)
    {
        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] SetTopDownVisibility({visible})");
        bool finalVisible = visible && ShouldShowTexts();
        SetVisibility(finalVisible);
        if (finalVisible) UpdateTexts();
    }

    void OnDestroy()
    {
        if (isRegistered) ManualLabelsManager.Unregister(this);
        isRegistered = false;
    }
}