using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sistema multi-nivel para navegación planeta → continente → país → estado → planta
/// Cambia texturas dinámicamente y detecta clicks con máscaras de color
/// </summary>
public class MultiLevelPlanetSystem : MonoBehaviour
{
    [Header("Referencias del Planeta")]
    [SerializeField] private GameObject planet;
    [SerializeField] private Camera mainCamera;
    
    [Header("Texturas - Nivel 1: Continentes")]
    [SerializeField] private Texture2D continentsBackground;
    [SerializeField] private Texture2D continentsMask;
    
    [Header("Texturas - Nivel 2: Países (se asignan dinámicamente)")]
    [SerializeField] private List<ContinentTextures> continentTexturesList = new List<ContinentTextures>();
    
    [Header("Texturas - Nivel 3: Estados (se asignan dinámicamente)")]
    [SerializeField] private List<CountryTextures> countryTexturesList = new List<CountryTextures>();
    
    [Header("Texturas - Nivel 4: Plantas (se asignan dinámicamente)")]
    [SerializeField] private List<StateTextures> stateTexturesList = new List<StateTextures>();
    
    [Header("Configuración de Zoom")]
    [SerializeField] private float countryZoomDistance = 7f;
    [SerializeField] private float stateZoomDistance = 5f;
    [SerializeField] private float plantZoomDistance = 3f;
    
    [Header("Configuración de Rotación")]
    [SerializeField] private float rotationDuration = 0.8f;
    [SerializeField] private float planetRotationSpeed = 10f;
    [SerializeField] private bool autoRotate = true;
    
    [Header("Detección de Clicks")]
    [SerializeField] private float colorTolerance = 0.15f;
    [SerializeField] private bool showDebugLogs = true;
    
    // Estado del sistema
    private NavigationLevel currentLevel = NavigationLevel.Continents;
    private Stack<LevelState> navigationStack = new Stack<LevelState>();
    private bool isTransitioning = false;
    private Texture2D currentMask;
    private Material planetMaterial;
    
    // Estado actual de navegación
    private string currentContinent = "";
    private string currentCountry = "";
    private string currentState = "";
    
    private Vector3 initialCameraPosition;
    private Quaternion initialPlanetRotation;
    
    public enum NavigationLevel
    {
        Continents,
        Countries,
        States,
        Plants
    }
    
    [System.Serializable]
    public class ContinentTextures
    {
        public string continentName;
        public Color maskColor; // Color en la máscara de continentes
        public Texture2D countriesBackground;
        public Texture2D countriesMask;
    }
    
    [System.Serializable]
    public class CountryTextures
    {
        public string continentName; // A qué continente pertenece
        public string countryName;
        public Color maskColor; // Color en la máscara de países
        public Texture2D statesBackground;
        public Texture2D statesMask;
    }
    
    [System.Serializable]
    public class StateTextures
    {
        public string countryName; // A qué país pertenece
        public string stateName;
        public Color maskColor; // Color en la máscara de estados
        public Texture2D plantsBackground;
        public Texture2D plantsMask;
    }
    
    [System.Serializable]
    public class PlantMapping
    {
        public string stateName; // A qué estado pertenece
        public string plantName;
        public Color maskColor; // Color en la máscara de plantas
        public string sceneToLoad; // Escena a cargar (ej: "10_Plant_OP2")
    }
    
    [Header("Mapeo de Plantas a Escenas")]
    [SerializeField] private List<PlantMapping> plantMappings = new List<PlantMapping>();
    
    private class LevelState
    {
        public NavigationLevel level;
        public Vector3 cameraPosition;
        public Quaternion planetRotation;
        public Texture2D background;
        public Texture2D mask;
        public string contextData; // Guarda continente/país/estado actual
    }
    
    void Start()
    {
        InitializeReferences();
        SetupInitialState();
    }
    
