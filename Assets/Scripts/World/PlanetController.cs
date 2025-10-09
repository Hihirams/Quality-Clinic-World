using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlador principal del sistema de navegación
/// MEJORADO: Integrado con TextureLayerManager para navegación multinivel
/// </summary>
public class PlanetController : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject planet;
    public Camera mainCamera;
    public RegionCard[] continentCards;
    
    [Header("Sistema de Capas")]
    public TextureLayerManager layerManager;
    
    [Header("Configuración de Zoom")]
    public float zoomDuration = 1.5f;
    public float continentZoomDistance = 10f;
    public float countryZoomDistance = 7f;
    public float stateZoomDistance = 5f;
    public float plantZoomDistance = 3f;
    
    [Header("Rotación del Planeta")]
    public float planetRotationSpeed = 10f;
    public bool autoRotate = true;
    public float focusRotationDuration = 1.2f;
    
    [Header("Capas Visuales (Opcional - Sistema Legacy)")]
    public GameObject visualLayer_Continents;
    public GameObject visualLayer_Countries;
    public GameObject visualLayer_States;
    
    private Vector3 initialCameraPosition;
    private Quaternion initialPlanetRotation;
    private Stack<NavigationLevel> navigationStack = new Stack<NavigationLevel>();
    private RegionCard[] currentVisibleCards;
    private bool isTransitioning = false;
    
    private class NavigationLevel
    {
        public RegionCard focusedRegion;
        public RegionCard[] visibleCards;
        public Vector3 cameraPosition;
        public Quaternion planetRotation;
        public GameObject activeVisualLayer;
    }
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (planet == null)
            planet = GameObject.Find("Planet");
        
        if (layerManager == null)
            layerManager = FindFirstObjectByType<TextureLayerManager>();
        
        // Guardar estado inicial
        initialCameraPosition = mainCamera.transform.position;
        initialPlanetRotation = planet.transform.rotation;
        
        // Configurar vista inicial
        currentVisibleCards = continentCards;
        ShowCards(continentCards);
        
        // Solo continentes visibles al inicio
        if (visualLayer_Continents != null)
            visualLayer_Continents.SetActive(true);
        if (visualLayer_Countries != null)
            visualLayer_Countries.SetActive(false);
        if (visualLayer_States != null)
            visualLayer_States.SetActive(false);
        
        Debug.Log($"🌍 PlanetController inicializado - {continentCards.Length} continentes");
    }
    
    void Update()
    {
        // Auto-rotación cuando no hay transiciones
        if (autoRotate && !isTransitioning)
        {
            planet.transform.Rotate(Vector3.up, planetRotationSpeed * Time.deltaTime, Space.World);
        }
        
        // Navegación con ESC o click derecho
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            GoBack();
        }
    }
    
    /// <summary>
    /// Hacer focus en una región: rota el planeta para centrarla y hace zoom
    /// </summary>
    public void FocusOnRegion(RegionCard region)
    {
        if (isTransitioning) return;
        
        Debug.Log($"\n{new string('=', 50)}");
        Debug.Log($"🎯 FOCUS EN: {region.regionName} ({region.regionType})");
        
        // Detener auto-rotación
        autoRotate = false;
        
        // Bloquear posiciones de las tarjetas hijas ANTES de cualquier movimiento
        if (region.childRegions != null && region.childRegions.Length > 0)
        {
            foreach (var childCard in region.childRegions)
            {
                if (childCard != null && !childCard.rotatesWithPlanet)
                {
                    childCard.LockPosition();
                }
            }
        }
        
        // Guardar nivel actual en el stack
        NavigationLevel currentLevel = new NavigationLevel
        {
            focusedRegion = region,
            visibleCards = currentVisibleCards,
            cameraPosition = mainCamera.transform.position,
            planetRotation = planet.transform.rotation,
            activeVisualLayer = GetCurrentActiveVisualLayer()
        };
        navigationStack.Push(currentLevel);
        
        // Calcular rotación necesaria para centrar la región
        Vector3 regionWorldPos = region.transform.position;
        Vector3 directionToPlanetCenter = planet.transform.position - regionWorldPos;
        Vector3 targetDirection = mainCamera.transform.position - planet.transform.position;
        
        // Rotación objetivo para alinear la región con la cámara
        Quaternion targetRotation = Quaternion.FromToRotation(directionToPlanetCenter, targetDirection) * planet.transform.rotation;
        
        // Calcular distancia de zoom según el tipo de región
        float targetCameraDistance = GetZoomDistanceForNextLevel(region.regionType);
        Vector3 targetCameraPosition = planet.transform.position + 
            (mainCamera.transform.position - planet.transform.position).normalized * targetCameraDistance;
        
        // Cambiar capa visual
        ActivateVisualLayerForType(region.regionType);
        
        // Ocultar tarjetas actuales
        HideCards(currentVisibleCards);
        
        // Iniciar transición
        StartCoroutine(TransitionToRegion(targetRotation, targetCameraPosition, region.childRegions));
        
        Debug.Log($"{new string('=', 50)}\n");
    }
    
    /// <summary>
    /// Corrutina que maneja la transición suave
    /// </summary>
    private IEnumerator TransitionToRegion(Quaternion targetPlanetRotation, Vector3 targetCameraPos, RegionCard[] newCards)
    {
        isTransitioning = true;
        
        Quaternion startPlanetRotation = planet.transform.rotation;
        Vector3 startCameraPos = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < focusRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / focusRotationDuration);
            
            // Interpolar rotación del planeta
            planet.transform.rotation = Quaternion.Slerp(startPlanetRotation, targetPlanetRotation, t);
            
            // Interpolar posición de cámara (zoom)
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPos, t);
            
            yield return null;
        }
        
        // Asegurar valores finales
        planet.transform.rotation = targetPlanetRotation;
        mainCamera.transform.position = targetCameraPos;
        
        // Mostrar nuevas tarjetas
        if (newCards != null && newCards.Length > 0)
        {
            currentVisibleCards = newCards;
            ShowCards(newCards);
            Debug.Log($"✅ {newCards.Length} nuevas tarjetas mostradas");
        }
        else
        {
            currentVisibleCards = null;
            Debug.LogWarning("⚠️ No hay tarjetas hijas para mostrar");
        }
        
        isTransitioning = false;
    }
    
    /// <summary>
    /// Volver al nivel anterior
    /// </summary>
    public void GoBack()
    {
        if (isTransitioning) return;
        
        Debug.Log("\n🔙 VOLVER ATRÁS");
        
        // NUEVO: Notificar al LayerManager para volver a la capa anterior
        if (layerManager != null)
        {
            layerManager.GoToPreviousLayer();
        }
        
        // Desbloquear y ocultar tarjetas actuales
        if (currentVisibleCards != null)
        {
            foreach (var card in currentVisibleCards)
            {
                if (card != null)
                {
                    card.UnlockPosition();
                    card.SetVisibility(false);
                }
            }
        }
        
        if (navigationStack.Count > 0)
        {
            navigationStack.Pop(); // Quitar nivel actual
            
            if (navigationStack.Count > 0)
            {
                // Volver al nivel anterior
                NavigationLevel previousLevel = navigationStack.Pop();
                
                // Restaurar capa visual
                ActivateSpecificVisualLayer(previousLevel.activeVisualLayer);
                
                StartCoroutine(TransitionBack(
                    previousLevel.planetRotation,
                    previousLevel.cameraPosition,
                    previousLevel.visibleCards
                ));
            }
            else
            {
                // Volver a vista inicial
                StartCoroutine(TransitionToInitialView());
            }
        }
        else
        {
            // Ya estamos en la vista inicial
            Debug.Log("ℹ️ Ya estamos en la vista inicial");
        }
    }
    
    /// <summary>
    /// Transición para volver al nivel anterior
    /// </summary>
    private IEnumerator TransitionBack(Quaternion targetPlanetRotation, Vector3 targetCameraPos, RegionCard[] cardsToShow)
    {
        isTransitioning = true;
        
        Quaternion startPlanetRotation = planet.transform.rotation;
        Vector3 startCameraPos = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < focusRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / focusRotationDuration);
            
            planet.transform.rotation = Quaternion.Slerp(startPlanetRotation, targetPlanetRotation, t);
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPos, t);
            
            yield return null;
        }
        
        planet.transform.rotation = targetPlanetRotation;
        mainCamera.transform.position = targetCameraPos;
        
        currentVisibleCards = cardsToShow;
        ShowCards(cardsToShow);
        
        isTransitioning = false;
    }
    
    /// <summary>
    /// Volver a la vista inicial (continentes)
    /// </summary>
    private IEnumerator TransitionToInitialView()
    {
        isTransitioning = true;
        
        Debug.Log("🏠 Volviendo a vista inicial");
        
        Quaternion startPlanetRotation = planet.transform.rotation;
        Vector3 startCameraPos = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < focusRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / focusRotationDuration);
            
            planet.transform.rotation = Quaternion.Slerp(startPlanetRotation, initialPlanetRotation, t);
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, initialCameraPosition, t);
            
            yield return null;
        }
        
        planet.transform.rotation = initialPlanetRotation;
        mainCamera.transform.position = initialCameraPosition;
        
        // Limpiar stack
        navigationStack.Clear();
        
        // Mostrar solo continentes
        if (visualLayer_Continents != null)
            visualLayer_Continents.SetActive(true);
        if (visualLayer_Countries != null)
            visualLayer_Countries.SetActive(false);
        if (visualLayer_States != null)
            visualLayer_States.SetActive(false);
        
        currentVisibleCards = continentCards;
        ShowCards(continentCards);
        
        // Reactivar auto-rotación
        autoRotate = true;
        isTransitioning = false;
    }
    
    /// <summary>
    /// Activar la capa visual correcta según el tipo de región
    /// </summary>
    private void ActivateVisualLayerForType(RegionCard.RegionType regionType)
    {
        switch (regionType)
        {
            case RegionCard.RegionType.Continent:
                // Al hacer zoom a un continente, mostrar países
                if (visualLayer_Continents != null) visualLayer_Continents.SetActive(false);
                if (visualLayer_Countries != null) visualLayer_Countries.SetActive(true);
                if (visualLayer_States != null) visualLayer_States.SetActive(false);
                Debug.Log("🗺️ Capa visual: PAÍSES");
                break;
                
            case RegionCard.RegionType.Country:
                // Al hacer zoom a un país, mostrar estados
                if (visualLayer_Continents != null) visualLayer_Continents.SetActive(false);
                if (visualLayer_Countries != null) visualLayer_Countries.SetActive(false);
                if (visualLayer_States != null) visualLayer_States.SetActive(true);
                Debug.Log("🗺️ Capa visual: ESTADOS");
                break;
                
            case RegionCard.RegionType.State:
                // Los estados mantienen su propia capa
                Debug.Log("🗺️ Capa visual: ESTADOS (mantenida)");
                break;
        }
    }
    
    /// <summary>
    /// Activar una capa visual específica
    /// </summary>
    private void ActivateSpecificVisualLayer(GameObject layer)
    {
        if (visualLayer_Continents != null) 
            visualLayer_Continents.SetActive(layer == visualLayer_Continents);
        if (visualLayer_Countries != null) 
            visualLayer_Countries.SetActive(layer == visualLayer_Countries);
        if (visualLayer_States != null) 
            visualLayer_States.SetActive(layer == visualLayer_States);
    }
    
    /// <summary>
    /// Obtener la capa visual activa actualmente
    /// </summary>
    private GameObject GetCurrentActiveVisualLayer()
    {
        if (visualLayer_States != null && visualLayer_States.activeSelf)
            return visualLayer_States;
        if (visualLayer_Countries != null && visualLayer_Countries.activeSelf)
            return visualLayer_Countries;
        if (visualLayer_Continents != null && visualLayer_Continents.activeSelf)
            return visualLayer_Continents;
        
        return null;
    }
    
    /// <summary>
    /// Obtener distancia de zoom según el siguiente nivel
    /// </summary>
    private float GetZoomDistanceForNextLevel(RegionCard.RegionType currentType)
    {
        switch (currentType)
        {
            case RegionCard.RegionType.Continent:
                return countryZoomDistance;
            case RegionCard.RegionType.Country:
                return stateZoomDistance;
            case RegionCard.RegionType.State:
                return plantZoomDistance;
            default:
                return continentZoomDistance;
        }
    }
    
    /// <summary>
    /// Mostrar un conjunto de tarjetas
    /// </summary>
    private void ShowCards(RegionCard[] cards)
    {
        if (cards == null || cards.Length == 0)
        {
            Debug.LogWarning("⚠️ No hay tarjetas para mostrar");
            return;
        }
        
        foreach (var card in cards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
                card.SetVisibility(true);
            }
        }
        
        Debug.Log($"👁️ {cards.Length} tarjetas mostradas");
    }
    
    /// <summary>
    /// Ocultar un conjunto de tarjetas
    /// </summary>
    private void HideCards(RegionCard[] cards)
    {
        if (cards == null) return;
        
        foreach (var card in cards)
        {
            if (card != null)
            {
                card.SetVisibility(false);
            }
        }
        
        Debug.Log($"🙈 Tarjetas ocultadas");
    }
}