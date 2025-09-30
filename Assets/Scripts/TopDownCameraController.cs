using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador de cámara para vista estática Top-Down ortogonal o cuasi-ortogonal.
/// Permite alternar entre modo libre (Sims) y vista cenital sobre la planta.
/// Soporta focus dinámico sobre áreas con encuadre automático y retorno al "home" estático.
/// </summary>
public class TopDownCameraController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuración de Vista Top-Down")]
    [SerializeField] private float cameraHeight = 80f;
    [SerializeField] private float cameraAngle = 20f; // 0 = perpendicular
    [SerializeField] private Vector3 plantCenter = new Vector3(-50f, 0f, -20f);

    [Header("Ajuste del Viewport")]
    [SerializeField] private float viewportWidth = 120f;
    [SerializeField] private float viewportDepth = 100f;

    [Header("Transición")]
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private bool smoothTransitions = true;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Control")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool startInTopDownMode = false;

    [Header("Vista Top-Down fija (opcional)")]
    public bool useFixedStaticView = true;
    [SerializeField] private Vector3 fixedTopDownPosition = new Vector3(-34.9f, 168.6f, 20.3f);
    [SerializeField] private Vector3 fixedTopDownEuler = new Vector3(90f, 0f, 0f);

    [Header("Focus al área (encuadre)")]
    [SerializeField, Tooltip("Margen extra alrededor del área al hacer focus")]
    private float focusPaddingPercent = 0.20f;
    [SerializeField] private float minFocusHeight = 25f;
    [SerializeField] private float maxFocusHeight = 220f;
    [SerializeField, Tooltip("Duración por defecto al regresar al home estático")]
    private float returnSeconds = 0.6f;

    #endregion

    #region Private State

    public enum CameraMode { Free, TopDown }

    private CameraMode currentMode;
    private CameraMode targetMode;

    // Referencias cacheadas
    private SimsCameraController simsCameraController;
    private Camera cam;
    private AreaOverlayPainter overlay;

    // Transiciones/targets
    private Vector3 topDownPosition;
    private Quaternion topDownRotation;
    private Vector3 freePosition;
    private Quaternion freeRotation;
    private bool isTransitioning = false;
    private float transitionProgress = 0f;

    // "Home" de la vista estática (a dónde regresamos al cerrar dashboard)
    private Vector3 staticHomePos;
    private Quaternion staticHomeRot;

    #endregion

    #region Unity Callbacks

    void Start()
    {
        CacheComponents();
        CalculateTopDownTransform();

        currentMode = startInTopDownMode ? CameraMode.TopDown : CameraMode.Free;
        targetMode = currentMode;

        if (startInTopDownMode)
        {
            SetTopDownModeImmediate();
        }

        QCLog.Info($"TopDownCameraController listo. Presiona {toggleKey} para alternar.");
    }

    void Update()
    {
        // No consumir input de cámara sobre la UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCameraMode();
        }

        if (isTransitioning)
        {
            UpdateTransition();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene el modo actual de cámara (considera el target si hay transición en curso).
    /// </summary>
    public CameraMode GetCurrentMode()
    {
        return isTransitioning ? targetMode : currentMode;
    }

    /// <summary>
    /// Retorna true si el modo target es TopDown (útil durante transiciones).
    /// </summary>
    public bool IsTopDownTarget() => targetMode == CameraMode.TopDown;

    /// <summary>
    /// Retorna true si está usando la vista estática fija configurada.
    /// </summary>
    public bool IsUsingFixedStaticView()
    {
        var mode = isTransitioning ? targetMode : currentMode;
        return (mode == CameraMode.TopDown) && useFixedStaticView;
    }

    /// <summary>
    /// Alterna entre modo libre y top-down.
    /// </summary>
    public void ToggleCameraMode()
    {
        if (currentMode == CameraMode.Free)
            SetTopDownMode();
        else
            SetFreeMode();
    }

    /// <summary>
    /// Cambia inmediatamente a modo Top-Down sin transición.
    /// Útil para inicialización o cambios instantáneos.
    /// </summary>
    public void SetTopDownModeImmediate()
    {
        currentMode = CameraMode.TopDown;
        targetMode = CameraMode.TopDown;

        if (useFixedStaticView)
        {
            staticHomePos = fixedTopDownPosition;
            staticHomeRot = Quaternion.Euler(fixedTopDownEuler);
        }
        else
        {
            staticHomePos = topDownPosition;
            staticHomeRot = topDownRotation;
        }

        transform.position = staticHomePos;
        transform.rotation = staticHomeRot;

        if (simsCameraController != null)
            simsCameraController.enabled = false;

        if (overlay != null)
            overlay.SetTopDownMode(true);
    }

    /// <summary>
    /// Cambia a modo Top-Down con transición suave.
    /// </summary>
    public void SetTopDownMode()
    {
        if (currentMode == CameraMode.TopDown || isTransitioning)
            return;

        targetMode = CameraMode.TopDown;

        if (simsCameraController != null)
            simsCameraController.enabled = false;

        freePosition = transform.position;
        freeRotation = transform.rotation;

        Vector3 targetPos = useFixedStaticView ? fixedTopDownPosition : topDownPosition;
        Quaternion targetRot = useFixedStaticView ? Quaternion.Euler(fixedTopDownEuler) : topDownRotation;

        staticHomePos = targetPos;
        staticHomeRot = targetRot;

        StartTransition(targetPos, targetRot, CameraMode.TopDown);

        if (overlay != null)
            overlay.SetTopDownMode(true);
    }

    /// <summary>
    /// Cambia a modo libre (Sims) con transición suave.
    /// </summary>
    public void SetFreeMode()
    {
        if (currentMode == CameraMode.Free || isTransitioning)
            return;

        targetMode = CameraMode.Free;
        StartTransition(freePosition, freeRotation, CameraMode.Free);

        if (overlay != null)
            overlay.SetTopDownMode(false);
    }

    /// <summary>
    /// Hace focus sobre un área específica con encuadre automático.
    /// Calcula la altura necesaria para encuadrar el área completa.
    /// </summary>
    /// <param name="area">Transform del área objetivo</param>
    /// <param name="zoomSeconds">Duración de la transición</param>
    public void FocusOnAreaTopDown(Transform area, float zoomSeconds = 0.75f)
    {
        if (area == null || cam == null)
        {
            QCLog.Warn("FocusOnAreaTopDown: área o cámara nula.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FocusRoutineFit(area.gameObject, Mathf.Max(0.1f, zoomSeconds)));
    }

    /// <summary>
    /// API homogénea: Focus sobre un punto específico con distancia y duración.
    /// Compatible con la firma de SimsCameraController.
    /// </summary>
    /// <param name="focusPoint">Punto central donde enfocar</param>
    /// <param name="distance">Altura de la cámara (distancia vertical)</param>
    /// <param name="duration">Duración de la transición en segundos</param>
    public void FocusOnArea(Vector3 focusPoint, float distance, float duration)
    {
        if (cam == null)
        {
            QCLog.Warn("FocusOnArea: cámara nula.");
            return;
        }

        float height = Mathf.Clamp(distance, minFocusHeight, maxFocusHeight);
        Vector3 targetPos = new Vector3(focusPoint.x, height, focusPoint.z);
        Quaternion targetRot = Quaternion.Euler(90f, 0f, 0f);

        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(targetPos, targetRot, CameraMode.TopDown, duration));
    }

    /// <summary>
    /// Retorna la cámara al punto "home" estático configurado.
    /// </summary>
    /// <param name="secondsOverride">Duración custom, o -1 para usar returnSeconds configurado</param>
    public void ReturnToStaticHome(float secondsOverride = -1f)
    {
        if (GetCurrentMode() != CameraMode.TopDown)
            return;

        float secs = (secondsOverride > 0f) ? secondsOverride : returnSeconds;
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(staticHomePos, staticHomeRot, CameraMode.TopDown, secs));
    }

    /// <summary>
    /// Aplica configuración de vista top-down desde struct.
    /// Útil para ajuste dinámico de parámetros.
    /// </summary>
    public void ApplySettings(TopDownSettings s)
    {
        cameraHeight = s.cameraHeight;
        cameraAngle = s.cameraAngle;
        plantCenter = s.plantCenter;
        viewportWidth = s.viewportWidth;
        viewportDepth = s.viewportDepth;
        CalculateTopDownTransform();
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Cachea componentes necesarios al inicio.
    /// </summary>
    private void CacheComponents()
    {
        cam = GetComponent<Camera>();
        simsCameraController = GetComponent<SimsCameraController>();
        overlay = FindFirstObjectByType<AreaOverlayPainter>();

        if (cam == null)
            QCLog.Error("TopDownCameraController: No se encontró componente Camera.");
    }

    /// <summary>
    /// Calcula la posición y rotación para la vista top-down calculada.
    /// </summary>
    private void CalculateTopDownTransform()
    {
        Vector3 offset = Vector3.back * (cameraHeight * Mathf.Tan(cameraAngle * Mathf.Deg2Rad));
        topDownPosition = plantCenter + Vector3.up * cameraHeight + offset;

        Vector3 direction = (plantCenter - topDownPosition).normalized;
        topDownRotation = Quaternion.LookRotation(direction);

        if (cam != null)
            cam.fieldOfView = CalculateRequiredFOV();
    }

    /// <summary>
    /// Calcula el FOV necesario para encuadrar el viewport configurado.
    /// </summary>
    private float CalculateRequiredFOV()
    {
        if (cam == null) return 60f;

        float distance = Vector3.Distance(topDownPosition, plantCenter);
        float half = Mathf.Max(viewportWidth, viewportDepth) * 0.5f;
        float fovRad = 2f * Mathf.Atan2(half, distance);
        return Mathf.Clamp(fovRad * Mathf.Rad2Deg * 1.4f, 20f, 85f);
    }

    /// <summary>
    /// Inicia una transición de cámara.
    /// </summary>
    private void StartTransition(Vector3 targetPos, Quaternion targetRot, CameraMode newMode)
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(targetPos, targetRot, newMode));
    }

    /// <summary>
    /// Rutina de transición suave entre posiciones/rotaciones de cámara.
    /// </summary>
    private IEnumerator TransitionRoutine(Vector3 toPos, Quaternion toRot, CameraMode newMode, float customSpeed = -1f)
    {
        isTransitioning = true;
        transitionProgress = 0f;

        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;

        float speed = (customSpeed > 0f) ? (1f / customSpeed) : transitionSpeed;

        while (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime * speed;
            float t = smoothTransitions ? transitionCurve.Evaluate(transitionProgress) : transitionProgress;
            transform.position = Vector3.Lerp(fromPos, toPos, t);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        isTransitioning = false;
        currentMode = newMode;
        transform.position = toPos;
        transform.rotation = toRot;

        if (simsCameraController != null)
            simsCameraController.enabled = (currentMode == CameraMode.Free);
    }

    /// <summary>
    /// Rutina de focus que calcula bounds del área y encuadra automáticamente.
    /// </summary>
    private IEnumerator FocusRoutineFit(GameObject areaObj, float seconds)
    {
        // 1) Bounds del área por renderers
        Bounds b = GetWorldBounds(areaObj);
        Vector3 center = b.center;

        // 2) Radio útil en XZ + padding
        float half = Mathf.Max(b.extents.x, b.extents.z);
        half *= (1f + focusPaddingPercent);

        // 3) Altura requerida según FOV vertical, vista perpendicular
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float requiredHeight = half / Mathf.Tan(fovRad * 0.5f);
        float height = Mathf.Clamp(requiredHeight, minFocusHeight, maxFocusHeight);

        Vector3 toPos = new Vector3(center.x, height, center.z);
        Quaternion toRot = Quaternion.Euler(90f, 0f, 0f);

        yield return TransitionRoutine(toPos, toRot, CameraMode.TopDown, seconds);
    }

    /// <summary>
    /// Obtiene los bounds mundiales de un GameObject basándose en sus Renderers.
    /// </summary>
    private Bounds GetWorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return b;
        }

        // Fallback si no hay renderers
        QCLog.Warn($"GetWorldBounds: No se encontraron Renderers en {go.name}, usando fallback.");
        return new Bounds(go.transform.position, new Vector3(10, 2, 10));
    }

    /// <summary>
    /// Método reservado para actualizaciones de transición adicionales si son necesarias.
    /// </summary>
    private void UpdateTransition()
    {
        // Reservado para interpolaciones extras si se requieren en el futuro
    }

    #endregion

    #region Debug

    /// <summary>
    /// Struct para aplicar configuraciones de top-down de forma programática.
    /// </summary>
    public struct TopDownSettings
    {
        public float cameraHeight, cameraAngle;
        public Vector3 plantCenter;
        public float viewportWidth, viewportDepth;
    }

    #endregion
}
