using UnityEngine;
using UnityEngine.EventSystems;

public class FreeCameraController : MonoBehaviour
{

    // Si el puntero est� sobre UI, no proceses controles de c�mara

[Header("Configuraci�n de Movimiento")]
    public float moveSpeed = 20f;
    public float fastMoveSpeed = 40f;
    public float mouseSensitivity = 3f;

    [Header("Configuraci�n de Zoom")]
    public float zoomSpeed = 10f;
    public float minHeight = 2f;
    public float maxHeight = 100f;

    [Header("Configuraci�n de L�mites del Mapa")]
    public bool useBounds = false;  // Desactivado por defecto para movimiento libre
    public Vector3 mapCenter = Vector3.zero;
    public float mapSize = 200f;    // Mapa m�s grande

    [Header("Configuraci�n de Suavizado")]
    public bool smoothMovement = true;
    public float smoothTime = 0.1f;

    // Variables internas
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool isDragging = false;
    private bool isPanning = false;
    private Vector3 lastMouseWorldPos;

    void Start()
    {
        // Posici�n/rotaci�n inicial solicitadas
        transform.position = new Vector3(-78.38f, 25f, -33.06f);
        transform.rotation = Quaternion.Euler(10f, 41.1f, 0f);

        targetPosition = transform.position;

        // Obtener rotaci�n inicial
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.y; // 41.1
        rotationY = euler.x; // 10

        Debug.Log("C�mara libre estilo Sims inicializada");
        Debug.Log("Controles:");
        Debug.Log("- WASD: Mover c�mara libremente");
        Debug.Log("- Shift + WASD: Movimiento r�pido");
        Debug.Log("- Click derecho + rat�n: Rotar c�mara");
        Debug.Log("- Click medio + rat�n: Pan/arrastrar vista");
        Debug.Log("- Scroll: Subir/bajar c�mara");
        Debug.Log("- R: Resetear posici�n");
    }

    void Update()
    {
        // Keep camera interpolation even when cursor is over UI
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (pointerOverUI)
        {
            // Suelta estados para no arrastrar acciones al salir del bot�n
            isDragging = false;
            isPanning = false;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            HandleKeyboardMovement();
            HandleMouseControls();
            HandleZoom();
        }

        ApplyMovementAndRotation();

        // Debug con C
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log($"Posici�n c�mara: {transform.position}, Rotaci�n: {transform.eulerAngles}");
        }

        // Reset con R
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCamera();
        }
    }

    // --- UI Guard: true si el puntero est� sobre la UI ---
    bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void HandleKeyboardMovement()
    {
        bool isFastMove = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isFastMove ? fastMoveSpeed : moveSpeed;

        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        if (horizontal != 0 || vertical != 0)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Mantener movimiento horizontal
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
            Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;

            targetPosition += movement;

            if (useBounds)
            {
                ApplyMapBounds();
            }
        }
    }

    void HandleMouseControls()
    {
        // ========= ROTACI�N (Click derecho) =========
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

        // Si durante el drag el puntero entra a la UI, cancelar para no �cruzar� inputs
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

            // Limitar rotaci�n vertical para evitar volteretas
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
            if (lastMouseWorldPos != Vector3.zero && currentMouseWorldPos != Vector3.zero)
            {
                Vector3 difference = lastMouseWorldPos - currentMouseWorldPos;
                targetPosition += difference;

                if (useBounds)
                {
                    ApplyMapBounds();
                }
            }
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        // Plano a la altura 0 para pan
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    void HandleZoom()
    {
        // *** Bloquear zoom si el puntero est� sobre UI (scroll del panel de detalle, etc.) ***
        if (IsPointerOverUI()) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Zoom vertical estilo Sims (subir/bajar la c�mara)
            float zoomAmount = scroll * zoomSpeed;
            targetPosition.y += zoomAmount;

            if (useBounds)
            {
                targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
            }
        }
    }

    void ApplyMapBounds()
    {
        if (useBounds)
        {
            float halfSize = mapSize * 0.5f;
            targetPosition.x = Mathf.Clamp(targetPosition.x, mapCenter.x - halfSize, mapCenter.x + halfSize);
            targetPosition.z = Mathf.Clamp(targetPosition.z, mapCenter.z - halfSize, mapCenter.z + halfSize);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        }
    }

    void ApplyMovementAndRotation()
    {
        // Aplicar posici�n
        if (smoothMovement)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }

        // Aplicar rotaci�n
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

    public void ResetCamera()
    {
        // Restablecer a la vista inicial solicitada
        targetPosition = new Vector3(-78.38f, 25f, -33.06f);
        rotationX = 41.1f;
        rotationY = 10f;

        transform.position = targetPosition;
        transform.rotation = Quaternion.Euler(10f, 41.1f, 0f);

        Debug.Log("C�mara reseteada a la vista inicial personalizada");
    }

    public void FocusOnArea(Transform areaTransform, float distance = 20f)
    {
        if (areaTransform == null)
        {
            Debug.LogError("? FocusOnArea: areaTransform es NULL!");
            return;
        }

        Vector3 areaPosition = areaTransform.position;

        // DEBUG CR�TICO
        Debug.Log($"?? FOCUS ON AREA LLAMADO:");
        Debug.Log($"?? �rea: {areaTransform.name}");
        Debug.Log($"?? Posici�n del �rea: {areaPosition}");
        Debug.Log($"?? Posici�n actual de la c�mara: {transform.position}");

        // Vista isom�trica del �rea
        Vector3 offset = new Vector3(-distance * 0.7f, distance * 0.8f, -distance * 0.7f);
        targetPosition = areaPosition + offset;

        // Apuntar hacia el �rea
        Vector3 direction = (areaPosition - targetPosition).normalized;
        float newRotationX = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float newRotationY = Mathf.Asin(-direction.y) * Mathf.Rad2Deg;

        newRotationY = Mathf.Clamp(newRotationY, 10f, 80f);

        rotationX = newRotationX;
        rotationY = newRotationY;

        Debug.Log($"?? Nueva rotaci�n X: {rotationX}, Y: {rotationY}");
        Debug.Log($"?? C�mara enfocada en �rea: {areaTransform.name}");

        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget < 1f)
        {
            Debug.LogWarning($"?? La c�mara ya est� cerca del �rea {areaTransform.name}. Distancia: {distanceToTarget}");
        }
    }

    public void SetMapBounds(Vector3 center, float size)
    {
        mapCenter = center;
        mapSize = size;
        useBounds = true;

        Debug.Log($"L�mites del mapa establecidos - Centro: {center}, Tama�o: {size}");
    }

    void OnDrawGizmosSelected()
    {
        // Dibujar l�mites del mapa en el editor
        if (useBounds)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(mapCenter, Vector3.one * mapSize);

            // Dibujar l�mites de altura
            Gizmos.color = Color.blue;
            Vector3 minHeightPos = mapCenter + Vector3.up * minHeight;
            Vector3 maxHeightPos = mapCenter + Vector3.up * maxHeight;
            Gizmos.DrawWireCube(minHeightPos, new Vector3(mapSize, 0.1f, mapSize));
            Gizmos.DrawWireCube(maxHeightPos, new Vector3(mapSize, 0.1f, mapSize));
        }
    }
}


