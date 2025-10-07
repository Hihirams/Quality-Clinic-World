using UnityEngine;
using TMPro;
using static TMPro.ShaderUtilities;

/// <summary>
/// Controla etiquetas de texto manuales (nombre + porcentaje) que se muestran �nicamente
/// en la vista est�tica top-down del mapa 3D/2D.
/// 
/// Responsabilidades:
/// - Auto-detectar el �rea asociada desde la jerarqu�a
/// - Sincronizar visibilidad con ManualLabelsManager y TopDownCameraController
/// - Actualizar textos con datos de AreaManager
/// - Aplicar estilos configurables (preset TMP, outline, colores Apple)
/// - Mantener consistencia de posici�n y escala del Canvas WorldSpace
/// 
/// Dependencias:
/// - ManualLabelsManager: registro centralizado
/// - AreaManager: datos de porcentaje por �rea
/// - TopDownCameraController: detecci�n de modo de c�mara
/// - AppleTheme (opcional): colores consistentes
/// </summary>
public class ManualAreaLabel : MonoBehaviour
{

    [Header("Fallbacks (si no hay datos en AreaManager)")]
[SerializeField] private string fallbackDisplayName = "";
[SerializeField] private float fallbackOverall = 81f;

// --- modifica UpdateTexts() para usar fallback ---
// Reemplaza TODO el método GetAreaData() por este:
private AreaManager.AreaData GetAreaData()
{
    // 1) Intenta con AreaManager (si existe y contiene el área)
    var am = areaManager ?? FindFirstObjectByType<AreaManager>();
    if (am != null)
    {
        string normalizedKey = NormalizeAreaKey(areaKey);
        var d = am.GetAreaData(normalizedKey);
        if (d != null) return d;
    }

    // 2) Fallback a lo configurado por el wizard (SO) si aún no existe en AreaManager
    float ov = Mathf.Clamp(fallbackOverall, 0f, 100f);
    return new AreaManager.AreaData {
        displayName  = string.IsNullOrEmpty(fallbackDisplayName) ? areaKey : fallbackDisplayName,
        overallResult= ov,
        statusColor  = AppleTheme.Status(ov),
    };
}



    #region Serialized Fields

    [Header("Estilo (opcional)")]
    [Tooltip("Si est� activo, NO se sobreescriben Outline/Underlay/Color en runtime.")]
    public bool respectInspectorTextSettings = true;

    [Tooltip("Preset Material de TMP para aplicar a ambos textos (opcional).")]
    public Material sharedTMPPreset;

    [Tooltip("Aplica sharedTMPPreset una sola vez en Start.")]
    public bool applyPresetOnStart = true;

    [Header("Referencias de Textos")]
    [Tooltip("TextMeshProUGUI del nombre del �rea")]
    public TextMeshProUGUI nameText;

    [Tooltip("TextMeshProUGUI del porcentaje de calidad")]
    public TextMeshProUGUI percentText;

    [Header("Configuraci�n")]
    [Tooltip("Clave del �rea (ATHONDA, VCTL4, BUZZERL2, VBL1). Se auto-detecta si est� vac�o.")]
    public string areaKey = "ATHONDA";

    [Tooltip("Forzar nombre personalizado en lugar del de AreaManager")]
    public bool useCustomName = false;

    public string customNameText = "AT HONDA";

    [Header("Vista")]
    [Tooltip("Solo mostrar en vista top-down fija")]
    public bool onlyShowInStaticTopDown = true;

    [Header("Colores de Texto")]
    [Tooltip("Forzar texto negro (obsoleto si usas respectInspectorTextSettings)")]
    public bool keepTextBlack = false;

    [Header("Estilo en Top-Down Est�tico")]
    [Tooltip("Aplicar blanco con outline negro en vista est�tica")]
    public bool whiteWithBlackOutlineInStatic = true;

    [Range(0f, 1f)]
    [Tooltip("Ancho del outline (recomendado 0.12-0.25)")]
    public float outlineWidthStatic = 0.18f;

    public Color outlineColorStatic = new Color(0, 0, 0, 0.95f);

    [Header("Canvas Settings - CLAVE PARA CONSISTENCIA")]
    [Tooltip("Altura Y fija para el label en el mundo")]
    public float labelHeightY = 0.25f;

    [Tooltip("Escala base del canvas")]
    public float canvasBaseScale = 1.0f;

