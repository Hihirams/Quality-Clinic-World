using UnityEngine;

/// <summary>
/// Mejoras visuales opcionales para las tarjetas
/// Efectos hover, pulse, y transiciones suaves estilo Apple
/// </summary>
[RequireComponent(typeof(RegionCard))]
public class VisualEnhancements : MonoBehaviour
{
    [Header("Hover Effect")]
    [SerializeField] private bool enableHoverEffect = true;
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverTransitionSpeed = 8f;
    [SerializeField] private Color hoverColor = Color.white;
    
    [Header("Click Effect")]
    [SerializeField] private bool enableClickEffect = true;
    [SerializeField] private float clickScalePulse = 1.15f;
    [SerializeField] private float clickPulseDuration = 0.2f;
    
    [Header("Glow Effect")]
    [SerializeField] private bool enableGlow = false;
    [SerializeField] private float glowIntensity = 1.5f;
    [SerializeField] private float glowSpeed = 2f;
    
    private RegionCard regionCard;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private Color originalColor;
    private bool isHovering = false;
    private bool isClicking = false;
    private float clickTimer = 0f;
    private Material cardMaterial;
    
    void Start()
    {
        regionCard = GetComponent<RegionCard>();
        originalScale = transform.localScale;
        targetScale = originalScale;
        
        // Obtener el material de la tarjeta
        if (regionCard.cardBackground != null)
        {
            MeshRenderer renderer = regionCard.cardBackground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                cardMaterial = renderer.material;
                originalColor = cardMaterial.color;
            }
        }
    }
    
    void Update()
    {
        // Smooth scale transition
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, 
                targetScale, 
                Time.deltaTime * hoverTransitionSpeed
            );
        }
        
        // Click pulse effect
        if (isClicking)
        {
            clickTimer += Time.deltaTime;
            if (clickTimer >= clickPulseDuration)
            {
                isClicking = false;
                targetScale = isHovering ? originalScale * hoverScale : originalScale;
            }
        }
        
        // Glow effect (pulsación sutil)
        if (enableGlow && cardMaterial != null)
        {
            float glow = 1f + Mathf.Sin(Time.time * glowSpeed) * 0.1f;
            Color glowColor = originalColor * glow;
            glowColor.a = originalColor.a;
            cardMaterial.color = glowColor;
        }
    }
    
    void OnMouseEnter()
    {
        if (!enableHoverEffect || !regionCard) return;
        
        isHovering = true;
        targetScale = originalScale * hoverScale;
        
        if (cardMaterial != null)
        {
            cardMaterial.color = hoverColor;
        }
    }
    
    void OnMouseExit()
    {
        if (!enableHoverEffect || !regionCard) return;
        
        isHovering = false;
        targetScale = originalScale;
        
        if (cardMaterial != null)
        {
            cardMaterial.color = originalColor;
        }
    }
    
    void OnMouseDown()
    {
        if (!enableClickEffect || !regionCard) return;
        
        // Efecto de "pulse" al hacer click
        isClicking = true;
        clickTimer = 0f;
        targetScale = originalScale * clickScalePulse;
    }
    
    /// <summary>
    /// Resetear todos los efectos visuales
    /// </summary>
    public void ResetEffects()
    {
        isHovering = false;
        isClicking = false;
        targetScale = originalScale;
        transform.localScale = originalScale;
        
        if (cardMaterial != null)
        {
            cardMaterial.color = originalColor;
        }
    }
}