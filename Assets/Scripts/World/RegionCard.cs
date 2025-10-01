using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RegionCard : MonoBehaviour
{
    [Header("Card Info")]
    public string regionName;
    public RegionType regionType;
    public RegionCard[] childRegions; // Regiones hijas (países, estados, plantas)
    
    [Header("Referencias")]
    public TextMeshPro textMesh;
    public GameObject cardVisual;
    
    [Header("Configuración")]
    public float rotationSpeed = 30f;
    public string sceneToLoad = ""; // Para la planta específica
    
    private PlanetController planetController;
    private bool isVisible = true;
    private Vector3 fixedWorldPosition; // Posición fija para tarjetas no-continente
    private bool isPositionLocked = false;
    private Transform originalParent; // Parent original para restaurar
    private GameObject planet; // Referencia al planeta para continentes
    private Vector3 localPositionToPlanet; // Posición local al planeta (solo para continentes)

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
        
        if (textMesh != null)
        {
            textMesh.text = regionName;
        }

        // Para CONTINENTES: guardar posición local al planeta
        if (regionType == RegionType.Continent && planet != null)
        {
            localPositionToPlanet = planet.transform.InverseTransformPoint(transform.position);
            Debug.Log($"[{regionName}] Posición local al planeta guardada: {localPositionToPlanet}");
        }

        // IMPORTANTE: Desactivar CardRotator si existe (RegionCard maneja todo)
        CardRotator rotator = GetComponent<CardRotator>();
        if (rotator != null)
        {
            rotator.enabled = false;
            Debug.Log($"[{regionName}] CardRotator desactivado - RegionCard toma control");
        }
    }

    void Update()
    {
        // CRÍTICO: Mantener posición fija para tarjetas no-continente
        if (isPositionLocked && isVisible)
        {
            // DEBUG: Detectar si la posición está cambiando
            if (Vector3.Distance(transform.position, fixedWorldPosition) > 0.01f)
            {
                Debug.LogWarning($"[{regionName}] ⚠️ POSICIÓN CAMBIANDO! " +
                    $"Actual: {transform.position}, Esperada: {fixedWorldPosition}, " +
                    $"Distancia: {Vector3.Distance(transform.position, fixedWorldPosition):F4}, " +
                    $"Parent: {(transform.parent != null ? transform.parent.name : "NULL")}");
            }
            transform.position = fixedWorldPosition;
        }
    }

    void LateUpdate()
    {
        // Para CONTINENTES: mantener posición orbital con el planeta
        if (regionType == RegionType.Continent && planet != null && isVisible)
        {
            // Convertir la posición local guardada a posición mundial actual
            transform.position = planet.transform.TransformPoint(localPositionToPlanet);
        }

        // Billboard effect: SIEMPRE hacer que TODAS las tarjetas miren a la cámara
        if (isVisible && Camera.main != null)
        {
            // Rotar la tarjeta para que siempre mire a la cámara (billboard effect)
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0);
        }
    }

    void OnMouseDown()
    {
        HandleClick();
    }

    public void HandleClick()
    {
        Debug.Log($"[{regionName}] CLICK DETECTADO - Type: {regionType}, SceneToLoad: '{sceneToLoad}', ChildRegions: {(childRegions != null ? childRegions.Length : 0)}");
        
        if (planetController == null)
        {
            planetController = FindObjectOfType<PlanetController>();
        }

        // Si es una planta y tiene escena asignada, cargar la escena
        if (regionType == RegionType.Plant && !string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"[{regionName}] CARGANDO ESCENA: {sceneToLoad}");
            // Cargar en modo Single para descargar la escena actual
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            return;
        }

        // Si tiene regiones hijas, hacer zoom y mostrarlas
        if (childRegions != null && childRegions.Length > 0)
        {
            Debug.Log($"[{regionName}] HACIENDO ZOOM A HIJOS");
            planetController.FocusOnRegion(this);
        }
        else
        {
            Debug.LogWarning($"[{regionName}] NO HAY HIJOS NI ESCENA PARA CARGAR");
        }
    }

    public void SetVisibility(bool visible)
    {
        isVisible = visible;
        if (cardVisual != null)
        {
            cardVisual.SetActive(visible);
        }
        if (textMesh != null)
        {
            textMesh.gameObject.SetActive(visible);
        }

        // CRÍTICO: Desactivar/activar el collider para evitar clics fantasma
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = visible;
            Debug.Log($"[{regionName}] Collider {(visible ? "ACTIVADO" : "DESACTIVADO")}");
        }
    }

    // Método para bloquear la posición (llamado por PlanetController)
    public void LockPosition()
    {
        if (regionType != RegionType.Continent)
        {
            Debug.Log($"[{regionName}] 🔒 INICIANDO BLOQUEO...");
            Debug.Log($"[{regionName}] Parent ANTES: {(transform.parent != null ? transform.parent.name : "NULL")}");
            Debug.Log($"[{regionName}] Posición ANTES: {transform.position}");
            
            // SOLUCIÓN: Si no tiene parent, significa que fue posicionado con el planeta en rotación 0
            // Necesitamos ajustar su posición a la rotación actual del planeta
            if (transform.parent == null)
            {
                GameObject planet = GameObject.Find("Planet");
                if (planet != null)
                {
                    // Calcular la posición como si hubiera rotado con el planeta
                    Vector3 localPos = Quaternion.Inverse(planet.transform.rotation) * (transform.position - planet.transform.position);
                    fixedWorldPosition = planet.transform.position + (planet.transform.rotation * localPos);
                    
                    Debug.Log($"[{regionName}] 🔄 AJUSTADO por rotación del planeta");
                    Debug.Log($"[{regionName}] Rotación planeta: {planet.transform.rotation.eulerAngles}");
                }
                else
                {
                    fixedWorldPosition = transform.position;
                }
            }
            else
            {
                // Si tiene parent, sacar de la jerarquía
                originalParent = transform.parent;
                transform.SetParent(null);
                fixedWorldPosition = transform.position;
            }
            
            isPositionLocked = true;
            
            Debug.Log($"[{regionName}] Parent DESPUÉS: {(transform.parent != null ? transform.parent.name : "NULL")}");
            Debug.Log($"[{regionName}] Posición DESPUÉS: {transform.position}");
            Debug.Log($"[{regionName}] ✅ BLOQUEADO en {fixedWorldPosition}");
        }
        else
        {
            Debug.Log($"[{regionName}] ⏭️ SALTADO (es continente)");
        }
    }

    // Método para desbloquear la posición
    public void UnlockPosition()
    {
        if (isPositionLocked && originalParent != null)
        {
            // Restaurar el padre original
            transform.SetParent(originalParent);
            Debug.Log($"[RegionCard] {regionName} - REPARENTADO");
        }
        isPositionLocked = false;
    }
}