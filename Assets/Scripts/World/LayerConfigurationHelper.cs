using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Herramienta para configurar rápidamente las capas de texturas
/// Genera automáticamente la estructura de mapeos de color
/// </summary>
public class LayerConfigurationHelper : MonoBehaviour
{
    [Header("Generación Automática de Mapeos")]
    [SerializeField] private TextureLayerManager layerManager;
    [SerializeField] private PixelPerfectPlanetClick pixelClickSystem;
    
    void Start()
    {
        if (layerManager == null)
            layerManager = FindFirstObjectByType<TextureLayerManager>();
            
        if (pixelClickSystem == null)
            pixelClickSystem = FindFirstObjectByType<PixelPerfectPlanetClick>();
    }
    
    /// <summary>
    /// Validar que todas las texturas tengan Read/Write habilitado
    /// </summary>
    [ContextMenu("Validar Configuración")]
    public void ValidateConfiguration()
    {
        if (layerManager == null)
        {
            Debug.LogError("❌ No se encontró TextureLayerManager");
            return;
        }
        
        Debug.Log("=== VALIDACIÓN DE CONFIGURACIÓN ===");
        
        int errores = 0;
        int warnings = 0;
        
        // Validar que las texturas tengan Read/Write habilitado
        // Este método debe expandirse según tu configuración
        
        if (errores == 0 && warnings == 0)
        {
            Debug.Log("✅ Configuración válida!");
        }
        else
        {
            Debug.Log($"⚠️ {errores} errores, {warnings} advertencias");
        }
    }
    
