using UnityEngine;

public class PlanetSetup : MonoBehaviour
{
    [Header("Configuración Visual")]
    [SerializeField] private Texture2D worldTexture; // Arrastra tu imagen aquí
    [SerializeField] private Color planetColor = new Color(0.16f, 0.20f, 0.25f);
    
    void Start()
    {
        SetupPlanetMaterial();
    }
    
    private void SetupPlanetMaterial()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;
        
        Material planetMat;
        
        if (worldTexture != null)
        {
            // Usar textura
            planetMat = new Material(Shader.Find("Unlit/Texture"));
            planetMat.mainTexture = worldTexture;
            Debug.Log("Textura del planeta aplicada");
        }
        else
        {
            // Color sólido si no hay textura
            planetMat = new Material(Shader.Find("Unlit/Color"));
            planetMat.color = planetColor;
            Debug.Log("Color sólido aplicado");
        }
        
        renderer.material = planetMat;
    }
}