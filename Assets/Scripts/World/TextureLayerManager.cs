using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestiona las capas de texturas (background + mask) para cada nivel de navegación
/// Este script maneja el cambio dinámico de texturas según el contexto
/// VERSIÓN MEJORADA: Con mapeos dinámicos por nivel
/// </summary>
public class TextureLayerManager : MonoBehaviour
{
    [System.Serializable]
    public class TextureLayer
    {
        public string layerName;
        public Texture2D backgroundTexture;
        public Texture2D maskTexture;
        public RegionCard.RegionType levelType;
        
        [Header("Mapeos de Color para este Nivel")]
        public List<LayerColorMapping> colorMappings = new List<LayerColorMapping>();
    }
    
    [System.Serializable]
    public class LayerColorMapping
    {
        public string regionName;
        public Color maskColor;
        public RegionCard.RegionType regionType;
        public string sceneToLoad = "";
    }
    
    [Header("Configuración de Capas")]
    [Tooltip("Capa inicial (Continentes)")]
    [SerializeField] private TextureLayer worldLayer;
    
    [Header("Capas de Continentes")]
    [SerializeField] private List<TextureLayer> continentLayers = new List<TextureLayer>();
    
    [Header("Capas de Países")]
    [SerializeField] private List<TextureLayer> countryLayers = new List<TextureLayer>();
    
    [Header("Capas de Estados")]
    [SerializeField] private List<TextureLayer> stateLayers = new List<TextureLayer>();
    
    [Header("Referencias")]
    [SerializeField] private GameObject planet;
    [SerializeField] private PixelPerfectPlanetClick pixelClickSystem;
    
    private MeshRenderer planetRenderer;
    private Stack<TextureLayer> layerStack = new Stack<TextureLayer>();
    private TextureLayer currentLayer;
    
    void Start()
    {
        if (planet == null)
            planet = GameObject.Find("Planet");
            
        if (planet != null)
            planetRenderer = planet.GetComponent<MeshRenderer>();
            
        if (pixelClickSystem == null)
            pixelClickSystem = FindFirstObjectByType<PixelPerfectPlanetClick>();
        
        // Configurar capa inicial
        if (worldLayer != null && worldLayer.backgroundTexture != null)
        {
            currentLayer = worldLayer;
            ApplyLayer(worldLayer);
            Debug.Log("🌍 TextureLayerManager inicializado - Capa: " + worldLayer.layerName);
        }
        else
        {
            Debug.LogError("❌ No se configuró la capa mundial inicial!");
        }
    }
    
