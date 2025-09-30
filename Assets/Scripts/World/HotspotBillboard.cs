using UnityEngine;
using TMPro;

public class HotspotBillboard : MonoBehaviour
{
    [Header("Refs")]
    public Transform earth;          // Se asigna en Init
    public Camera targetCamera;      // Si es null, usa Camera.main
    public TextMeshPro label;        // Texto flotante (TextMeshPro 3D)

    [Header("Offset")]
    public float altitude = 0.5f;    // Separación desde la superficie del globo

    [Header("Billboard")]
    public bool yawOnly = true;      // Solo girar en Y
    public float rotationLerp = 10f; // Suavizado de giro

    [Header("Escala con distancia")]
    public float baseScale = 1f;
    public float distanceFactor = 0.02f;
    public float minScale = 0.6f;
    public float maxScale = 2.0f;
    public float scaleLerp = 8f;

    // Dirección LOCAL (en el espacio del Earth), unitario
    Vector3 localDir = Vector3.forward;

    // -------- INITS --------

    // 1) Ya tienes esta versión: recibe dirección LOCAL sobre la esfera
    public void Init(Transform earthRef, Camera cameraRef, Vector3 localDirOnSphere, string display)
    {
        earth = earthRef;
        targetCamera = cameraRef;
        localDir = localDirOnSphere.normalized;

        if (!label) label = GetComponentInChildren<TextMeshPro>();
        if (label) label.text = display;

        UpdatePosition();
    }

    // 2) Si en algún punto quieres inicializar con POSICIÓN en MUNDO:
    public void InitFromWorldPos(Transform earthRef, Camera cameraRef, Vector3 anchorWorldPos, string display)
    {
        earth = earthRef;
        targetCamera = cameraRef;

        // pá­salo a LOCAL y normaliza
        Vector3 localPos = earth.InverseTransformPoint(anchorWorldPos);
        localDir = localPos.normalized;

        if (!label) label = GetComponentInChildren<TextMeshPro>();
        if (label) label.text = display;

        UpdatePosition();
    }

    // 3) O con DIRECCIÓN en MUNDO:
    public void InitFromWorldDir(Transform earthRef, Camera cameraRef, Vector3 worldDir, string display)
    {
        earth = earthRef;
        targetCamera = cameraRef;

        // conviértelo a LOCAL y normaliza
        localDir = earth.InverseTransformDirection(worldDir.normalized).normalized;

        if (!label) label = GetComponentInChildren<TextMeshPro>();
        if (label) label.text = display;

        UpdatePosition();
    }

    // -------- VISUAL --------
    public void SetVisual(Color color, float fontSize = 2f)
    {
        var rend = GetComponent<Renderer>();
        if (rend && rend.material) rend.material.color = color;
        if (label) label.fontSize = fontSize;
    }

    void LateUpdate()
    {
        if (!earth) return;
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        // 1) Posición sobre el globo
        UpdatePosition();

        // 2) Orientar hacia cámara
        Vector3 toCam = targetCamera.transform.position - transform.position;
        if (yawOnly) toCam.y = 0f;

        if (toCam.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toCam);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerp);
        }

        // 3) Escalar con distancia
        float d = Vector3.Distance(transform.position, targetCamera.transform.position);
        float targetScale = Mathf.Clamp(baseScale + d * distanceFactor, minScale, maxScale);
        Vector3 goal = Vector3.one * targetScale;
        transform.localScale = Vector3.Lerp(transform.localScale, goal, Time.deltaTime * scaleLerp);
    }
void UpdatePosition()
{
    if (!earth) return;

    // 1) Calcular el radio base del Earth
    float baseRadius = 0.5f; // default para esfera unitaria
    var sc = earth.GetComponent<SphereCollider>();
    if (sc) baseRadius = sc.radius;

    // 2) Aplicar la escala SOLO del eje más grande
    float maxScale = Mathf.Max(
        Mathf.Abs(earth.lossyScale.x), 
        Mathf.Abs(earth.lossyScale.y), 
        Mathf.Abs(earth.lossyScale.z)
    );
    
    float worldRadius = baseRadius * maxScale;
    
    // 3) Calcular posición sobre la superficie
    Vector3 worldDir = earth.TransformDirection(localDir).normalized;
    Vector3 surfacePos = earth.position + worldDir * worldRadius;
    
    // 4) Aplicar altitud sobre la superficie
    transform.position = surfacePos + worldDir * altitude;

    // Debug para verificar
    Debug.Log($"[HS] {label?.text} - radius:{worldRadius:F2} scale:{maxScale:F2} dist:{Vector3.Distance(earth.position, transform.position):F2}");
}

}
