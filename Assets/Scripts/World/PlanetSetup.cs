using UnityEngine;

/// <summary>
/// Script para configurar el planeta base. Adjuntar al GameObject "Planet"
/// </summary>
public class PlanetSetup : MonoBehaviour
{
    [Header("Configuración Visual")]
    [SerializeField] private Color planetColor = new Color(0.16f, 0.20f, 0.25f); // #2A3440
    
    void Start()
    {
        SetupPlanetMaterial();
    }
    
    private void SetupPlanetMaterial()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;
        
        // Crear material unlit con color sólido (estilo flat)
        Material planetMat = new Material(Shader.Find("Unlit/Color"));
        planetMat.color = planetColor;
        renderer.material = planetMat;
        
        Debug.Log("✅ Material del planeta configurado");
    }
}