    void InitializeReferences()
    {
        if (planet == null)
            planet = GameObject.Find("Planet");
        
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (planet == null || mainCamera == null)
        {
            Debug.LogError("❌ Faltan referencias críticas!");
            return;
        }
        
        // Obtener material del planeta
        MeshRenderer renderer = planet.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            planetMaterial = renderer.material;
        }
        else
        {
            Debug.LogError("❌ El planeta no tiene MeshRenderer!");
        }
        
        initialCameraPosition = mainCamera.transform.position;
        initialPlanetRotation = planet.transform.rotation;
    }
    
    void SetupInitialState()
    {
        currentLevel = NavigationLevel.Continents;
        currentMask = continentsMask;
        
        if (planetMaterial != null && continentsBackground != null)
        {
            planetMaterial.mainTexture = continentsBackground;
        }
        
        if (showDebugLogs)
            Debug.Log("🌍 Sistema inicializado - Nivel: Continentes");
    }
    
    void Update()
    {
        // Auto-rotación
        if (autoRotate && !isTransitioning)
        {
            planet.transform.Rotate(Vector3.up, planetRotationSpeed * Time.deltaTime, Space.World);
        }
        
        // Detección de clicks
        if (Input.GetMouseButtonDown(0) && !isTransitioning)
        {
            DetectClickOnPlanet();
        }
        
        // Navegación hacia atrás
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            GoBack();
        }
    }
    
    void DetectClickOnPlanet()
    {
        if (currentMask == null || !currentMask.isReadable)
        {
            Debug.LogError("❌ Máscara no válida o no tiene Read/Write enabled");
            return;
        }
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == planet)
            {
                Vector2 uv = GetUVFromHit(hit);
                Color clickedColor = GetColorFromMask(uv);
                
                if (showDebugLogs)
                {
                    Debug.Log($"🎯 Click detectado - Nivel: {currentLevel}, Color: RGB({clickedColor.r:F2}, {clickedColor.g:F2}, {clickedColor.b:F2})");
                }
                
                HandleClickByLevel(clickedColor, hit.point);
            }
        }
    }
    
    Vector2 GetUVFromHit(RaycastHit hit)
    {
        Vector2 uv = hit.textureCoord;
        
        // Si no hay UV válido, calcular manualmente
        if (uv.sqrMagnitude < 0.0001f)
        {
            Vector3 localHitPoint = planet.transform.InverseTransformPoint(hit.point);
            Vector3 dir = localHitPoint.normalized;
            
            float u = 0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI);
            float v = 0.5f + Mathf.Asin(dir.y) / Mathf.PI;
            
            uv = new Vector2(u, v);
        }
        
        return uv;
    }
    
    Color GetColorFromMask(Vector2 uv)
    {
        int x = Mathf.FloorToInt(uv.x * currentMask.width);
        int y = Mathf.FloorToInt(uv.y * currentMask.height);
        
        x = Mathf.Clamp(x, 0, currentMask.width - 1);
        y = Mathf.Clamp(y, 0, currentMask.height - 1);
        
        return currentMask.GetPixel(x, y);
    }
    
    void HandleClickByLevel(Color clickedColor, Vector3 hitPoint)
    {
        switch (currentLevel)
        {
            case NavigationLevel.Continents:
                HandleContinentClick(clickedColor, hitPoint);
                break;
            
            case NavigationLevel.Countries:
                HandleCountryClick(clickedColor, hitPoint);
                break;
            
            case NavigationLevel.States:
                HandleStateClick(clickedColor, hitPoint);
                break;
            
            case NavigationLevel.Plants:
                HandlePlantClick(clickedColor, hitPoint);
                break;
        }
    }
    
    void HandleContinentClick(Color clickedColor, Vector3 hitPoint)
    {
        foreach (var continent in continentTexturesList)
        {
            if (ColorsMatch(clickedColor, continent.maskColor))
            {
                Debug.Log($"✅ Continente detectado: {continent.continentName}");
                currentContinent = continent.continentName;
                
                if (continent.countriesBackground != null && continent.countriesMask != null)
                {
                    TransitionToNextLevel(
                        NavigationLevel.Countries,
                        continent.countriesBackground,
                        continent.countriesMask,
                        hitPoint,
                        countryZoomDistance
                    );
                }
                else
                {
                    Debug.LogWarning($"⚠️ {continent.continentName} no tiene texturas de países asignadas");
                }
                return;
            }
        }
        
        if (showDebugLogs)
            Debug.Log("⚪ Click en área sin mapear (océano)");
    }
    
    void HandleCountryClick(Color clickedColor, Vector3 hitPoint)
    {
        foreach (var country in countryTexturesList)
        {
            if (country.continentName == currentContinent && ColorsMatch(clickedColor, country.maskColor))
            {
                Debug.Log($"✅ País detectado: {country.countryName}");
                currentCountry = country.countryName;
                
                if (country.statesBackground != null && country.statesMask != null)
                {
                    TransitionToNextLevel(
                        NavigationLevel.States,
                        country.statesBackground,
                        country.statesMask,
                        hitPoint,
                        stateZoomDistance
                    );
                }
                else
                {
                    Debug.LogWarning($"⚠️ {country.countryName} no tiene texturas de estados asignadas");
                }
                return;
            }
        }
        
        if (showDebugLogs)
            Debug.Log("⚪ Click en área sin mapear");
    }
    
    void HandleStateClick(Color clickedColor, Vector3 hitPoint)
    {
        foreach (var state in stateTexturesList)
        {
            if (state.countryName == currentCountry && ColorsMatch(clickedColor, state.maskColor))
            {
                Debug.Log($"✅ Estado detectado: {state.stateName}");
                currentState = state.stateName;
                
                if (state.plantsBackground != null && state.plantsMask != null)
                {
                    TransitionToNextLevel(
                        NavigationLevel.Plants,
                        state.plantsBackground,
                        state.plantsMask,
                        hitPoint,
                        plantZoomDistance
                    );
                }
                else
                {
                    Debug.LogWarning($"⚠️ {state.stateName} no tiene texturas de plantas asignadas");
                }
                return;
            }
        }
        
        if (showDebugLogs)
            Debug.Log("⚪ Click en área sin mapear");
    }
    
    void HandlePlantClick(Color clickedColor, Vector3 hitPoint)
    {
        foreach (var plant in plantMappings)
        {
            if (plant.stateName == currentState && ColorsMatch(clickedColor, plant.maskColor))
            {
                Debug.Log($"✅ Planta detectada: {plant.plantName} → Cargando escena: {plant.sceneToLoad}");
                
                if (!string.IsNullOrEmpty(plant.sceneToLoad))
                {
                    SceneManager.LoadScene(plant.sceneToLoad, LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogWarning($"⚠️ {plant.plantName} no tiene escena asignada");
                }
                return;
            }
        }
        
        if (showDebugLogs)
            Debug.Log("⚪ Click en área sin mapear");
    }
    
    bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < colorTolerance &&
               Mathf.Abs(a.g - b.g) < colorTolerance &&
               Mathf.Abs(a.b - b.b) < colorTolerance;
    }
    
    void TransitionToNextLevel(NavigationLevel nextLevel, Texture2D newBackground, Texture2D newMask, Vector3 focusPoint, float zoomDistance)
    {
        // Guardar estado actual
        LevelState savedState = new LevelState
        {
            level = currentLevel,
            cameraPosition = mainCamera.transform.position,
            planetRotation = planet.transform.rotation,
            background = planetMaterial.mainTexture as Texture2D,
            mask = currentMask,
            contextData = $"{currentContinent}|{currentCountry}|{currentState}"
        };
        navigationStack.Push(savedState);
        
        // Iniciar transición
        StartCoroutine(TransitionCoroutine(nextLevel, newBackground, newMask, focusPoint, zoomDistance));
    }
    
    IEnumerator TransitionCoroutine(NavigationLevel nextLevel, Texture2D newBackground, Texture2D newMask, Vector3 focusPoint, float zoomDistance)
    {
        isTransitioning = true;
        autoRotate = false;
        
        // Calcular rotación para centrar el punto clickeado
        Vector3 directionToPlanetCenter = planet.transform.position - focusPoint;
        Vector3 targetDirection = mainCamera.transform.position - planet.transform.position;
        Quaternion targetRotation = Quaternion.FromToRotation(directionToPlanetCenter, targetDirection) * planet.transform.rotation;
        
        // Calcular posición de cámara (zoom)
        Vector3 targetCameraPosition = planet.transform.position + 
            (mainCamera.transform.position - planet.transform.position).normalized * zoomDistance;
        
        // Fase 1: Rotación del planeta + Zoom
        Quaternion startRotation = planet.transform.rotation;
        Vector3 startCameraPos = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / rotationDuration);
            
            planet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPosition, t);
            
            yield return null;
        }
        
        planet.transform.rotation = targetRotation;
        mainCamera.transform.position = targetCameraPosition;
        
        // Fase 2: Cambiar textura con fade
        yield return StartCoroutine(FadeAndChangeTexture(newBackground));
        
        // Actualizar estado
        currentLevel = nextLevel;
        currentMask = newMask;
        
        if (showDebugLogs)
            Debug.Log($"🎯 Transición completada → Nivel: {nextLevel}");
        
        isTransitioning = false;
    }
    
    IEnumerator FadeAndChangeTexture(Texture2D newTexture)
    {
        // Fade out (opcional: puedes agregar un efecto de desvanecimiento)
        yield return new WaitForSeconds(0.1f);
        
        // Cambiar textura
        if (planetMaterial != null)
        {
            planetMaterial.mainTexture = newTexture;
        }
        
        // Fade in
        yield return new WaitForSeconds(0.1f);
    }
    
    public void GoBack()
    {
        if (isTransitioning || navigationStack.Count == 0)
        {
            Debug.Log("ℹ️ Ya estamos en el nivel inicial");
            return;
        }
        
        Debug.Log("🔙 Volviendo al nivel anterior");
        
        LevelState previousState = navigationStack.Pop();
        
        StartCoroutine(TransitionBackCoroutine(previousState));
    }
    
    IEnumerator TransitionBackCoroutine(LevelState previousState)
    {
        isTransitioning = true;
        
        // Restaurar contexto
        string[] context = previousState.contextData.Split('|');
        currentContinent = context[0];
        currentCountry = context[1];
        currentState = context[2];
        
        // Transición inversa
        Quaternion startRotation = planet.transform.rotation;
        Vector3 startCameraPos = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / rotationDuration);
            
            planet.transform.rotation = Quaternion.Slerp(startRotation, previousState.planetRotation, t);
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, previousState.cameraPosition, t);
            
            yield return null;
        }
        
        planet.transform.rotation = previousState.planetRotation;
        mainCamera.transform.position = previousState.cameraPosition;
        
        // Cambiar textura
        if (planetMaterial != null)
        {
            planetMaterial.mainTexture = previousState.background;
        }
        
        currentLevel = previousState.level;
        currentMask = previousState.mask;
        
        if (navigationStack.Count == 0)
        {
            autoRotate = true;
        }
        
        if (showDebugLogs)
            Debug.Log($"🔙 Vuelta completada → Nivel: {currentLevel}");
        
        isTransitioning = false;
    }
    
    // Métodos de utilidad para el Inspector
    [ContextMenu("Reset a Vista Inicial")]
    public void ResetToInitialView()
    {
        navigationStack.Clear();
        currentLevel = NavigationLevel.Continents;
        currentContinent = "";
        currentCountry = "";
        currentState = "";
        
        if (planetMaterial != null)
        {
            planetMaterial.mainTexture = continentsBackground;
        }
        
        currentMask = continentsMask;
        mainCamera.transform.position = initialCameraPosition;
        planet.transform.rotation = initialPlanetRotation;
        autoRotate = true;
        
        Debug.Log("🏠 Vista inicial restaurada");
    }
}
