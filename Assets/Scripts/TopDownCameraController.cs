using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TopDownCameraController : MonoBehaviour
{
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
    [SerializeField, Tooltip("margen extra alrededor del área al hacer focus")]
    private float focusPaddingPercent = 0.20f;
    [SerializeField] private float minFocusHeight = 25f;
    [SerializeField] private float maxFocusHeight = 220f;
    [SerializeField, Tooltip("Duración por defecto al regresar al home estático")]
    private float returnSeconds = 0.6f;

    public enum CameraMode { Free, TopDown }
    private CameraMode currentMode;
    private CameraMode targetMode;

    private FreeCameraController freeCameraController;
    private Camera _cam;

    // Transiciones/targets
    private Vector3 topDownPosition;
    private Quaternion topDownRotation;
    private Vector3 freePosition;
    private Quaternion freeRotation;
    private bool isTransitioning = false;
    private float transitionProgress = 0f;

    // Overlays
    private AreaOverlayPainter _overlay;

    // "Home" de la vista estática (a dónde regresamos al cerrar dashboard)
    private Vector3 staticHomePos;
    private Quaternion staticHomeRot;

    void Start()
    {
        _cam = GetComponent<Camera>();
        freeCameraController = GetComponent<FreeCameraController>();

        _overlay = FindFirstObjectByType<AreaOverlayPainter>();

        CalculateTopDownTransform();

        currentMode = startInTopDownMode ? CameraMode.TopDown : CameraMode.Free;
        targetMode = currentMode;

        if (startInTopDownMode) SetTopDownModeImmediate();

        Debug.Log("TopDownCameraController listo. Presiona " + toggleKey + " para alternar.");
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return; // No consumir input de cámara sobre la UI

        if (Input.GetKeyDown(toggleKey)) ToggleCameraMode();
        if (isTransitioning) UpdateTransition();
    }


    // ========= Reporte de estado (considera target cuando hay transición) =========
    public CameraMode GetCurrentMode()
    {
        return isTransitioning ? targetMode : currentMode;
    }
    public bool IsTopDownTarget() => targetMode == CameraMode.TopDown;
    public bool IsUsingFixedStaticView()
    {
        var mode = isTransitioning ? targetMode : currentMode;
        return (mode == CameraMode.TopDown) && useFixedStaticView;
    }
    // =========================================================================

    void CalculateTopDownTransform()
    {
        Vector3 offset = Vector3.back * (cameraHeight * Mathf.Tan(cameraAngle * Mathf.Deg2Rad));
        topDownPosition = plantCenter + Vector3.up * cameraHeight + offset;

        Vector3 direction = (plantCenter - topDownPosition).normalized;
        topDownRotation = Quaternion.LookRotation(direction);

        if (_cam != null) _cam.fieldOfView = CalculateRequiredFOV();
    }

    float CalculateRequiredFOV()
    {
        if (_cam == null) return 60f;
        float distance = Vector3.Distance(topDownPosition, plantCenter);
        float half = Mathf.Max(viewportWidth, viewportDepth) * 0.5f;
        float fovRad = 2f * Mathf.Atan2(half, distance);
        return Mathf.Clamp(fovRad * Mathf.Rad2Deg * 1.4f, 20f, 85f);
    }

    public struct TopDownSettings
    {
        public float cameraHeight, cameraAngle;
        public Vector3 plantCenter;
        public float viewportWidth, viewportDepth;
    }
    public void ApplySettings(TopDownSettings s)
    {
        cameraHeight = s.cameraHeight;
        cameraAngle = s.cameraAngle;
        plantCenter = s.plantCenter;
        viewportWidth = s.viewportWidth;
        viewportDepth = s.viewportDepth;
        CalculateTopDownTransform();
    }

    public void ToggleCameraMode()
    {
        if (currentMode == CameraMode.Free) SetTopDownMode();
        else SetFreeMode();
    }

    public void SetTopDownModeImmediate()
    {
        currentMode = CameraMode.TopDown;
        targetMode = CameraMode.TopDown;

        if (useFixedStaticView)
        {
            // Guardar "home" estático
            staticHomePos = fixedTopDownPosition;
            staticHomeRot = Quaternion.Euler(fixedTopDownEuler);

            transform.position = staticHomePos;
            transform.rotation = staticHomeRot;
        }
        else
        {
            staticHomePos = topDownPosition;
            staticHomeRot = topDownRotation;

            transform.position = staticHomePos;
            transform.rotation = staticHomeRot;
        }

        if (freeCameraController != null)
            freeCameraController.enabled = false;

        _overlay = _overlay ?? FindFirstObjectByType<AreaOverlayPainter>();
        _overlay?.SetTopDownMode(true);

        // Basado en tu implementación previa de cambio inmediato: :contentReference[oaicite:5]{index=5}
    }

    public void SetTopDownMode()
    {
        if (currentMode == CameraMode.TopDown || isTransitioning) return;

        targetMode = CameraMode.TopDown;

        if (freeCameraController != null)
            freeCameraController.enabled = false;

        freePosition = transform.position;
        freeRotation = transform.rotation;

        Vector3 targetPos = useFixedStaticView ? fixedTopDownPosition : topDownPosition;
        Quaternion targetRot = useFixedStaticView ? Quaternion.Euler(fixedTopDownEuler) : topDownRotation;

        // Guardar "home" de esta sesión top-down
        staticHomePos = targetPos;
        staticHomeRot = targetRot;

        StartTransition(targetPos, targetRot, CameraMode.TopDown);

        _overlay = _overlay ?? FindFirstObjectByType<AreaOverlayPainter>();
        _overlay?.SetTopDownMode(true);

        // Estructura de transición preservada del archivo base: :contentReference[oaicite:6]{index=6}
    }

    public void SetFreeMode()
    {
        if (currentMode == CameraMode.Free || isTransitioning) return;

        targetMode = CameraMode.Free;

        StartTransition(freePosition, freeRotation, CameraMode.Free);

        _overlay = _overlay ?? FindFirstObjectByType<AreaOverlayPainter>();
        _overlay?.SetTopDownMode(false);
    }

    void StartTransition(Vector3 targetPos, Quaternion targetRot, CameraMode newMode)
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(targetPos, targetRot, newMode));
    }

    IEnumerator TransitionRoutine(Vector3 toPos, Quaternion toRot, CameraMode newMode)
    {
        isTransitioning = true;
        transitionProgress = 0f;

        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;

        while (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime * transitionSpeed;
            float t = smoothTransitions ? transitionCurve.Evaluate(transitionProgress) : transitionProgress;
            transform.position = Vector3.Lerp(fromPos, toPos, t);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        isTransitioning = false;
        currentMode = newMode;
        transform.position = toPos;
        transform.rotation = toRot;

        if (freeCameraController != null)
            freeCameraController.enabled = (currentMode == CameraMode.Free);
    }

    // ============== NUEVO: Focus encuadrando el área de verdad ==============
    public void FocusOnAreaTopDown(Transform area, float zoomSeconds = 0.75f)
    {
        if (area == null || _cam == null) return;
        StopAllCoroutines();
        StartCoroutine(FocusRoutineFit(area.gameObject, Mathf.Max(0.1f, zoomSeconds)));
    }

    private IEnumerator FocusRoutineFit(GameObject areaObj, float seconds)
    {
        // 1) Bounds del área por renderers
        Bounds b = GetWorldBounds(areaObj);
        Vector3 center = b.center;

        // 2) Radio útil en XZ + padding
        float half = Mathf.Max(b.extents.x, b.extents.z);
        half *= (1f + focusPaddingPercent);

        // 3) Altura requerida según FOV vertical, vista perpendicular
        float fovRad = _cam.fieldOfView * Mathf.Deg2Rad;
        float requiredHeight = half / Mathf.Tan(fovRad * 0.5f);
        float height = Mathf.Clamp(requiredHeight, minFocusHeight, maxFocusHeight);

        Vector3 toPos = new Vector3(center.x, height, center.z);
        Quaternion toRot = Quaternion.Euler(90f, 0f, 0f);

        // Transición suave
        yield return TransitionRoutine(toPos, toRot, CameraMode.TopDown);
    }

    private Bounds GetWorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
        // Fallback si no hay renderers
        return new Bounds(go.transform.position, new Vector3(10, 2, 10));
    }

    // ============== NUEVO: Volver al punto "home" estático ==============
    public void ReturnToStaticHome(float secondsOverride = -1f)
    {
        if (GetCurrentMode() != CameraMode.TopDown) return;
        float secs = (secondsOverride > 0f) ? secondsOverride : returnSeconds;
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(staticHomePos, staticHomeRot, CameraMode.TopDown));
    }

    void UpdateTransition() { /* reservado por si quieres interpolaciones extra */ }
}