    /// <summary>
    /// Cambiar a una capa específica según el nombre de la región
    /// </summary>
    public void LoadLayerForRegion(string regionName, RegionCard.RegionType regionType)
    {
        Debug.Log($"🔄 Cargando capa para: {regionName} ({regionType})");
        
        TextureLayer targetLayer = null;
        
        switch (regionType)
        {
            case RegionCard.RegionType.Continent:
                targetLayer = continentLayers.Find(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                break;
                
            case RegionCard.RegionType.Country:
                targetLayer = countryLayers.Find(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                break;
                
            case RegionCard.RegionType.State:
                targetLayer = stateLayers.Find(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                break;
                
            case RegionCard.RegionType.Plant:
                Debug.Log("🏭 Planta detectada - No se requiere cambio de capa");
                return;
        }
        
        if (targetLayer != null && targetLayer.backgroundTexture != null && targetLayer.maskTexture != null)
        {
            // Guardar capa actual en el stack
            if (currentLayer != null)
            {
                layerStack.Push(currentLayer);
            }
            
            currentLayer = targetLayer;
            ApplyLayer(targetLayer);
            Debug.Log($"✅ Capa aplicada: {targetLayer.layerName}");
        }
        else
        {
            Debug.LogWarning($"⚠️ No se encontró capa para: {regionName} ({regionType})");
        }
    }
    
    /// <summary>
    /// Volver a la capa anterior
    /// </summary>
    public void GoToPreviousLayer()
    {
        if (layerStack.Count > 0)
        {
            currentLayer = layerStack.Pop();
            ApplyLayer(currentLayer);
            Debug.Log($"⬅️ Capa anterior restaurada: {currentLayer.layerName}");
        }
        else
        {
            // Volver a la capa mundial
            currentLayer = worldLayer;
            ApplyLayer(worldLayer);
            Debug.Log("🏠 Volviendo a capa mundial");
        }
    }
    
    /// <summary>
    /// Aplicar una capa (background + mask + mapeos)
    /// </summary>
    private void ApplyLayer(TextureLayer layer)
    {
        if (layer == null) return;
        
        // Aplicar background al planeta (lo que ve el usuario)
        if (planetRenderer != null && layer.backgroundTexture != null)
        {
            planetRenderer.material.mainTexture = layer.backgroundTexture;
        }
        
        // Actualizar la máscara en el sistema de detección de clicks
        if (pixelClickSystem != null && layer.maskTexture != null)
        {
            pixelClickSystem.UpdateColorMask(layer.maskTexture);
            
            // 🆕 NUEVO: Actualizar mapeos dinámicamente
            UpdateMappingsForLayer(layer);
        }
        
        Debug.Log($"🎨 Capa aplicada - BG: {layer.backgroundTexture?.name}, Mask: {layer.maskTexture?.name}");
    }
    
    /// <summary>
    /// 🆕 NUEVO: Actualizar mapeos de color según la capa actual
    /// </summary>
    private void UpdateMappingsForLayer(TextureLayer layer)
    {
        if (pixelClickSystem == null || layer == null) return;
        
        // Convertir los mapeos del layer a mapeos del PixelClickSystem
        List<PixelPerfectPlanetClick.ColorRegionMapping> clickMappings = 
            new List<PixelPerfectPlanetClick.ColorRegionMapping>();
        
        foreach (var mapping in layer.colorMappings)
        {
            var clickMapping = new PixelPerfectPlanetClick.ColorRegionMapping
            {
                regionName = mapping.regionName,
                maskColor = mapping.maskColor,
                regionType = mapping.regionType,
                sceneToLoad = mapping.sceneToLoad,
                childRegions = new RegionCard[0] // Se manejan por el sistema de capas
            };
            
            clickMappings.Add(clickMapping);
        }
        
        // Actualizar los mapeos en el PixelClickSystem
        pixelClickSystem.UpdateColorMappings(clickMappings);
        
        Debug.Log($"🔄 Mapeos actualizados: {clickMappings.Count} regiones para nivel {layer.layerName}");
    }
    
    /// <summary>
    /// Resetear al estado inicial
    /// </summary>
    public void ResetToWorldLayer()
    {
        layerStack.Clear();
        currentLayer = worldLayer;
        ApplyLayer(worldLayer);
        Debug.Log("🔄 Sistema reseteado a capa mundial");
    }
    
    /// <summary>
    /// Obtener la capa actual
    /// </summary>
    public TextureLayer GetCurrentLayer()
    {
        return currentLayer;
    }
    
    /// <summary>
    /// Verificar si una región tiene capa disponible
    /// </summary>
    public bool HasLayerForRegion(string regionName, RegionCard.RegionType regionType)
    {
        switch (regionType)
        {
            case RegionCard.RegionType.Continent:
                return continentLayers.Exists(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                
            case RegionCard.RegionType.Country:
                return countryLayers.Exists(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                
            case RegionCard.RegionType.State:
                return stateLayers.Exists(layer => 
                    layer.layerName.Equals(regionName, System.StringComparison.OrdinalIgnoreCase));
                
            default:
                return false;
        }
    }
    
    [ContextMenu("Listar Todas las Capas")]
    private void ListAllLayers()
    {
        Debug.Log("=== CAPAS CONFIGURADAS ===");
        Debug.Log($"Mundial: {worldLayer?.layerName}");
        Debug.Log($"Continentes: {continentLayers.Count}");
        foreach (var layer in continentLayers)
            Debug.Log($"  - {layer.layerName}");
        Debug.Log($"Países: {countryLayers.Count}");
        foreach (var layer in countryLayers)
            Debug.Log($"  - {layer.layerName}");
        Debug.Log($"Estados: {stateLayers.Count}");
        foreach (var layer in stateLayers)
            Debug.Log($"  - {layer.layerName}");
    }
}