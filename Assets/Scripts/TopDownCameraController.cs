using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TopDownCameraController : MonoBehaviour
{
    [Header("Configuración de Vista Top-Down")]
    [SerializeField] private float cameraHeight = 80f;
    [SerializeField] private float cameraAngle = 20f; // Inclinación desde arriba (0 = perpendicular)
    [SerializeField] private Vector3 plantCenter = new Vector3(-50f, 0f, -20f); // Centro aproximado de tu planta

    [Header("Ajuste del Viewport")]
    [SerializeField] private float viewportWidth = 120f;  // Ancho que debe cubrir la cámara
    [SerializeField] private float viewportDepth = 100f;  // Profundidad que debe cubrir la cámara

    [Header("Configuración de Transición")]
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private bool smoothTransitions = true;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Control de Cámara")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool startInTopDownMode = false;

    // === NUEVO: Vista fija opcional para Top-Down ===
    [Header("Vista Top-Down Fija (opcional)")]
    [SerializeField] private bool useFixedStaticView = true;
    [SerializeField] private Vector3 fixedTopDownPosition = new Vector3(-34.9f, 139.9f, 20.3f);
    [SerializeField] private Vector3 fixedTopDownEuler = new Vector3(90f, 0f, 0f);

    public enum CameraMode { Free, TopDown }

    private CameraMode currentMode;
    private FreeCameraController freeCameraController;

    // Posiciones objetivo para transiciones (auto-fit)
    private Vector3 topDownPosition;
    private Quaternion topDownRotation;
    private Vector3 freePosition;
    private Quaternion freeRotation;

    // Variables de transición
    private bool isTransitioning = false;
    private float transitionProgress = 0f;

    // Cache de cámara
    private Camera _cam;

    // Focus/Zoom temporal en Top-Down
    private Coroutine _focusRoutine;

    void Start()
    {
        _cam = GetComponent<Camera>();
        // Hacer opcional el FreeCameraController
        freeCameraController = GetComponent<FreeCameraController>();
        if (freeCameraController == null)
        {
            Debug.LogWarning("No hay FreeCameraController; Top-Down funcionará, pero el modo Libre no podrá moverse.");
        }

        CalculateTopDownTransform();

        currentMode = startInTopDownMode ? CameraMode.TopDown : CameraMode.Free;
        if (startInTopDownMode)
        {
            SetTopDownModeImmediate();
        }

        Debug.Log("TopDownCameraController inicializado. Presiona " + toggleKey + " para alternar.");
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleCameraMode();
        if (isTransitioning) UpdateTransition();

        if (Input.GetKeyDown(KeyCode.T)) DebugCameraInfo();
    }

    void CalculateTopDownTransform()
    {
        Vector3 offset = Vector3.back * (cameraHeight * Mathf.Tan(cameraAngle * Mathf.Deg2Rad));
        topDownPosition = plantCenter + Vector3.up * cameraHeight + offset;

        Vector3 direction = (plantCenter - topDownPosition).normalized;
        topDownRotation = Quaternion.LookRotation(direction);

        if (_cam != null)
        {
            float requiredFOV = CalculateRequiredFOV();
            _cam.fieldOfView = requiredFOV;
        }
    }

    // CAMBIO: multiplicador 1.2f -> 1.4f para FOV más amplio
    float CalculateRequiredFOV()
    {
        if (_cam == null) return 60f;

        float distance = Vector3.Distance(topDownPosition, plantCenter);
        float maxDimension = Mathf.Max(viewportWidth, viewportDepth);
        float halfFOVRad = Mathf.Atan(maxDimension * 0.5f / distance);
        float fov = halfFOVRad * Mathf.Rad2Deg * 2f;
        return Mathf.Clamp(fov * 1.4f, 30f, 120f);
    }

    public void ToggleCameraMode()
    {
        if (isTransitioning) return;
        if (currentMode == CameraMode.Free) SetTopDownMode();
        else SetFreeMode();
    }

    // CAMBIO: Transición suave (con curva) en lugar de cambio inmediato
    public void SetTopDownMode()
    {
        if (currentMode == CameraMode.TopDown) return;

        // Guardar posición/rotación actuales como "libre"
        freePosition = transform.position;
        freeRotation = transform.rotation;

        if (freeCameraController != null) freeCameraController.enabled = false;

        if (smoothTransitions) StartTransition(CameraMode.TopDown);
        else SetTopDownModeImmediate();

        Debug.Log("Cambiando a vista Top-Down");
    }

    public void SetFreeMode()
    {
        if (currentMode == CameraMode.Free) return;

        if (smoothTransitions) StartTransition(CameraMode.Free);
        else SetFreeModeImmediate();

        Debug.Log("Cambiando a vista libre");
    }

    void SetTopDownModeImmediate()
    {
        if (useFixedStaticView)
        {
            transform.position = fixedTopDownPosition;
            transform.rotation = Quaternion.Euler(fixedTopDownEuler);
        }
        else
        {
            transform.position = topDownPosition;
            transform.rotation = topDownRotation;
        }

        if (freeCameraController != null) freeCameraController.enabled = false;

        currentMode = CameraMode.TopDown;
        NotifyModeChange();
    }

    void SetFreeModeImmediate()
    {
        // Volver a la última posición/rotación guardadas, o a un fallback
        transform.position = (freePosition != Vector3.zero) ? freePosition : new Vector3(-78.38f, 25f, -33.06f);
        transform.rotation = (freeRotation != Quaternion.identity) ? freeRotation : Quaternion.Euler(10f, 41.1f, 0f);

        if (freeCameraController != null) freeCameraController.enabled = true;

        currentMode = CameraMode.Free;
        NotifyModeChange();
    }

    void StartTransition(CameraMode targetMode)
    {
        isTransitioning = true;
        transitionProgress = 0f;

        // Si vamos a TopDown, apagamos movimientos libres desde el inicio
        if (targetMode == CameraMode.TopDown && freeCameraController != null)
            freeCameraController.enabled = false;
    }

    void UpdateTransition()
    {
        transitionProgress += Time.deltaTime * transitionSpeed;

        if (transitionProgress >= 1f)
        {
            transitionProgress = 1f;
            isTransitioning = false;

            if (currentMode == CameraMode.Free) SetTopDownModeImmediate();
            else SetFreeModeImmediate();
            return;
        }

        Vector3 startPos, endPos;
        Quaternion startRot, endRot;

        if (currentMode == CameraMode.Free)
        {
            // Transición hacia Top-Down
            startPos = (freePosition != Vector3.zero) ? freePosition : transform.position;
            startRot = (freeRotation != Quaternion.identity) ? freeRotation : transform.rotation;

            if (useFixedStaticView)
            {
                endPos = fixedTopDownPosition;
                endRot = Quaternion.Euler(fixedTopDownEuler);
            }
            else
            {
                endPos = topDownPosition;
                endRot = topDownRotation;
            }
        }
        else
        {
            // Transición desde Top-Down hacia Libre
            if (useFixedStaticView)
            {
                startPos = fixedTopDownPosition;
                startRot = Quaternion.Euler(fixedTopDownEuler);
            }
            else
            {
                startPos = topDownPosition;
                startRot = topDownRotation;
            }

            endPos = (freePosition != Vector3.zero) ? freePosition : new Vector3(-78.38f, 25f, -33.06f);
            endRot = (freeRotation != Quaternion.identity) ? freeRotation : Quaternion.Euler(10f, 41.1f, 0f);
        }

        float t = Mathf.Clamp01(transitionProgress);
        float eased = transitionCurve != null ? transitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(startPos, endPos, eased);
        transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
    }

    void NotifyModeChange()
    {
        if (currentMode == CameraMode.TopDown)
            Debug.Log("Vista Top-Down activa - todas las áreas visibles");
        else
            Debug.Log("Vista libre activa - navegación manual");
    }

    void DebugCameraInfo()
    {
        Debug.Log("=== INFO DE CÁMARA ===");
        Debug.Log($"Modo actual: {currentMode}");
        Debug.Log($"Posición: {transform.position}");
        Debug.Log($"Rotación: {transform.eulerAngles}");
        Debug.Log($"FOV: {_cam?.fieldOfView}");
        Debug.Log($"En transición: {isTransitioning} ({transitionProgress:F2})");
    }

    public bool IsTopDownMode() => currentMode == CameraMode.TopDown;
    public bool IsFreeMode() => currentMode == CameraMode.Free;
    public bool IsTransitioning() => isTransitioning;

    public void SetPlantCenter(Vector3 newCenter)
    {
        plantCenter = newCenter;
        CalculateTopDownTransform();
        if (currentMode == CameraMode.TopDown && !isTransitioning) SetTopDownModeImmediate();
    }

    public void SetViewportSize(float width, float depth)
    {
        viewportWidth = width;
        viewportDepth = depth;
        CalculateTopDownTransform();
        if (currentMode == CameraMode.TopDown && !isTransitioning) SetTopDownModeImmediate();
    }

    [System.Serializable]
    public class TopDownSettings
    {
        public float cameraHeight = 80f;
        public float cameraAngle = 20f;
        public Vector3 plantCenter = Vector3.zero;
        public float viewportWidth = 120f;
        public float viewportDepth = 100f;
    }

    public void ApplySettings(TopDownSettings settings)
    {
        cameraHeight = settings.cameraHeight;
        cameraAngle = settings.cameraAngle;
        plantCenter = settings.plantCenter;
        viewportWidth = settings.viewportWidth;
        viewportDepth = settings.viewportDepth;

        CalculateTopDownTransform();
        if (currentMode == CameraMode.TopDown && !isTransitioning) SetTopDownModeImmediate();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(plantCenter, new Vector3(viewportWidth, 2f, viewportDepth));

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(topDownPosition, 2f);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(topDownPosition, (plantCenter - topDownPosition).normalized * 10f);
    }

    // ===== Wrappers opcionales para compatibilidad
    public void EnableTopDownView() { SetTopDownMode(); }
    public void EnableFreeView() { SetFreeMode(); }

    // Zoom temporal en Top-Down
    /// <summary>
    /// Hace un focus/zoom temporal en la posición del área. zoomLevel > 1 acerca, < 1 aleja.
    /// Mantiene el ángulo de Top-Down. Vuelve suavemente a la vista original.
    /// </summary>
    public void FocusOnAreaTopDown(Transform area, float zoomLevel = 1.4f)
    {
        if (area == null || _cam == null) return;
        if (currentMode != CameraMode.TopDown) return; // Sólo tiene sentido en Top-Down

        if (_focusRoutine != null) StopCoroutine(_focusRoutine);
        _focusRoutine = StartCoroutine(FocusOnAreaRoutine(area.position, Mathf.Max(0.5f, zoomLevel)));
    }

    IEnumerator FocusOnAreaRoutine(Vector3 targetPoint, float zoomLevel)
    {
        // Guardar estado actual
        Vector3 originalTDPos;
        Quaternion originalTDRot;

        // Respetar vista fija si está activa
        if (useFixedStaticView)
        {
            originalTDPos = fixedTopDownPosition;
            originalTDRot = Quaternion.Euler(fixedTopDownEuler);
        }
        else
        {
            originalTDPos = topDownPosition;
            originalTDRot = topDownRotation;
        }

        float originalFOV = _cam.fieldOfView;

        // Calcular un topDownPosition que centre el área manteniendo altura/ángulo
        Vector3 offset = Vector3.back * (cameraHeight * Mathf.Tan(cameraAngle * Mathf.Deg2Rad));
        Vector3 targetTDPos = targetPoint + Vector3.up * cameraHeight + offset;
        Quaternion targetTDRot = Quaternion.LookRotation((targetPoint - targetTDPos).normalized);

        // FOV objetivo (acerca dividiendo por zoomLevel)
        float targetFOV = Mathf.Clamp(originalFOV / zoomLevel, 25f, 120f);

        float durIn = 0.55f;
        float hold = 1.1f;
        float durOut = 0.6f;

        // Animación de entrada
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, durIn);
            float e = transitionCurve != null ? transitionCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(originalTDPos, targetTDPos, e);
            transform.rotation = Quaternion.Slerp(originalTDRot, targetTDRot, e);
            _cam.fieldOfView = Mathf.Lerp(originalFOV, targetFOV, e);
            yield return null;
        }

        // Mantener un instante sobre el área
        if (hold > 0f) yield return new WaitForSeconds(hold);

        // Animación de regreso
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, durOut);
            float e = transitionCurve != null ? transitionCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(targetTDPos, originalTDPos, e);
            transform.rotation = Quaternion.Slerp(targetTDRot, originalTDRot, e);
            _cam.fieldOfView = Mathf.Lerp(targetFOV, originalFOV, e);
            yield return null;
        }

        _focusRoutine = null;
    }
}
