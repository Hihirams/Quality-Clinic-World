using UnityEngine;

/// <summary>
/// Configurar la cámara con el estilo visual deseado
/// Adjuntar a Main Camera
/// </summary>
public class CameraSetup : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.15f, 0.19f); // #1E2730
    
    void Start()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            
            // Configuración óptima para el estilo flat
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            
            Debug.Log("✅ Cámara configurada - Estilo Apple");
        }
    }
}