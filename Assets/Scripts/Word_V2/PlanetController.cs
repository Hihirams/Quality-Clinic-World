using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Word_V2
{
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
    
    [Header("Debug")]
    public bool showDebug = true;
    
    private int currentLevel = 0;
    private Texture2D currentMaskTexture;
    private bool isTransitioning = false;
    private int selectedRegionIndex = -1;
    private MeshCollider meshCollider;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Verificar o añadir MeshCollider
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            Debug.Log("✅ MeshCollider añadido automáticamente");
        }
        
        if (interactiveData == null)
        {
            Debug.LogError("⚠️ FALTA ASIGNAR InteractiveData!");
            return;
        }
        
        LoadLevel(0);
    }

    void Update()
    {
        if (!isTransitioning)
        {
            transform.Rotate(Vector3.up, idleRotationSpeed * Time.deltaTime, Space.World);
        }
        
        if (Input.GetMouseButtonDown(0) && !isTransitioning)
        {
            Debug.Log("🖱️ Click detectado!");
            HandleClick();
        }
    }

    void LoadLevel(int levelIndex)
    {
        if (interactiveData == null || levelIndex >= interactiveData.levels.Length)
        {
            Debug.LogError($"❌ Error: Nivel {levelIndex} fuera de rango!");
            return;
        }
        
        currentLevel = levelIndex;
        LevelData level = interactiveData.levels[levelIndex];
        
        if (level.backgroundTexture == null)
        {
            Debug.LogError($"❌ Background Texture NULL en nivel {levelIndex}");
            return;
        }
        
        if (level.maskTexture == null)
        {
            Debug.LogError($"❌ Mask Texture NULL en nivel {levelIndex}");
            return;
        }
        
        // Verificar que la textura tenga Read/Write habilitado
        try
        {
            Color testPixel = level.maskTexture.GetPixel(0, 0);
            Debug.Log("✅ Mask Texture tiene Read/Write habilitado");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ ERROR: La textura {level.maskTexture.name} NO tiene Read/Write habilitado! {e.Message}");
            return;
        }
        
        planetMaterial.mainTexture = level.backgroundTexture;
        currentMaskTexture = level.maskTexture;
        
        Debug.Log($"✅ Nivel cargado: {level.levelName}");
        Debug.Log($"   Background: {level.backgroundTexture.name}");
        Debug.Log($"   Mask: {level.maskTexture.name} ({level.maskTexture.width}x{level.maskTexture.height})");
    }

    void HandleClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        Debug.Log("🔍 Lanzando Raycast...");
        
        if (Physics.Raycast(ray, out hit))
        {
            Debug.Log($"✅ Raycast golpeó: {hit.collider.gameObject.name}");
            
            if (hit.collider.gameObject == gameObject)
            {
                Debug.Log("✅ Click en el planeta!");
                
                if (currentMaskTexture == null)
                {
                    Debug.LogError("❌ currentMaskTexture es NULL!");
                    return;
                }
                
                Vector2 uv = hit.textureCoord;
                Debug.Log($"📍 UV RAW: {uv}");
                
                // Verificar si UV es válido
                if (uv.x == 0 && uv.y == 0)
                {
                    Debug.LogWarning("⚠️ UV es (0,0) - Problema con el mesh o collider!");
                    Debug.LogWarning("💡 Intenta usar Mesh Collider en lugar de Sphere Collider");
                    
                    // Intentar calcular UV desde el punto de hit
                    Vector3 localPoint = transform.InverseTransformPoint(hit.point);
                    float u = 0.5f + Mathf.Atan2(localPoint.z, localPoint.x) / (2f * Mathf.PI);
                    float v = 0.5f - Mathf.Asin(localPoint.y) / Mathf.PI;
                    uv = new Vector2(u, v);
                    Debug.Log($"📍 UV CALCULADO: {uv}");
                }
                
                // Asegurar que UV esté en rango [0,1]
                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);
                
                int x = Mathf.FloorToInt(uv.x * currentMaskTexture.width);
                int y = Mathf.FloorToInt(uv.y * currentMaskTexture.height);
                
                // Asegurar que estén dentro de los límites
                x = Mathf.Clamp(x, 0, currentMaskTexture.width - 1);
                y = Mathf.Clamp(y, 0, currentMaskTexture.height - 1);
                
                Debug.Log($"📍 Pixel: ({x}, {y}) en textura {currentMaskTexture.width}x{currentMaskTexture.height}");
                
                Color clickedColor = currentMaskTexture.GetPixel(x, y);
                Debug.Log($"🎨 Color clickeado: R={clickedColor.r:F3} G={clickedColor.g:F3} B={clickedColor.b:F3} A={clickedColor.a:F3}");
                
                int regionIndex = FindRegionByColor(clickedColor);
                
                if (regionIndex >= 0)
                {
                    selectedRegionIndex = regionIndex;
                    LevelData level = interactiveData.levels[currentLevel];
                    RegionData region = level.regions[regionIndex];
                    
                    Debug.Log($"🌍 Continente/Región: {region.regionName} clickeado");
                    
                    StartCoroutine(TransitionToRegion(region));
                }
                else
                {
                    Debug.LogWarning("⚠️ No se encontró región con ese color");
                }
            }
        }
        else
        {
            Debug.LogWarning("❌ Raycast no golpeó nada");
        }
    }

    int FindRegionByColor(Color clickedColor)
    {
        LevelData level = interactiveData.levels[currentLevel];
        
        if (level.regions == null || level.regions.Length == 0)
        {
            Debug.LogError("❌ No hay regiones configuradas!");
            return -1;
        }
        
        Debug.Log($"🔍 Buscando entre {level.regions.Length} regiones...");
        
        for (int i = 0; i < level.regions.Length; i++)
        {
            Color regionColor = level.regions[i].maskColor;
            
            float distance = Vector3.Distance(
                new Vector3(clickedColor.r, clickedColor.g, clickedColor.b),
                new Vector3(regionColor.r, regionColor.g, regionColor.b)
            );
            
            Debug.Log($"   Región [{i}] {level.regions[i].regionName}: Color R={regionColor.r:F3} G={regionColor.g:F3} B={regionColor.b:F3}, Distancia={distance:F3}");
            
            if (distance < 0.2f) // Aumenté la tolerancia
            {
                Debug.Log($"✅ ¡Match encontrado! Región: {level.regions[i].regionName}");
                return i;
            }
        }
        
        return -1;
    }

    IEnumerator TransitionToRegion(RegionData region)
    {
        isTransitioning = true;
        Debug.Log($"🔄 Iniciando transición a: {region.regionName}");
        
        float elapsed = 0f;
        float rotationAmount = 360f;
        
        while (elapsed < transitionDuration)
        {
            float rotationThisFrame = (rotationAmount / transitionDuration) * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationThisFrame, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
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
        
        if (currentLevel == 3)
        {
            if (!string.IsNullOrEmpty(region.nextSceneName))
            {
                Debug.Log($"🎬 Cargando escena: {region.nextSceneName}");
                SceneManager.LoadScene(region.nextSceneName);
            }
            else
            {
                Debug.LogWarning("⚠️ No se especificó escena para este estado");
            }
        }
        else
        {
            currentLevel++;
            
            if (currentLevel < interactiveData.levels.Length)
            {
                planetMaterial.mainTexture = region.backgroundTexture;
                currentMaskTexture = region.maskTexture;
                
                interactiveData.levels[currentLevel].backgroundTexture = region.backgroundTexture;
                interactiveData.levels[currentLevel].maskTexture = region.maskTexture;
                
                Debug.Log($"✅ Nivel {currentLevel} cargado");
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
}
