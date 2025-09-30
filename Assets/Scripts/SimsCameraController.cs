using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador de cámara libre estilo "The Sims" con movimiento WASD, rotación, pan y zoom.
/// Permite navegación libre por el mapa 3D del dashboard con vista isométrica ajustable.
/// </summary>
/// <remarks>
/// Responsabilidades:
/// - Movimiento libre con teclado (WASD) con velocidad rápida (Shift)
/// - Rotación de cámara con click derecho + arrastre
/// - Pan con click medio + arrastre
/// - Zoom vertical con scroll del mouse
/// - Enfoque programático en áreas específicas
/// - Límites opcionales del mapa
/// - Detección de UI para evitar conflictos con controles
/// 
/// Dependencias:
/// - EventSystem para detección de UI
/// - Camera principal para raycasts
/// 
/// Flujo principal:
/// Update → HandleKeyboardMovement/MouseControls/Zoom → ApplyMovementAndRotation
/// </remarks>
public class SimsCameraController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuración de Movimiento")]
    [Tooltip("Velocidad de movimiento normal con WASD")]
    public float moveSpeed = 20f;

    [Tooltip("Velocidad de movimiento rápido con Shift + WASD")]
    public float fastMoveSpeed = 40f;

    [Tooltip("Sensibilidad del mouse para rotación")]
    public float mouseSensitivity = 3f;

    [Header("Configuración de Zoom")]
    [Tooltip("Velocidad de zoom con scroll del mouse")]
    public float zoomSpeed = 10f;

    [Tooltip("Altura mínima permitida de la cámara")]
    public float minHeight = 2f;

    [Tooltip("Altura máxima permitida de la cámara")]
    public float maxHeight = 100f;

    [Header("Configuración de Límites del Mapa")]
    [Tooltip("Activar límites del mapa")]
    public bool useBounds = false;

    [Tooltip("Centro del mapa para límites")]
    public Vector3 mapCenter = Vector3.zero;

    [Tooltip("Tamaño total del mapa (lado del cuadrado)")]
    public float mapSize = 200f;

    [Header("Configuración de Suavizado")]
    [Tooltip("Activar movimiento suave interpolado")]
    public bool smoothMovement = true;

    [Tooltip("Tiempo de suavizado para interpolación")]
    public float smoothTime = 0.1f;

    [Header("Configuración de Animación")]
    [Tooltip("Duración por defecto de animaciones de focus/return")]
    public float defaultAnimationDuration = 1.5f;

    #endregion

    #region Private State

    // Posición objetivo para interpolación
    private Vector3 targetPosition;

    // Velocidad actual para SmoothDamp
    private Vector3 currentVelocity;

    // Rotación horizontal (eje Y)
    private float rotationX = 0f;

    // Rotación vertical (eje X)
    private float rotationY = 0f;

    // Estado de arrastre con botón derecho (rotación)
    private bool isDragging = false;

    // Estado de pan con botón medio
    private bool isPanning = false;

    // Última posición mundial del mouse para pan
    private Vector3 lastMouseWorldPos;

    // Posición inicial de la cámara para reset
    private Vector3 initialPosition;

    // Rotación inicial de la cámara para reset
    private Quaternion initialRotation;

    // Cache de EventSystem para evitar lookups repetidos
    private EventSystem cachedEventSystem;

    // Cache de cámara principal
    private Camera cachedCamera;

    // Estados de animación programática
    private bool isAnimatingFocus = false;
    private Vector3 animStartPosition;
    private Quaternion animStartRotation;
    private Vector3 animTargetPosition;
    private Quaternion animTargetRotation;
    private float animProgress = 0f;
    private float animDuration = 1f;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        // Cachear componentes
        cachedEventSystem = EventSystem.current;
        cachedCamera = Camera.main;

        if (cachedCamera == null)
        {
            Debug.LogError("[SimsCameraController] No se encontró Camera.main. Asegúrate de que la cámara tenga el tag 'MainCamera'.");
        }

        // Establecer posición/rotación inicial solicitadas
        initialPosition = new Vector3(-78.38f, 25f, -33.06f);
        initialRotation = Quaternion.Euler(10f, 41.1f, 0f);

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        targetPosition = transform.position;

        // Obtener rotación inicial
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.y; // 41.1
        rotationY = euler.x; // 10

        QCLog.Info("[SimsCameraController] Cámara libre estilo Sims inicializada");
        QCLog.Info("Controles: WASD (mover), Shift+WASD (rápido), Click derecho (rotar), Click medio (pan), Scroll (zoom), R (reset)");
    }

    private void Update()
    {
        // Si hay animación programática activa, manejarla y salir
        if (isAnimatingFocus)
        {
            UpdateFocusAnimation();
            return;
        }

        // Verificar si el puntero está sobre UI (para evitar conflictos)
        bool pointerOverUI = IsPointerOverUI();

        if (pointerOverUI)
        {
            // Liberar estados de arrastre al entrar en UI
            if (isDragging || isPanning)
            {
                isDragging = false;
                isPanning = false;
                Cursor.lockState = CursorLockMode.None;
            }
        }
        else
        {
            // Procesar controles normales solo si no estamos sobre UI
            HandleKeyboardMovement();
            HandleMouseControls();
            HandleZoom();
        }

        // Aplicar movimiento y rotación (siempre, para suavizado)
        ApplyMovementAndRotation();

        // Debug de posición con C
        if (Input.GetKeyDown(KeyCode.C))
        {
            QCLog.Info($"[SimsCameraController] Posición: {transform.position}, Rotación: {transform.eulerAngles}");
        }

        // Reset con R
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCamera();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dibujar límites del mapa en el editor
        if (useBounds)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(mapCenter, Vector3.one * mapSize);

            // Dibujar límites de altura
            Gizmos.color = Color.blue;
            Vector3 minHeightPos = mapCenter + Vector3.up * minHeight;
            Vector3 maxHeightPos = mapCenter + Vector3.up * maxHeight;
            Gizmos.DrawWireCube(minHeightPos, new Vector3(mapSize, 0.1f, mapSize));
            Gizmos.DrawWireCube(maxHeightPos, new Vector3(mapSize, 0.1f, mapSize));
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Enfoca la cámara en un área específica con animación suave.
    /// API unificada compatible con TopDownCameraController.
    /// </summary>
    /// <param name="focusPoint">Punto central donde enfocar</param>
    /// <param name="distance">Distancia desde el punto de enfoque</param>
    /// <param name="duration">Duración de la animación en segundos</param>
    public void FocusOnArea(Vector3 focusPoint, float distance, float duration)
    {
        QCLog.Info($"[SimsCameraController] FocusOnArea llamado - Punto: {focusPoint}, Distancia: {distance}, Duración: {duration}s");

        // Calcular posición de cámara con offset isométrico
        Vector3 offset = new Vector3(-distance * 0.7f, distance * 0.8f, -distance * 0.7f);
        Vector3 newTargetPosition = focusPoint + offset;

        // Calcular rotación para apuntar al área
        Vector3 direction = (focusPoint - newTargetPosition).normalized;
        float newRotationX = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float newRotationY = Mathf.Asin(-direction.y) * Mathf.Rad2Deg;
        newRotationY = Mathf.Clamp(newRotationY, 10f, 80f);

        Quaternion newTargetRotation = Quaternion.Euler(newRotationY, newRotationX, 0f);

        // Iniciar animación
        StartFocusAnimation(newTargetPosition, newTargetRotation, duration);
    }

    /// <summary>
    /// Sobrecarga para compatibilidad con código existente que usa Transform.
    /// </summary>
    /// <param name="areaTransform">Transform del área a enfocar</param>
    /// <param name="distance">Distancia desde el área</param>
    public void FocusOnArea(Transform areaTransform, float distance = 20f)
    {
        if (areaTransform == null)
        {
            Debug.LogError("[SimsCameraController] FocusOnArea: areaTransform es NULL");
            return;
        }

        FocusOnArea(areaTransform.position, distance, defaultAnimationDuration);
    }

    /// <summary>
    /// Retorna la cámara a su posición inicial con animación suave.
    /// API unificada compatible con TopDownCameraController.
    /// </summary>
    /// <param name="duration">Duración de la animación en segundos</param>
    public void ReturnToStaticHome(float duration)
    {
        QCLog.Info($"[SimsCameraController] ReturnToStaticHome llamado - Duración: {duration}s");
        StartFocusAnimation(initialPosition, initialRotation, duration);
    }

    /// <summary>
    /// Resetea la cámara instantáneamente a su posición inicial.
    /// </summary>
    public void ResetCamera()
    {
        // Cancelar cualquier animación en curso
        isAnimatingFocus = false;

        // Restablecer a la vista inicial
        targetPosition = initialPosition;
        rotationX = 41.1f;
        rotationY = 10f;

        transform.position = targetPosition;
        transform.rotation = initialRotation;

        QCLog.Info("[SimsCameraController] Cámara reseteada a vista inicial");
    }

    /// <summary>
    /// Establece los límites del mapa para restringir el movimiento de la cámara.
    /// </summary>
    /// <param name="center">Centro del área del mapa</param>
    /// <param name="size">Tamaño del área (lado del cuadrado)</param>
    public void SetMapBounds(Vector3 center, float size)
    {
        mapCenter = center;
        mapSize = size;
        useBounds = true;

        QCLog.Info($"[SimsCameraController] Límites del mapa establecidos - Centro: {center}, Tamaño: {size}");
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Verifica si el puntero del mouse está sobre un elemento de UI.
    /// Usa cache del EventSystem para evitar lookups repetidos.
    /// </summary>
    /// <returns>True si el puntero está sobre UI</returns>
    private bool IsPointerOverUI()
    {
        // Usar cache del EventSystem
        if (cachedEventSystem == null)
        {
            cachedEventSystem = EventSystem.current;
        }

        return cachedEventSystem != null && cachedEventSystem.IsPointerOverGameObject();
    }

    /// <summary>
    /// Maneja el movimiento de la cámara con teclado (WASD).
    /// Soporta movimiento rápido con Shift.
    /// </summary>
    private void HandleKeyboardMovement()
    {
        // Determinar velocidad (normal o rápida)
        bool isFastMove = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isFastMove ? fastMoveSpeed : moveSpeed;

        // Obtener input de teclado
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        // Solo procesar si hay input
        if (horizontal != 0 || vertical != 0)
        {
            // Calcular dirección de movimiento basada en la orientación actual de la cámara
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Mantener movimiento horizontal (ignorar componente Y)
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Combinar direcciones
            Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
            Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;

            targetPosition += movement;

            // Aplicar límites si están activos
            if (useBounds)
            {
                ApplyMapBounds();
            }
        }
    }

    /// <summary>
    /// Maneja los controles del mouse: rotación (click derecho) y pan (click medio).
    /// Incluye protección contra inputs sobre UI.
    /// </summary>
    private void HandleMouseControls()
    {
        // ========= ROTACIÓN (Click derecho) =========
        if (Input.GetMouseButtonDown(1) && !IsPointerOverUI())
        {
            isDragging = true;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
            Cursor.lockState = CursorLockMode.None;
        }

        // Si durante el drag el puntero entra a la UI, cancelar
        if (isDragging && IsPointerOverUI())
        {
            isDragging = false;
            Cursor.lockState = CursorLockMode.None;
        }

        if (isDragging)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            rotationX += mouseX;
            rotationY -= mouseY;

            // Limitar rotación vertical para evitar volteretas
            rotationY = Mathf.Clamp(rotationY, 10f, 80f);
        }

        // ========= PAN (Click medio) =========
        if (Input.GetMouseButtonDown(2) && !IsPointerOverUI())
        {
            isPanning = true;
            lastMouseWorldPos = GetMouseWorldPosition();
        }

        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        // Si durante el pan el puntero entra a la UI, cancelar
        if (isPanning && IsPointerOverUI())
        {
            isPanning = false;
        }

        if (isPanning)
        {
            Vector3 currentMouseWorldPos = GetMouseWorldPosition();
            
            // Validar posiciones válidas (evitar Vector3.zero de raycast fallido)
            if (lastMouseWorldPos != Vector3.zero && currentMouseWorldPos != Vector3.zero)
            {
                Vector3 difference = lastMouseWorldPos - currentMouseWorldPos;
                targetPosition += difference;

                if (useBounds)
                {
                    ApplyMapBounds();
                }
            }

            // Actualizar última posición para el siguiente frame
            lastMouseWorldPos = currentMouseWorldPos;
        }
    }

    /// <summary>
    /// Convierte la posición del mouse en pantalla a posición mundial en el plano Y=0.
    /// Usa raycast contra un plano horizontal para determinar la posición.
    /// </summary>
    /// <returns>Posición mundial del mouse, o Vector3.zero si el raycast falla</returns>
    private Vector3 GetMouseWorldPosition()
    {
        if (cachedCamera == null)
        {
            return Vector3.zero;
        }

        // Plano horizontal a altura 0 para pan
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);

        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Maneja el zoom vertical con scroll del mouse.
    /// Bloqueado si el puntero está sobre UI (para evitar conflictos con scroll de paneles).
    /// </summary>
    private void HandleZoom()
    {
        // Bloquear zoom si el puntero está sobre UI
        if (IsPointerOverUI())
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Zoom vertical estilo Sims (subir/bajar la cámara en el eje Y)
            float zoomAmount = scroll * zoomSpeed;
            targetPosition.y += zoomAmount;

            if (useBounds)
            {
                targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
            }
        }
    }

    /// <summary>
    /// Aplica los límites del mapa a la posición objetivo.
    /// Restringe movimiento en X, Z y altura en Y.
    /// </summary>
    private void ApplyMapBounds()
    {
        if (useBounds)
        {
            float halfSize = mapSize * 0.5f;
            targetPosition.x = Mathf.Clamp(targetPosition.x, mapCenter.x - halfSize, mapCenter.x + halfSize);
            targetPosition.z = Mathf.Clamp(targetPosition.z, mapCenter.z - halfSize, mapCenter.z + halfSize);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        }
    }

    /// <summary>
    /// Aplica la posición y rotación objetivo a la cámara.
    /// Usa interpolación suave si smoothMovement está activo.
    /// </summary>
    private void ApplyMovementAndRotation()
    {
        // Aplicar posición
        if (smoothMovement)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }

        // Aplicar rotación
        Quaternion targetRotation = Quaternion.Euler(rotationY, rotationX, 0f);
        if (smoothMovement)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    /// <summary>
    /// Inicia una animación programática hacia una posición y rotación objetivo.
    /// Desactiva controles manuales durante la animación.
    /// </summary>
    /// <param name="targetPos">Posición objetivo</param>
    /// <param name="targetRot">Rotación objetivo</param>
    /// <param name="duration">Duración de la animación en segundos</param>
    private void StartFocusAnimation(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        animStartPosition = transform.position;
        animStartRotation = transform.rotation;
        animTargetPosition = targetPos;
        animTargetRotation = targetRot;
        animProgress = 0f;
        animDuration = Mathf.Max(duration, 0.1f); // Mínimo 0.1s para evitar división por cero
        isAnimatingFocus = true;

        // Liberar cualquier estado de arrastre
        isDragging = false;
        isPanning = false;
        Cursor.lockState = CursorLockMode.None;

        QCLog.Info($"[SimsCameraController] Animación iniciada - Duración: {animDuration}s");
    }

    /// <summary>
    /// Actualiza la animación de enfoque en cada frame.
    /// Usa interpolación suave (ease-in-out) para transiciones naturales.
    /// </summary>
    private void UpdateFocusAnimation()
    {
        animProgress += Time.deltaTime / animDuration;

        if (animProgress >= 1f)
        {
            // Animación completada
            animProgress = 1f;
            isAnimatingFocus = false;

            // Establecer valores finales exactos
            transform.position = animTargetPosition;
            transform.rotation = animTargetRotation;

            // Actualizar targetPosition y rotaciones para controles manuales
            targetPosition = animTargetPosition;
            Vector3 euler = animTargetRotation.eulerAngles;
            rotationX = euler.y;
            rotationY = euler.x;

            QCLog.Info("[SimsCameraController] Animación de enfoque completada");
        }
        else
        {
            // Interpolación suave con ease-in-out
            float t = EaseInOutCubic(animProgress);

            transform.position = Vector3.Lerp(animStartPosition, animTargetPosition, t);
            transform.rotation = Quaternion.Slerp(animStartRotation, animTargetRotation, t);
        }
    }

    /// <summary>
    /// Función de easing cúbico para animaciones suaves.
    /// Proporciona aceleración al inicio y desaceleración al final.
    /// </summary>
    /// <param name="t">Progreso normalizado (0-1)</param>
    /// <returns>Valor interpolado con easing</returns>
    private float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }
        else
        {
            float f = (2f * t - 2f);
            return 0.5f * f * f * f + 1f;
        }
    }

    #endregion

    #region Debug

    // Los métodos de debug ya están integrados en Update (tecla C y R)
    // OnDrawGizmosSelected está en Unity Callbacks

    #endregion
}
