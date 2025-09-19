using UnityEngine;
using TMPro;

/// <summary>
/// Textos manuales que se ven solo en la vista MAPA (top-down fija).
/// Colócalo en el GameObject padre que contiene NameText y PercentText.
/// </summary>
public class ManualAreaLabel : MonoBehaviour
{
    [Header("Referencias de Textos")]
    [Tooltip("TextMeshProUGUI del nombre")]
    public TextMeshProUGUI nameText;
    [Tooltip("TextMeshProUGUI del porcentaje")]
    public TextMeshProUGUI percentText;

    [Header("Configuración")]
    [Tooltip("Clave del área (ATHONDA, VCTL4, BUZZERL2, VBL1). Se auto-detecta si está vacío o si se habilita autoDetect.")]
    public string areaKey = "ATHONDA";
    [Tooltip("Forzar nombre personalizado (ignora el AreaManager)")]
    public bool useCustomName = false;
    public string customNameText = "AT HONDA";

    [Header("Vista")]
    [Tooltip("Solo mostrar en vista top-down fija (MAPA)")]
    public bool onlyShowInStaticTopDown = true;

    [Header("Colores de Texto")]
    [Tooltip("Forzar texto negro")]
    public bool keepTextBlack = true;

    [Header("World Space / Orden")]
    public bool forceCanvasSetup = true;
    public int canvasSortingOrder = 5000;
    public float labelHeightY = 0.25f;
    public float mapScale = 1.0f;

    [Header("Autodetección")]
    [Tooltip("Si está activo, intenta deducir la clave del área buscando el ancestro con nombre Area_* (e.g., Area_VCTL4).")]
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

    void Awake()
    {
        // Autodetectar clave ANTES de cualquier uso
        if (autoDetectAreaFromHierarchy)
        {
            string detected = DetectAreaKeyFromParents();
            if (!string.IsNullOrEmpty(detected))
            {
                areaKey = detected; // siempre preferimos la jerarquía
            }
        }
        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] areaKey inicial = {areaKey}");
    }

    void Start()
    {
        ManualLabelsManager.Register(this);
        isRegistered = true;

        areaManager = FindObjectOfType<AreaManager>();
        var mainCamera = Camera.main;
        if (mainCamera != null) topDownController = mainCamera.GetComponent<TopDownCameraController>();

        if (forceCanvasSetup) SetupChildCanvas();

        if (keepTextBlack)
        {
            if (nameText) nameText.color = Color.black;
            if (percentText) percentText.color = Color.black;
        }

        // Si el usuario pidió nombre custom, se muestra de inicio
        if (useCustomName && nameText && !string.IsNullOrEmpty(customNameText))
            nameText.text = customNameText;

        // Ocultar de inicio; se activará al entrar a MAPA
        SetVisibility(false);

        // Datos iniciales
        UpdateTexts();

        if (enableDebug)
        {
            var d = GetAreaData();
            string nkey = NormalizeAreaKey(areaKey);
            Debug.Log($"[ManualAreaLabel:{name}] NormalizedKey={nkey} | Data={(d != null ? d.displayName : "NULL")}");
        }
    }

    void Update()
    {
        bool shouldShow = ShouldShowTexts();

        if (shouldShow != currentlyVisible)
        {
            if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Visible {currentlyVisible} → {shouldShow}");
            currentlyVisible = shouldShow;
            SetVisibility(currentlyVisible);
            if (currentlyVisible) UpdateTexts();
        }

        if (currentlyVisible)
        {
            var data = GetAreaData();
            if (data != null && data.overallResult != lastOverallResult)
            {
                UpdatePercentage();
                lastOverallResult = data.overallResult;
            }
        }
    }

    // ---------- Visibilidad ----------
    bool ShouldShowTexts()
    {
        if (!onlyShowInStaticTopDown) return true;
        if (!topDownController)
        {
            if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] TopDownController null");
            return false;
        }

        bool isTopDown = topDownController.GetCurrentMode() == TopDownCameraController.CameraMode.TopDown;
        bool isStatic = topDownController.IsUsingFixedStaticView();
        return isTopDown && isStatic;
    }

    void SetVisibility(bool visible)
    {
        if (nameText) nameText.gameObject.SetActive(visible);
        if (percentText) percentText.gameObject.SetActive(visible);

        if (visible && cachedCanvas != null && topDownController != null && topDownController.IsUsingFixedStaticView())
        {
            var rt = cachedCanvas.transform as RectTransform;
            if (rt != null) rt.localScale = Vector3.one * Mathf.Max(0.001f, mapScale);
        }

        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] SetVisibility({visible})");
    }

    // ---------- Canvas ----------
    void SetupChildCanvas()
    {
        cachedCanvas = GetComponentInChildren<Canvas>(true);
        if (!cachedCanvas)
        {
            if (enableDebug) Debug.LogWarning($"[ManualAreaLabel:{name}] No se encontró Canvas hijo.");
            return;
        }
        cachedCanvas.renderMode = RenderMode.WorldSpace;
        cachedCanvas.worldCamera = Camera.main;
        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = canvasSortingOrder;

        var p = transform.position;
        transform.position = new Vector3(p.x, labelHeightY, p.z);
    }

    // ---------- Textos ----------
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

    // ---------- Datos ----------
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

    // Detección por jerarquía: busca el ancestro con nombre "Area_*"
    string DetectAreaKeyFromParents()
    {
        Transform t = transform;
        while (t != null)
        {
            string n = t.name.ToUpperInvariant();
            if (n.StartsWith("AREA_"))
            {
                // e.g., Area_VCTL4 → VCTL4
                string k = n.Substring(5);
                k = k.Replace(" ", "").Replace("_", "");
                if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] Detectado por jerarquía: {k}");
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
    }

    public void SetTopDownVisibility(bool visible)
    {
        if (enableDebug) Debug.Log($"[ManualAreaLabel:{name}] SetTopDownVisibility({visible})");
        SetVisibility(visible);
        if (visible) UpdateTexts();
    }

    void OnDestroy()
    {
        if (isRegistered) ManualLabelsManager.Unregister(this);
    }

    void OnValidate()
    {
        if (Application.isPlaying && currentlyVisible) UpdateTexts();
    }
}