    [Tooltip("Escala espec�fica para vista mapa")]
    public float mapModeScale = 1.2f;

    [Tooltip("Orden de sorting del canvas")]
    public int canvasSortingOrder = 5000;

    [Header("?? CORRECCI�N DE POSICI�N")]
    [Tooltip("Offset en X para ajustar posici�n final")]
    public float positionOffsetX = 0f;

    [Tooltip("Offset en Z para ajustar posici�n final")]
    public float positionOffsetZ = 0f;

    [Tooltip("Forzar recalculo de posici�n en cada frame")]
    public bool continuousPositionUpdate = true;

    [Header("Autodetecci�n")]
    [Tooltip("Detectar �rea por jerarqu�a de padres (busca AREA_*)")]
    public bool autoDetectAreaFromHierarchy = true;

    [Header("Debug")]
    public bool enableDebug = false;

    #endregion

    #region Private State

    private AreaManager areaManager;
    private TopDownCameraController topDownController;
    private bool currentlyVisible = false;
    private float lastOverallResult = -1f;
    private bool isRegistered = false;
    private Canvas cachedCanvas;
    private Vector3 originalWorldPosition = Vector3.zero;

    #endregion

    #region Unity Callbacks

    /// <summary>
    /// Validaci�n en Editor: guarda la posici�n original antes de Play Mode
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            originalWorldPosition = transform.position;
        }
    }

    /// <summary>
    /// Inicializaci�n temprana:
    /// - Auto-detecta areaKey desde jerarqu�a
    /// - Guarda posici�n original del transform
    /// </summary>
    void Awake()
    {
        // Auto-detecci�n de �rea
        if (autoDetectAreaFromHierarchy)
        {
            string detected = DetectAreaKeyFromParents();
            if (!string.IsNullOrEmpty(detected))
            {
                areaKey = detected;
            }
        }

        // Guardar posici�n original para correcciones posteriores
        originalWorldPosition = transform.position;

        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] Awake - areaKey={areaKey}, pos={originalWorldPosition}");
        }
        var rt = GetComponentInChildren<Canvas>(true)?.transform as RectTransform;
    if (rt != null && respectInspectorTextSettings)
        canvasBaseScale = rt.localScale.x;
    }

    /// <summary>
    /// Inicializaci�n principal:
    /// - Registra en ManualLabelsManager
    /// - Obtiene referencias de sistemas (AreaManager, c�mara)
    /// - Configura Canvas WorldSpace
    /// - Aplica preset TMP y colores iniciales
    /// - Establece visibilidad inicial (oculto)
    /// </summary>
    void Start()
    {
        // Registro centralizado
        ManualLabelsManager.Register(this);
        isRegistered = true;

        // Referencias de sistemas
        areaManager = FindFirstObjectByType<AreaManager>();
        
        var mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        if (mainCamera != null)
        {
            topDownController = mainCamera.GetComponent<TopDownCameraController>();
        }

        // Setup cr�tico del Canvas WorldSpace
        SetupCanvasForConsistency();

        // Aplicar preset TMP opcional (una sola vez)
        if (applyPresetOnStart && sharedTMPPreset != null)
        {
            if (nameText) nameText.fontSharedMaterial = sharedTMPPreset;
            if (percentText) percentText.fontSharedMaterial = sharedTMPPreset;
        }

        // Colores iniciales (si no se respeta Inspector)
        if (keepTextBlack)
        {
            if (nameText) nameText.color = Color.black;
            if (percentText) percentText.color = Color.black;
        }

        // Nombre personalizado
        if (useCustomName && nameText && !string.IsNullOrEmpty(customNameText))
        {
            nameText.text = customNameText;
        }

        // Estado inicial: oculto hasta que se active el modo correcto
        SetVisibility(false);
        UpdateTexts();

        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] Start completado");
        }
    }

    /// <summary>
    /// Loop principal:
    /// - Asegura consistencia del Canvas (c�mara asignada)
    /// - Actualiza visibilidad seg�n modo de c�mara
    /// - Sincroniza datos de porcentaje si cambian
    /// - Actualiza posici�n continuamente si est� configurado
    /// </summary>
    void Update()
    {
        // Asegurar consistencia de Canvas y c�mara
        EnsureCanvasConsistency();

        // Determinar si debe mostrarse seg�n modo de c�mara actual
        bool shouldShow = ShouldShowTexts();
        
        if (shouldShow != currentlyVisible)
        {
            currentlyVisible = shouldShow;
            SetVisibility(currentlyVisible);
            
            if (currentlyVisible)
            {
                UpdateTexts();
                ApplyStaticStyleIfNeeded();
            }

            if (enableDebug)
            {
                QCLog.Info($"[ManualAreaLabel:{name}] Visibilidad: {shouldShow}");
            }
        }

        // Actualizar datos si el porcentaje cambi�
        if (currentlyVisible)
        {
            var data = GetAreaData();
            if (data != null && data.overallResult != lastOverallResult)
            {
                UpdatePercentage();
                lastOverallResult = data.overallResult;
            }
        }

        // Actualizaci�n continua de posici�n si est� habilitada
        if (continuousPositionUpdate && currentlyVisible)
        {
            UpdateCanvasPosition();
        }
    }

    /// <summary>
    /// Limpieza: desregistra del ManualLabelsManager
    /// </summary>
    void OnDestroy()
    {
        if (isRegistered)
        {
            ManualLabelsManager.Unregister(this);
            isRegistered = false;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Fuerza actualizaci�n completa de textos y posici�n.
    /// Llamado desde ManualLabelsManager cuando cambia el modo de c�mara.
    /// </summary>
    public void ForceRefresh()
    {
        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] ForceRefresh()");
        }
        
        UpdateTexts();
        UpdateCanvasPosition();
    }

    /// <summary>
    /// Establece visibilidad seg�n el estado top-down.
    /// Llamado desde ManualLabelsManager para sincronizaci�n global.
    /// </summary>
    /// <param name="visible">Si el modo top-down est� activo</param>
    public void SetTopDownVisibility(bool visible)
    {
        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] SetTopDownVisibility({visible})");
        }

        bool finalVisible = visible && ShouldShowTexts();
        SetVisibility(finalVisible);
        
        if (finalVisible)
        {
            UpdateTexts();
        }
    }

    #endregion

    #region Internal Helpers - Canvas Setup

    /// <summary>
    /// Configuraci�n inicial del Canvas WorldSpace.
    /// Asegura renderMode correcto, c�mara asignada, posici�n y escala iniciales.
    /// </summary>
    private void SetupCanvasForConsistency()
    {
        cachedCanvas = GetComponentInChildren<Canvas>(true);
        
        if (!cachedCanvas)
        {
            if (enableDebug)
            {
                QCLog.Error($"[ManualAreaLabel:{name}] No se encontr� Canvas hijo!");
            }
            return;
        }

        // Configuraci�n b�sica WorldSpace
        cachedCanvas.renderMode = RenderMode.WorldSpace;
        cachedCanvas.overrideSorting = true;
        cachedCanvas.sortingOrder = canvasSortingOrder;

        // Asignar c�mara inmediatamente
        AssignCameraToCanvas();

        // Posici�n inicial basada en originalWorldPosition
        UpdateCanvasPosition();

        // Escala inicial
        if (!respectInspectorTextSettings)
    UpdateCanvasScale();

        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] Canvas configurado - Camera: {cachedCanvas.worldCamera?.name}");
        }
    }

    /// <summary>
    /// Verifica y mantiene la consistencia del Canvas en runtime.
    /// Re-asigna la c�mara si se pierde la referencia.
    /// </summary>
    private void EnsureCanvasConsistency()
    {
        if (!cachedCanvas) return;

        // Verificar que la c�mara siga asignada en WorldSpace
        if (cachedCanvas.renderMode == RenderMode.WorldSpace)
        {
            if (cachedCanvas.worldCamera == null)
            {
                AssignCameraToCanvas();
            }
        }
    }

    /// <summary>
    /// Asigna la c�mara activa al Canvas WorldSpace.
    /// Prioridad: Camera.main > FindFirstObjectByType<Camera>
    /// </summary>
    private void AssignCameraToCanvas()
    {
        if (!cachedCanvas) return;

        Camera targetCamera = Camera.main;
        
        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }

        if (targetCamera != null)
        {
            cachedCanvas.worldCamera = targetCamera;
            
            if (enableDebug)
            {
                QCLog.Info($"[ManualAreaLabel:{name}] C�mara asignada: {targetCamera.name}");
            }
        }
        else
        {
            if (enableDebug)
            {
                QCLog.Warn($"[ManualAreaLabel:{name}] No se encontr� c�mara disponible!");
            }
        }
    }

    /// <summary>
    /// Actualiza la posici�n mundial del label aplicando offsets configurados.
    /// Resetea la posici�n local del Canvas a (0,0,0).
    /// </summary>
    private void UpdateCanvasPosition()
    {
        if (!cachedCanvas) return;

        // Calcular posici�n final con offsets
        Vector3 finalPosition = originalWorldPosition;
        finalPosition.x += positionOffsetX;
        finalPosition.y = labelHeightY;
        finalPosition.z += positionOffsetZ;
        
        transform.position = finalPosition;

        // Resetear Canvas local a cero (para evitar offsets acumulados)
        cachedCanvas.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Actualiza la escala del Canvas seg�n el modo actual.
    /// Aplica mapModeScale cuando est� visible en modo mapa.
    /// </summary>
private void UpdateCanvasScale()
{
    if (respectInspectorTextSettings) return; // <- NO cambiar escala si quiero respetar el inspector
    if (!cachedCanvas) return;

    bool isInMapMode = ShouldShowTexts() && currentlyVisible;
    float targetScale = isInMapMode ? (canvasBaseScale * mapModeScale) : canvasBaseScale;

    var rt = cachedCanvas.transform as RectTransform;
    if (rt != null)
        rt.localScale = Vector3.one * targetScale;
}


    /// <summary>
    /// Centra los textos hijos dentro del Canvas.
    /// Configura anchors y pivot al centro (0.5, 0.5).
    /// </summary>
    private void CenterChildTexts()
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

    #endregion

    #region Internal Helpers - Style Application

    /// <summary>
    /// Aplica el estilo de outline blanco/negro si corresponde.
    /// Solo act�a si NO se respeta el Inspector y NO hay preset TMP.
    /// </summary>
    private void ApplyStaticStyleIfNeeded()
    {
        // Respetar configuraci�n manual o preset
        if (respectInspectorTextSettings || sharedTMPPreset != null)
        {
            return;
        }

        bool isStaticTopDown = ShouldShowTexts();
        
        if (isStaticTopDown && whiteWithBlackOutlineInStatic)
        {
            if (nameText) SetTMPWhiteWithOutline(nameText);
            if (percentText) SetTMPWhiteWithOutline(percentText);
        }
    }

    /// <summary>
    /// Aplica estilo blanco con outline negro a un TextMeshProUGUI.
    /// Modifica el material runtime (instancia).
    /// </summary>
    /// <param name="tmp">TextMeshProUGUI a modificar</param>
    private void SetTMPWhiteWithOutline(TextMeshProUGUI tmp)
    {
        tmp.color = Color.white;
        
        var mat = tmp.fontMaterial; // Instancia runtime
        if (mat != null)
        {
            mat.SetFloat(ID_OutlineWidth, outlineWidthStatic);
            mat.SetColor(ID_OutlineColor, outlineColorStatic);
            
            if (mat.HasProperty(ID_FaceDilate))
            {
                mat.SetFloat(ID_FaceDilate, 0.0f);
            }
        }
    }

    #endregion

    #region Internal Helpers - Visibility & Text Updates

    /// <summary>
    /// Determina si los textos deben mostrarse seg�n el modo de c�mara actual.
    /// Eval�a: ManualLabelsManager, TopDownCameraController y modo est�tico.
    /// </summary>
    /// <returns>True si debe mostrarse en el modo actual</returns>
    private bool ShouldShowTexts()
    {
        // Si no est� restringido a top-down, siempre visible
        if (!onlyShowInStaticTopDown)
        {
            return true;
        }

        // Consultar estado del manager central
        var mgr = FindFirstObjectByType<ManualLabelsManager>();
        bool managerSaysTopDown = (mgr != null) && mgr.GetCurrentTopDownMode();

        // Verificar controlador de c�mara top-down
        bool topDownOk = false;
        bool staticOk = false;

        if (topDownController != null)
        {
            topDownOk = (topDownController.GetCurrentMode() == TopDownCameraController.CameraMode.TopDown);
            staticOk = topDownController.IsUsingFixedStaticView();
        }

        // Mostrar si el manager lo indica O si ambos modos est�n activos
        return managerSaysTopDown || (topDownOk && staticOk);
    }

    /// <summary>
    /// Establece la visibilidad de los GameObjects de texto.
    /// Actualiza escala y estilo cuando se vuelve visible.
    /// </summary>
    /// <param name="visible">Si debe mostrarse</param>
    private void SetVisibility(bool visible)
    {
        if (nameText)
        {
            nameText.gameObject.SetActive(visible);
        }

        if (percentText)
        {
            percentText.gameObject.SetActive(visible);
        }

        if (visible)
        {
            UpdateCanvasScale();
            EnsureCanvasConsistency();
            ApplyStaticStyleIfNeeded();
        }

        if (enableDebug)
        {
            QCLog.Info($"[ManualAreaLabel:{name}] SetVisibility({visible})");
        }
    }

    /// <summary>
    /// Actualiza ambos textos (nombre y porcentaje).
    /// </summary>
    private void UpdateTexts()
    {
        UpdateName();
        UpdatePercentage();
    }

    /// <summary>
    /// Actualiza el texto del nombre del �rea.
    /// Usa customNameText si est� configurado, sino obtiene de AreaManager.
    /// </summary>
    private void UpdateName()
    {
        if (!nameText) return;

        if (useCustomName && !string.IsNullOrEmpty(customNameText))
        {
            nameText.text = customNameText;
        }
        else
        {
            var data = GetAreaData();
            if (data != null)
            {
                nameText.text = data.displayName;
            }
        }

        // Aplicar color negro si est� configurado y NO en modo est�tico
        if (keepTextBlack && !ShouldShowTexts())
        {
            nameText.color = Color.black;
        }
    }

    /// <summary>
    /// Actualiza el texto del porcentaje de calidad.
    /// Formatea como entero con s�mbolo %.
    /// </summary>
    private void UpdatePercentage()
    {
        if (!percentText) return;

        var data = GetAreaData();
        
        if (data != null)
        {
            percentText.text = $"{data.overallResult:F0}%";
            
            // Aplicar color negro si est� configurado y NO en modo est�tico
            if (keepTextBlack && !ShouldShowTexts())
            {
                percentText.color = Color.black;
            }
        }
        else
        {
            percentText.text = "0%";
            
            if (keepTextBlack && !ShouldShowTexts())
            {
                percentText.color = Color.black;
            }
        }
    }

    #endregion

    #region Internal Helpers - Area Data & Detection
    /// <summary>
    /// Normaliza la clave del �rea eliminando prefijos y espacios.
    /// Mapea variantes comunes a claves est�ndar (ATHONDA, VCTL4, BUZZERL2, VBL1).
    /// </summary>
    /// <param name="key">Clave cruda</param>
    /// <returns>Clave normalizada</returns>
    private string NormalizeAreaKey(string key)
    {
        string upper = (key ?? "").ToUpperInvariant();
        upper = upper.Replace("AREA_", "").Replace(" ", "").Replace("_", "");

        // Mapeo a claves conocidas
        if (upper.Contains("ATHONDA") || upper == "ATHONDA") return "ATHONDA";
        if (upper.Contains("VCTL4") || upper == "VCTL4") return "VCTL4";
        if (upper.Contains("BUZZERL2") || upper == "BUZZERL2") return "BUZZERL2";
        if (upper.Contains("VBL1") || upper == "VBL1") return "VBL1";

        return upper;
    }

    /// <summary>
    /// Detecta autom�ticamente la clave del �rea buscando en la jerarqu�a de padres.
    /// Busca GameObjects con nombres que empiecen con "AREA_".
    /// </summary>
    /// <returns>Clave detectada o null si no se encuentra</returns>
    private string DetectAreaKeyFromParents()
    {
        Transform t = transform;
        
        while (t != null)
        {
            string n = t.name.ToUpperInvariant();
            
            if (n.StartsWith("AREA_"))
            {
                // Extraer la parte despu�s de "AREA_"
                string k = n.Substring(5);
                k = k.Replace(" ", "").Replace("_", "");
                
                if (enableDebug)
                {
                    QCLog.Info($"[ManualAreaLabel:{name}] Detectado desde padre: {k}");
                }
                
                return NormalizeAreaKey(k);
            }
            
            t = t.parent;
        }
        
        return null;
    }

    #endregion

    #region Debug

    // M�todos de debug adicionales pueden a�adirse aqu� si es necesario
    // Por ahora, los logs est�n distribuidos en los m�todos principales

    #endregion
}
