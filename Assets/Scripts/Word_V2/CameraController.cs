using UnityEngine;

namespace Word_V2
{
    // SCRIPT OPCIONAL - Solo si quieres más control de cámara
    public class CameraController : MonoBehaviour
    {
    [Header("Target")]
    public Transform target;
    
    [Header("Distance Settings")]
    public float distance = 5f;
    public float minDistance = 3f;
    public float maxDistance = 10f;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;
    public bool allowRotation = false;
    
    private Vector3 offset;

    void Start()
    {
        if (target == null)
        {
            GameObject planet = GameObject.FindGameObjectWithTag("Planet");
            if (planet != null)
                target = planet.transform;
        }
        
        offset = new Vector3(0, 0, -distance);
        transform.position = target.position + offset;
        transform.LookAt(target);
    }

    void LateUpdate()
    {
        if (target == null) return;
        
        // Rotación con mouse (opcional)
        if (allowRotation && Input.GetMouseButton(1))
        {
            float horizontal = Input.GetAxis("Mouse X") * rotationSpeed;
            float vertical = -Input.GetAxis("Mouse Y") * rotationSpeed;
            
            transform.RotateAround(target.position, Vector3.up, horizontal);
            transform.RotateAround(target.position, transform.right, vertical);
        }
        
        // Zoom con scroll (opcional)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            distance = Mathf.Clamp(distance - scroll * 2f, minDistance, maxDistance);
        }
        
        // Actualizar posición
        Vector3 direction = (transform.position - target.position).normalized;
        transform.position = target.position + direction * distance;
    }
    
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }
    }
}
