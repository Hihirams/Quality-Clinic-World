using UnityEngine;

/// <summary>
/// Componente para sprites de continentes/países/estados sobre el planeta
/// Los sprites rotan CON el planeta porque son hijos del GameObject Planet
/// </summary>
public class ContinentSprite : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private Sprite spriteImage; // Asignar PNG del continente
    [SerializeField] private Color spriteColor = new Color(0.89f, 0.22f, 0.22f); // #E23939
    [SerializeField] private float offsetFromSurface = 0.01f; // Ligeramente sobre la superficie
    
    [Header("Orientation")]
    [SerializeField] private bool flipX = false;
    [SerializeField] private bool flipY = false;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f); // 180 en Y suele corregir el espejo


    [Header("Positioning")]
    [SerializeField] private Vector3 positionOnPlanet = Vector3.forward; // Dirección desde el centro
    [SerializeField] private float scale = 1f;
    
    private GameObject planet;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        planet = GameObject.Find("Planet");
        if (planet == null)
        {
            Debug.LogError("❌ No se encontró el planeta!");
            return;
        }

        SetupSprite();
        PositionOnPlanet();
    }
    
    private void SetupSprite()
    {
        // Crear el sprite renderer si no existe
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        spriteRenderer.sprite = spriteImage;
        spriteRenderer.color = spriteColor;

        spriteRenderer.flipX = flipX;
        spriteRenderer.flipY = flipY;
        
        // Configuración para que se vea sobre el planeta
        spriteRenderer.sortingOrder = 10;
        
        Debug.Log($"✅ Sprite configurado: {gameObject.name}");
    }
    
private void PositionOnPlanet()
{
    if (planet == null) return;
    
    // Calcular radio del planeta (asumiendo esfera con escala uniforme)
    float planetRadius = planet.transform.localScale.x / 2f;
    
    // Dirección normalizada (hacia afuera del planeta)
    Vector3 dir = positionOnPlanet.sqrMagnitude < 1e-6f ? Vector3.forward : positionOnPlanet.normalized;
    
    // Posicionar el sprite en la superficie del planeta
    Vector3 surfacePosition = dir * (planetRadius + offsetFromSurface);
    transform.localPosition = surfacePosition;
    
    // Rotar para que "mire" hacia afuera del planeta con offset opcional
    transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(rotationOffsetEuler);
    
    // Aplicar escala
    transform.localScale = Vector3.one * scale;
    
    Debug.Log($"📍 {gameObject.name} posicionado en {surfacePosition}");
}

    
    /// <summary>
    /// Método público para reposicionar el sprite manualmente desde el editor
    /// </summary>
    public void UpdatePosition()
    {
        PositionOnPlanet();
    }
    
    /// <summary>
    /// Cambiar el color del sprite (útil para animaciones de selección)
    /// </summary>
    public void SetColor(Color newColor)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = newColor;
        }
    }
    
    /// <summary>
    /// Toggle visibilidad del sprite
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }
}