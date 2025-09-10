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
    // 🔁 CAMBIADO: posición/rotación fija según tu captura
    [SerializeField] private Vector3 fixedTopDownPosition = new Vector3(-34.9f, 168.6f, 20.3f);
    [SerializeField] private Vector3 fixedTopDownEuler = new Vector3(90f, 0f, 0f);

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
        if (Input.GetKeyDown(toggleKey)) ToggleCameraMode();
        if (isTransitioning) UpdateTransition();
    }

    // Lo usa AreaCard
    public bool IsUsingFixedStaticView() => useFixedStaticView;

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
        if (isTransitioning) return;
        if (currentMode == CameraMode.Free) SetTopDownMode();
        else SetFreeMode();
    }

    public void SetTopDownModeImmediate()
    {
        currentMode = CameraMode.TopDown;
        targetMode = CameraMode.TopDown;

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

        if (freeCameraController != null)
            freeCameraController.enabled = false;

        _overlay = _overlay ?? FindFirstObjectByType<AreaOverlayPainter>();
        _overlay?.SetTopDownMode(true);
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

        StartTransition(targetPos, targetRot, CameraMode.TopDown);

        _overlay = _overlay ?? FindFirstObjectByType<AreaOverlayPainter>();
        _overlay?.SetTopDownMode(true);
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

    public void FocusOnAreaTopDown(Transform area, float zoomSeconds = 0.75f)
    {
        if (area == null || _cam == null) return;
        StartCoroutine(FocusRoutine(area, zoomSeconds));
    }

    IEnumerator FocusRoutine(Transform target, float seconds)
    {
        Vector3 targetPos = target.position + Vector3.up * cameraHeight;
        Quaternion targetRot = Quaternion.Euler(90f, 0f, 0f);
        yield return TransitionRoutine(targetPos, targetRot, CameraMode.TopDown);
    }

    void UpdateTransition() { }
}
