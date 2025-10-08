using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Word_V2;

public class PlanetController : MonoBehaviour
{
    [Header("References")]
    public Material planetMaterial;
    public Camera mainCamera;
    
    [Header("Rotation Settings")]
    public float idleRotationSpeed = 10f;
    public float transitionRotationSpeed = 180f;
    public float transitionDuration = 1.5f;
    
    [Header("Zoom Settings")]
    public float normalDistance = 5f;
    public float zoomedDistance = 4.2f;
    public float zoomSpeed = 2f;
    
    [Header("Data")]
    public InteractiveData interactiveData;
    
    private int currentLevel = 0; // 0=World, 1=Continent, 2=Country, 3=State
    private Texture2D currentMaskTexture;
    private bool isTransitioning = false;
    private int selectedRegionIndex = -1;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Cargar el primer nivel (mundo)
        LoadLevel(0);
    }

    void Update()
    {
        // Rotación idle del planeta
        if (!isTransitioning)
        {
            transform.Rotate(Vector3.up, idleRotationSpeed * Time.deltaTime, Space.World);
        }
        
        // Detectar click
        if (Input.GetMouseButtonDown(0) && !isTransitioning)
        {
            HandleClick();
        }
    }

    void LoadLevel(int levelIndex)
    {
        if (levelIndex >= interactiveData.levels.Length)
        {
            Debug.LogError("Nivel fuera de rango!");
            return;
        }
        
        currentLevel = levelIndex;
        LevelData level = interactiveData.levels[levelIndex];
        
        // Cargar texturas
        planetMaterial.mainTexture = level.backgroundTexture;
        currentMaskTexture = level.maskTexture;
        
        Debug.Log($"Nivel cargado: {level.levelName}");
    }

    void HandleClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == gameObject)
            {
                // Obtener coordenadas UV
                Vector2 uv = hit.textureCoord;
                
                // Convertir UV a coordenadas de pixel
                int x = Mathf.FloorToInt(uv.x * currentMaskTexture.width);
                int y = Mathf.FloorToInt(uv.y * currentMaskTexture.height);
                
                // Obtener color del mask
                Color clickedColor = currentMaskTexture.GetPixel(x, y);
                
                // Buscar región
                int regionIndex = FindRegionByColor(clickedColor);
                
                if (regionIndex >= 0)
                {
                    selectedRegionIndex = regionIndex;
                    LevelData level = interactiveData.levels[currentLevel];
                    RegionData region = level.regions[regionIndex];
                    
                    Debug.Log($"Continente/Región: {region.regionName} clickeado");
                    
                    // Iniciar transición
                    StartCoroutine(TransitionToRegion(region));
                }
            }
        }
    }

    int FindRegionByColor(Color clickedColor)
    {
        LevelData level = interactiveData.levels[currentLevel];
        
        for (int i = 0; i < level.regions.Length; i++)
        {
            Color regionColor = level.regions[i].maskColor;
            
            // Comparar colores con tolerancia
            float distance = Vector3.Distance(
                new Vector3(clickedColor.r, clickedColor.g, clickedColor.b),
                new Vector3(regionColor.r, regionColor.g, regionColor.b)
            );
            
            if (distance < 0.1f) // Tolerancia de color
            {
                return i;
            }
        }
        
        return -1;
    }

    IEnumerator TransitionToRegion(RegionData region)
    {
        isTransitioning = true;
        
        // Rotación de transición
        float elapsed = 0f;
        float rotationAmount = 360f;
        
        while (elapsed < transitionDuration)
        {
            float rotationThisFrame = (rotationAmount / transitionDuration) * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationThisFrame, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Zoom in
        Vector3 startPos = mainCamera.transform.position;
        Vector3 endPos = new Vector3(startPos.x, startPos.y, -zoomedDistance);
        elapsed = 0f;
        float zoomDuration = 0.5f;
        
        while (elapsed < zoomDuration)
        {
            mainCamera.transform.position = Vector3.Lerp(startPos, endPos, elapsed / zoomDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Verificar si es el último nivel (estados)
        if (currentLevel == 3) // Nivel de estados
        {
            // Cambiar a la escena específica del estado
            if (!string.IsNullOrEmpty(region.nextSceneName))
            {
                Debug.Log($"Cargando escena: {region.nextSceneName}");
                SceneManager.LoadScene(region.nextSceneName);
            }
            else
            {
                Debug.LogWarning("No se especificó escena para este estado");
            }
        }
        else
        {
            // Cargar siguiente nivel
            currentLevel++;
            
            if (currentLevel < interactiveData.levels.Length)
            {
                // Actualizar texturas al siguiente nivel
                planetMaterial.mainTexture = region.backgroundTexture;
                currentMaskTexture = region.maskTexture;
                
                // Actualizar el nivel actual en interactiveData
                interactiveData.levels[currentLevel].backgroundTexture = region.backgroundTexture;
                interactiveData.levels[currentLevel].maskTexture = region.maskTexture;
            }
        }
        
        isTransitioning = false;
    }

    public void FocusOnRegion(RegionCard regionCard)
    {
        Debug.Log($"Focusing on region: {regionCard.regionName}");

        // Hide all other regions at the same level
        RegionCard[] allCards = FindObjectsOfType<RegionCard>(true);
        foreach (var card in allCards)
        {
            if (card != regionCard && card.regionType == regionCard.regionType)
            {
                card.SetVisibility(false);
            }
        }

        // Show child regions if any
        if (regionCard.childRegions != null && regionCard.childRegions.Length > 0)
        {
            foreach (var child in regionCard.childRegions)
            {
                child.SetVisibility(true);
            }
        }

        // TODO: Implement level transition and texture update for planet
    }
}
