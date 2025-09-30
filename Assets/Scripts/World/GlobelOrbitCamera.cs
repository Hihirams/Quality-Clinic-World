using UnityEngine;
using UnityEngine.EventSystems;

public class GlobeOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;           // Asigna Earth
    public float distance = 18f;
    public float minDistance = 8f;
    public float maxDistance = 30f;

    [Header("Orbit")]
    public float orbitSpeed = 150f;
    public float pitchMin = 10f;
    public float pitchMax = 80f;

    [Header("Zoom")]
    public float zoomSpeed = 8f;

    [Header("Suavizado")]
    public bool smooth = true;
    public float smoothLerp = 10f;

    float yaw = 0f, pitch = 30f;
    float targetYaw, targetPitch, targetDistance;
    bool dragging;

    bool OverUI() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();

    void Start()
    {
        if (!target) target = GameObject.Find("Earth")?.transform;
        var dir = (transform.position - (target ? target.position : Vector3.zero)).normalized;
        if (dir.sqrMagnitude > 0.01f)
        {
            pitch = targetPitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            yaw = targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
        targetDistance = distance;
    }

    void Update()
    {
        // Orbit con botón derecho
        if (Input.GetMouseButtonDown(1) && !OverUI()) dragging = true;
        if (Input.GetMouseButtonUp(1)) dragging = false;

        if (dragging && !OverUI())
        {
            targetYaw += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
            targetPitch -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
            targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);
        }

        // Zoom con rueda (bloqueado si está sobre UI)
        if (!OverUI())
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }
        }

        if (smooth)
        {
            yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * smoothLerp);
            pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * smoothLerp);
            distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * smoothLerp);
        }
        else
        {
            yaw = targetYaw; pitch = targetPitch; distance = targetDistance;
        }

        // Posicionar cámara
        if (target)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = target.position + rot * (Vector3.back * distance);
            transform.SetPositionAndRotation(pos, rot);
        }
    }
    // PÉGALO dentro de la clase GlobeOrbitCamera (al final está bien)
public void FocusTowards(Vector3 worldDir, float desiredDistance, bool smoothFocus = true)
{
    if (!target) return;
    // worldDir es la dirección desde el centro (target.position) hacia el punto de interés
    worldDir = worldDir.normalized;

    // Convierte a ángulos yaw/pitch
    float newPitch = Mathf.Asin(worldDir.y) * Mathf.Rad2Deg;
    float newYaw   = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;

    // Aplica límites
    newPitch = Mathf.Clamp(newPitch, pitchMin, pitchMax);
    desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);

    if (smoothFocus)
    {
        // empuja los objetivos; el Update ya interpola
        targetPitch = newPitch;
        targetYaw   = newYaw;
        targetDistance = desiredDistance;
    }
    else
    {
        // salta directo
        pitch = targetPitch = newPitch;
        yaw   = targetYaw   = newYaw;
        distance = targetDistance = desiredDistance;
    }
}

}
