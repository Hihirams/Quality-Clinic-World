using UnityEngine;

/// <summary>
/// Sprites de continentes/países/estados pegados al planeta
/// MEJORADO: Usa SortingOrder y offset mínimo para evitar sobreposición
/// </summary>
public class ContinentSprite : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private Sprite spriteImage;
    [SerializeField] private Color spriteColor = new Color(0.89f, 0.22f, 0.22f);
    [SerializeField] private float offsetFromSurface = 0.0001f; // CASI pegado a la superficie
    
    [Header("Orientation")]
    [SerializeField] private bool flipX = false;
    [SerializeField] private bool flipY = false;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f);

    [Header("Positioning")]
    [SerializeField] private Vector3 positionOnPlanet = Vector3.forward;
    [SerializeField] private float scale = 1f;
    
    [Header("Render Settings")]
    [SerializeField] private int sortingOrder = 1; // Controla qué sprite está encima
    
    private GameObject planet;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        planet = GameObject.Find("Planet");
        if (planet == null)
        {
            Debug.LogError("No se encontró el planeta!");
            return;
        }

        SetupSprite();
        PositionOnPlanet();
    }
    
    private void SetupSprite()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        spriteRenderer.sprite = spriteImage;
        spriteRenderer.color = spriteColor;
        spriteRenderer.flipX = flipX;
        spriteRenderer.flipY = flipY;
        
        // CLAVE: Usar material que respete el orden de renderizado
        spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
        spriteRenderer.sortingOrder = sortingOrder;
        
        Debug.Log($"Sprite configurado: {gameObject.name}");
    }
    
    private void PositionOnPlanet()
    {
        if (planet == null) return;
        
        float planetRadius = planet.transform.localScale.x / 2f;
        Vector3 dir = positionOnPlanet.sqrMagnitude < 1e-6f ? Vector3.forward : positionOnPlanet.normalized;
        
        // Posición CASI en la superficie (offset mínimo para evitar z-fighting)
        Vector3 surfacePosition = dir * (planetRadius + offsetFromSurface);
        transform.localPosition = surfacePosition;
        
        // Rotar para que mire hacia afuera
        transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(rotationOffsetEuler);
        
        // Escala
        transform.localScale = Vector3.one * scale;
        
        Debug.Log($"{gameObject.name} posicionado en {surfacePosition}");
    }
    
    public void UpdatePosition()
    {
        PositionOnPlanet();
    }
    
    public void SetColor(Color newColor)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = newColor;
        }
    }
    
    public void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }
    
    /// <summary>
    /// Ajustar sorting order en runtime
    /// </summary>
    public void SetSortingOrder(int order)
    {
        sortingOrder = order;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = order;
        }
    }
}