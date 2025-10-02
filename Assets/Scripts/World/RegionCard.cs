using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Tarjeta flotante que representa una región clickeable
/// Billboard effect: siempre mira a la cámara
/// </summary>
public class RegionCard : MonoBehaviour
{
    [Header("Información de la Región")]
    public string regionName = "Region";
    public RegionType regionType = RegionType.Continent;

    [Header("Configuración Visual")]
    public float fontSize = 5f; // valor editable en el Inspector
    
    [Header("Jerarquía de Navegación")]
    public RegionCard[] childRegions; // Regiones hijas
    public GameObject visualLayerToShow; // Capa visual que se activa (ej: VisualLayer_Countries)
    public string sceneToLoad = ""; // Para plantas (carga nueva escena)
    
    [Header("Referencias Visuales")]
    public TextMeshPro labelText;
    public GameObject cardBackground; // Quad para el fondo de la tarjeta
    
    [Header("Configuración Visual")]
    public Color cardColor = new Color(1f, 1f, 1f, 0.9f);
    public Color textColor = Color.black;
    public Vector2 cardSize = new Vector2(2f, 0.5f);
    
    [Header("Comportamiento")]
    public bool rotatesWithPlanet = false; // Solo true para continentes
    
    private PlanetController planetController;
    private bool isVisible = false;
    private Vector3 lockedWorldPosition;
    private bool isPositionLocked = false;
    private GameObject planet;
    private Vector3 localPositionToPlanet;
    
    public enum RegionType
    {
        Continent,
        Country,
        State,
        Plant
    }
    
    void Start()
    {
        planetController = FindObjectOfType<PlanetController>();
        planet = GameObject.Find("Planet");
        
        SetupVisuals();
        
        // Solo los continentes guardan posición relativa al planeta
        if (regionType == RegionType.Continent && planet != null)
        {
            rotatesWithPlanet = true;
            localPositionToPlanet = planet.transform.InverseTransformPoint(transform.position);
            Debug.Log($"🌍 [{regionName}] Configurado para rotar con planeta");
        }
        
        // Inicialmente oculto
        SetVisibility(false);
    }
    
    void SetupVisuals()
    {
        // Configurar el texto
        if (labelText != null)
        {
            labelText.text = regionName;
            labelText.color = textColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = fontSize;
        }
        
        // Configurar el fondo de la tarjeta
        if (cardBackground != null)
        {
            MeshRenderer renderer = cardBackground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = cardColor;
                renderer.material = mat;
            }
            
            // Ajustar escala del fondo
            cardBackground.transform.localScale = new Vector3(cardSize.x, cardSize.y, 0.1f);
        }
    }
    
    void Update()
    {
        // Mantener posición fija si está bloqueada (no-continentes)
        if (isPositionLocked && isVisible)
        {
            transform.position = lockedWorldPosition;
        }
    }
    
    void LateUpdate()
    {
        // Continentes siguen al planeta
        if (rotatesWithPlanet && planet != null && isVisible)
        {
            transform.position = planet.transform.TransformPoint(localPositionToPlanet);
        }
        
        // Billboard effect: TODAS las tarjetas miran a la cámara
        if (isVisible && Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - transform.position;
            transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }
    
    void OnMouseDown()
    {
        if (!isVisible) return;
        HandleClick();
    }
    
    public void HandleClick()
    {
        Debug.Log($"🎯 CLICK en [{regionName}] - Tipo: {regionType}");
        
        // Si es planta con escena asignada
        if (regionType == RegionType.Plant && !string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"🚀 Cargando escena: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            return;
        }
        
        // Si tiene regiones hijas, hacer zoom
        if (childRegions != null && childRegions.Length > 0)
        {
            if (planetController != null)
            {
                planetController.FocusOnRegion(this);
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ [{regionName}] No tiene hijos ni escena asignada");
        }
    }
    
    /// <summary>
    /// Mostrar u ocultar la tarjeta
    /// </summary>
    public void SetVisibility(bool visible)
    {
        isVisible = visible;
        
        if (cardBackground != null)
            cardBackground.SetActive(visible);
            
        if (labelText != null)
            labelText.gameObject.SetActive(visible);
        
        // Activar/desactivar el collider
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = visible;
    }
    
    /// <summary>
    /// Bloquear posición en el espacio mundial (para no-continentes)
    /// </summary>
    public void LockPosition()
    {
        if (!rotatesWithPlanet)
        {
            lockedWorldPosition = transform.position;
            isPositionLocked = true;
            Debug.Log($"🔒 [{regionName}] Posición bloqueada en {lockedWorldPosition}");
        }
    }
    
    /// <summary>
    /// Desbloquear posición
    /// </summary>
    public void UnlockPosition()
    {
        isPositionLocked = false;
        Debug.Log($"🔓 [{regionName}] Posición desbloqueada");
    }
}