using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de detección pixel-perfect usando una máscara de colores invisible
/// </summary>
public class PixelPerfectPlanetClick : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject planet;
    [SerializeField] private Texture2D colorMask;
    
    [Header("Mapeo de Colores a Regiones")]
    [SerializeField] private List<ColorRegionMapping> colorMappings = new List<ColorRegionMapping>();
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showMaskOnPlanet = false;
    [SerializeField] private GameObject debugMarker;
    
    private PlanetController planetController;
    private Camera mainCamera;
    
    [System.Serializable]
    public class ColorRegionMapping
    {
        public string regionName;
        public Color maskColor;
        public RegionCard.RegionType regionType;
        public RegionCard[] childRegions;
    }
    
    void Start()
    {
        if (planet == null)
            planet = GameObject.Find("Planet");
        
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
        if (mainCamera == null)
            mainCamera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            
        planetController = FindObjectOfType<PlanetController>();
        
        if (planet == null)
        {
            Debug.LogError("No se encontró el planeta!");
            return;
        }
        
        if (mainCamera == null)
        {
            Debug.LogError("No se encontró la cámara!");
            return;
        }
        
        if (colorMask == null)
        {
            Debug.LogError("No se asignó la máscara de colores!");
            return;
        }
        
        if (!colorMask.isReadable)
        {
            Debug.LogError("La máscara debe tener 'Read/Write Enabled'!");
            return;
        }
        
        Debug.Log($"Sistema configurado. Máscara: {colorMask.width}x{colorMask.height}, {colorMappings.Count} regiones");
        
        if (showMaskOnPlanet)
        {
            StartCoroutine(ApplyDebugMaskDelayed());
        }
    }
    
    private System.Collections.IEnumerator ApplyDebugMaskDelayed()
    {
        yield return new WaitForEndOfFrame();
        
        MeshRenderer renderer = planet.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = colorMask;
            Debug.Log("Máscara de debug aplicada");
        }
    }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            DetectClickOnPlanet();
        }
    }
    
private void DetectClickOnPlanet()
{
    if (mainCamera == null)
        mainCamera = Camera.main;
        
    if (mainCamera == null || colorMask == null)
    {
        Debug.LogError("❌ Falta cámara o máscara");
        return;
    }
    
    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    RaycastHit hit;
    
    if (Physics.Raycast(ray, out hit))
    {
        if (hit.collider.gameObject == planet)
        {
            Vector2 uv = hit.textureCoord;
            
            // Si el collider no proporciona UV, calcular manualmente
            if (uv.sqrMagnitude < 0.0001f)
            {
                Vector3 localHitPoint = planet.transform.InverseTransformPoint(hit.point);
                Vector3 dir = localHitPoint.normalized;
                
                // Fórmula ajustada para tu orientación de textura (V invertido)
                float u = 0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI);
                float v = 0.5f + Mathf.Asin(dir.y) / Mathf.PI;
                
                uv = new Vector2(u, v);
            }
            
            // Convertir UV a coordenadas de pixel
            int x = Mathf.FloorToInt(uv.x * colorMask.width);
            int y = Mathf.FloorToInt(uv.y * colorMask.height);
            
            x = Mathf.Clamp(x, 0, colorMask.width - 1);
            y = Mathf.Clamp(y, 0, colorMask.height - 1);
            
            Color maskPixelColor = colorMask.GetPixel(x, y);
            
            if (showDebugLogs)
            {
                Debug.Log($"🎯 Click en UV: ({uv.x:F2}, {uv.y:F2}), Pixel: ({x},{y}), Color: RGB({maskPixelColor.r:F2}, {maskPixelColor.g:F2}, {maskPixelColor.b:F2})");
                MarkPixelForDebug(x, y);
            }
            
            ColorRegionMapping matchedRegion = FindRegionByMaskColor(maskPixelColor);
            
            if (matchedRegion != null)
            {
                Debug.Log($"✅ Región detectada: {matchedRegion.regionName}");
                HandleRegionClick(matchedRegion);
            }
            else
            {
                Debug.Log($"⚪ Click en área sin asignar (océano o fuera de continentes)");
            }
        }
    }
}
    
    private ColorRegionMapping FindRegionByMaskColor(Color clickedColor)
    {
        foreach (var mapping in colorMappings)
        {
            if (ColorsMatch(clickedColor, mapping.maskColor, 0.15f))
            {
                return mapping;
            }
        }
        return null;
    }
    
private bool ColorsMatch(Color a, Color b, float tolerance)
{
    return Mathf.Abs(a.r - b.r) < tolerance &&
           Mathf.Abs(a.g - b.g) < tolerance &&
           Mathf.Abs(a.b - b.b) < tolerance;
}
    
    private void HandleRegionClick(ColorRegionMapping region)
    {
        if (planetController == null)
        {
            Debug.LogWarning("No hay PlanetController");
            return;
        }
        
        // Crear un RegionCard temporal para usar el sistema existente
        GameObject tempObj = new GameObject($"Temp_{region.regionName}");
        tempObj.transform.position = planet.transform.position;
        tempObj.transform.SetParent(planet.transform);
        
        RegionCard tempCard = tempObj.AddComponent<RegionCard>();
        tempCard.regionName = region.regionName;
        tempCard.regionType = region.regionType;
        tempCard.childRegions = region.childRegions;
        tempCard.rotatesWithPlanet = true;
        
        planetController.FocusOnRegion(tempCard);
    }
    
    private void MarkPixelForDebug(int x, int y)
    {
        if (colorMask == null) return;
        
        // Marcar con cruz roja de 5x5 píxeles
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int px = Mathf.Clamp(x + dx, 0, colorMask.width - 1);
                int py = Mathf.Clamp(y + dy, 0, colorMask.height - 1);
                colorMask.SetPixel(px, py, Color.red);
            }
        }
        colorMask.Apply();
    }
    
    [ContextMenu("Exportar Máscara Debug")]
    private void ExportDebugMask()
    {
        if (colorMask == null) return;
        
        byte[] bytes = colorMask.EncodeToPNG();
        string path = Application.dataPath + "/WorldMask_Debug.png";
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"💾 Guardado en: {path}");
    }
    
    [ContextMenu("Listar Mapeos")]
    private void TestListMappings()
    {
        Debug.Log("=== MAPEOS ===");
        for (int i = 0; i < colorMappings.Count; i++)
        {
            var m = colorMappings[i];
            Debug.Log($"{i}: {m.regionName} - RGB({m.maskColor.r:F2}, {m.maskColor.g:F2}, {m.maskColor.b:F2})");
        }
    }
}