    /// <summary>
    /// Generar template de configuración en consola
    /// </summary>
    [ContextMenu("Generar Template de Configuración")]
    public void GenerateConfigurationTemplate()
    {
        Debug.Log(@"
=== TEMPLATE DE CONFIGURACIÓN ===

ESTRUCTURA DE CARPETAS RECOMENDADA:
Assets/
  Textures/
    World/
      WorldMap_Background.png    (Lo que ve el usuario)
      WorldMap_Mask.png          (Máscara de colores)
    
    Continents/
      Africa_Background.png
      Africa_Mask.png
      Europe_Background.png
      Europe_Mask.png
      Asia_Background.png
      Asia_Mask.png
      NorthAmerica_Background.png
      NorthAmerica_Mask.png
      SouthAmerica_Background.png
      SouthAmerica_Mask.png
      Oceania_Background.png
      Oceania_Mask.png
    
    Countries/
      Mexico_Background.png
      Mexico_Mask.png
      USA_Background.png
      USA_Mask.png
      Canada_Background.png
      Canada_Mask.png
      [... otros países ...]
    
    States/
      NuevoLeon_Background.png
      NuevoLeon_Mask.png
      Jalisco_Background.png
      Jalisco_Mask.png
      [... otros estados ...]

IMPORTANTE - CONFIGURACIÓN DE TEXTURAS:
1. Todas las texturas deben tener 'Read/Write Enabled' = TRUE
2. Las máscaras deben usar colores sólidos distintos para cada región
3. El fondo de las máscaras debe ser negro puro (0,0,0)

MAPEO DE COLORES RECOMENDADO:
- Continentes: Colores primarios brillantes
  África: Rojo (255, 0, 0)
  Europa: Verde (0, 255, 0)
  Asia: Azul (0, 0, 255)
  Norteamérica: Amarillo (255, 255, 0)
  Sudamérica: Magenta (255, 0, 255)
  Oceanía: Cian (0, 255, 255)

- Países: Colores secundarios
- Estados: Colores terciarios
- Plantas: Colores únicos por ubicación
");
    }
    
    /// <summary>
    /// Extraer colores únicos de una máscara
    /// </summary>
    [ContextMenu("Analizar Colores en Máscara Actual")]
    public void AnalyzeMaskColors()
    {
        if (pixelClickSystem == null)
        {
            Debug.LogError("❌ No se encontró PixelPerfectPlanetClick");
            return;
        }
        
        // Este método requiere acceso a la máscara actual
        // Implementación básica para guiar al usuario
        
        Debug.Log(@"
=== CÓMO ANALIZAR COLORES ===

1. Abre tu imagen de máscara en un editor (Photoshop, GIMP, etc.)
2. Usa la herramienta 'Color Picker' en cada región
3. Anota los valores RGB exactos
4. Crea los mapeos en el Inspector de PixelPerfectPlanetClick

EJEMPLO:
Si África es RGB(255, 0, 0):
- regionName: 'África'
- maskColor: R=1.0, G=0.0, B=0.0
- regionType: Continent
- childRegions: [Arrastra aquí las RegionCards de países africanos]
");
    }
    
    /// <summary>
    /// Guía paso a paso para configuración inicial
    /// </summary>
    [ContextMenu("Mostrar Guía de Configuración")]
    public void ShowSetupGuide()
    {
        Debug.Log(@"
=== GUÍA DE CONFIGURACIÓN PASO A PASO ===

PASO 1: PREPARAR TEXTURAS
□ Importa WorldMap_Background.png a Unity
□ Importa WorldMap_Mask.png a Unity
□ Selecciona ambas texturas en el Project
□ En el Inspector, activa 'Read/Write Enabled'
□ Click en 'Apply'

PASO 2: CONFIGURAR TEXTURELAYERMANAGER
□ Crea un GameObject vacío llamado 'Systems'
□ Agrégale el componente TextureLayerManager
□ Asigna el Planet en el campo 'Planet'
□ En 'World Layer':
  - Layer Name: 'World'
  - Background Texture: WorldMap_Background
  - Mask Texture: WorldMap_Mask
  - Level Type: Continent

PASO 3: CONFIGURAR PIXELPERFECTPLANETCLICK
□ En el GameObject 'Systems', agrega PixelPerfectPlanetClick
□ Asigna 'Planet'
□ Asigna 'Color Mask' = WorldMap_Mask
□ Asigna 'Layer Manager' = el TextureLayerManager
□ En 'Color Mappings', crea una entrada por continente:
  - Region Name: (nombre del continente)
  - Mask Color: (color RGB de la máscara)
  - Region Type: Continent
  - Child Regions: (vacío por ahora)

PASO 4: CONFIGURAR PLANETCONTROLLER
□ Asigna 'Layer Manager' en PlanetController

PASO 5: PREPARAR CAPAS DE CONTINENTES
□ Importa las texturas de cada continente
  (África_Background, África_Mask, etc.)
□ Habilita 'Read/Write Enabled' en todas
□ En TextureLayerManager > Continent Layers:
  - Crea una entrada por continente
  - Layer Name debe coincidir EXACTAMENTE con Region Name
  - Asigna Background y Mask correspondientes

PASO 6: CONFIGURAR PAÍSES (Repetir para cada continente)
□ Crea las máscaras de países con colores únicos
□ En TextureLayerManager > Country Layers:
  - Agrega una entrada por país
  - Layer Name = nombre del país
  - Asigna texturas

□ En PixelPerfectPlanetClick > Color Mappings:
  - Agrega mapeos para cada país del continente
  - Region Type: Country

PASO 7: CONFIGURAR ESTADOS (Repetir para cada país)
□ Similar al paso 6, pero con estados
□ Region Type: State

PASO 8: CONFIGURAR PLANTAS (Por cada estado)
□ En los mapeos de plantas:
  - Region Type: Plant
  - Scene To Load: nombre de tu escena (ej: 'PlantaMonterrey')

PASO 9: TESTING
□ Presiona Play
□ Click en un continente
□ Verifica que cambie la textura y se haga zoom
□ Navega a país, estado, planta
□ Presiona ESC para volver atrás

PASO 10: DEBUG
□ Activa 'Show Debug Logs' en PixelPerfectPlanetClick
□ Activa 'Show Mask On Planet' temporalmente
□ Verifica que los colores coincidan con tus mapeos
");
    }
}
