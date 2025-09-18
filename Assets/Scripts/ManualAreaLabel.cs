using UnityEngine;
using TMPro;

public class ManualAreaLabel : MonoBehaviour
{
    [Header("Referencias de Textos")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI percentText;

    [Header("Configuración")]
    public string areaKey = "ATHONDA";
    public bool useCustomName = false;
    public string customNameText = "AT HONDA";

    [Header("Vista")]
    public bool onlyShowInStaticTopDown = true;

    [Header("Colores de Texto")]
    public bool keepTextBlack = true;

    [Header("World Space / Orden")]
    [Tooltip("Si se encuentra un Canvas en hijos, se forzará WorldSpace, cámara principal y orden alto.")]
    public bool forceCanvasSetup = true;
    [Tooltip("OrderInLayer alto para que se vea por encima del mapa.")]
    public int canvasSortingOrder = 5000;
    [Tooltip("Altura Y para no quedar pegado al plano.")]
    public float labelHeightY = 0.25f;
    [Tooltip("Escala base del Canvas para MAPA (útil si se ve muy pequeño en Game).")]
    public float mapScale = 1.0f;

    [Header("Debug")]
    public bool enableDebug = false;

    private AreaManager areaManager;
    private TopDownCameraController topDownController;
    private bool currentlyVisible = false;
    private float lastOverallResult = -1f;
    private bool isRegistered = false;
    private Canvas cachedCanvas;

    void Start()
    {
        ManualLabelsManager.Register(this);
        isRegistered = true;

        areaManager = FindObjectOfType<AreaManager>();
        var mainCamera = Camera.main;
        if (mainCamera != null) topDownController = mainCamera.GetComponent<TopDownCameraController>();

        if (forceCanvasSetup)
            SetupChildCanvas();

        if (keepTextBlack)
        {
            if (nameText) nameText.color = Color.black;
            if (percentText) percentText.color = Color.black;
        }

        if (useCustomName && nameText && !string.IsNullOrEmpty(customNameText))
            nameText.text = customNameText;

        SetVisibility(false);
        UpdateTexts();
    }

    void SetupChildCanvas()
    {
        // Busca un canvas en hijos
        cachedCanvas = GetComponentInChildren<Canvas>(true);
        if (cachedCanvas == null)
        {
            if (enableDebug) Debug.LogWarning($"[ManualAreaLabel] {name}: No se encontró Canvas hijo.");
            return;
        }

        cachedCanvas.renderMode = RenderMode.WorldSpace;
        cachedCanvas.worldCamera = Camera.main;
        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = canvasSortingOrder;

        // Sube un poquito el GameObject padre (donde está este script)
        var p = transform.position;
        transform.position = new Vector3(p.x, labelHeightY, p.z);

        // Escala en MAPA (aplicamos cuando esté visible en MAPA)
    }

    void Update()
    {
        bool shouldShow = ShouldShowTexts();

        if (shouldShow != currentlyVisible)
        {
            if (enableDebug) Debug.Log($"[ManualAreaLabel {name}] Visible {currentlyVisible} → {shouldShow}");
            currentlyVisible = shouldShow;
            SetVisibility(currentlyVisible);

            if (currentlyVisible) UpdateTexts();
        }

        if (currentlyVisible)
        {
            var areaData = GetAreaData();
            if (areaData != null && areaData.overallResult != lastOverallResult)
            {
                UpdatePercentage();
                lastOverallResult = areaData.overallResult;
            }
        }
    }

    bool ShouldShowTexts()
    {
        if (!onlyShowInStaticTopDown) return true;
        if (!topDownController)
        {
            if (enableDebug) Debug.Log($"[ManualAreaLabel] {name}: TopDownController null");
            return false;
        }
        // Con el TopDownController corregido, esto devuelve true desde que pulsas MAPA
        bool isTopDown = topDownController.GetCurrentMode() == TopDownCameraController.CameraMode.TopDown;
        bool isStatic = topDownController.IsUsingFixedStaticView();
        return isTopDown && isStatic;
    }

    void SetVisibility(bool visible)
    {
        if (nameText) nameText.gameObject.SetActive(visible);
        if (percentText) percentText.gameObject.SetActive(visible);

        // Al mostrarse en MAPA, escalar si se requiere (para que sea visible en Game)
        if (visible && cachedCanvas != null && topDownController != null && topDownController.IsUsingFixedStaticView())
        {
            var rt = cachedCanvas.transform as RectTransform;
            if (rt != null) rt.localScale = Vector3.one * Mathf.Max(0.001f, mapScale);
        }
    }

    void UpdateTexts()
    {
        UpdateName();
        UpdatePercentage();
    }

    void UpdateName()
    {
        if (!nameText) return;

        if (useCustomName)
            nameText.text = customNameText;
        else
        {
            var areaData = GetAreaData();
            if (areaData != null) nameText.text = areaData.displayName;
        }

        if (keepTextBlack) nameText.color = Color.black;
    }

    void UpdatePercentage()
    {
        if (!percentText || areaManager == null) return;

        var areaData = GetAreaData();
        if (areaData != null)
        {
            percentText.text = $"{areaData.overallResult:F0}%";
            lastOverallResult = areaData.overallResult;

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

    public void ForceRefresh()
    {
        if (enableDebug) Debug.Log($"[ManualAreaLabel] {name}: ForceRefresh()");
        UpdateTexts();
    }

    public void SetTopDownVisibility(bool visible)
    {
        if (enableDebug) Debug.Log($"[ManualAreaLabel] {name}: SetTopDownVisibility({visible})");
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
