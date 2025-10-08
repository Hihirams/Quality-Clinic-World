using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Herramienta de debug para verificar configuración de máscaras y colores
/// Adjunta este script a un GameObject vacío en tu escena
/// </summary>
public class DebugHelper : MonoBehaviour
{
    [Header("Verificación de Máscara")]
    [SerializeField] private Texture2D maskToCheck;
    [SerializeField] private bool showMaskOnPlanet = false;
    [SerializeField] private GameObject planet;
    
    [Header("Visualización de Clicks")]
    [SerializeField] private bool visualizeClicks = true;
    [SerializeField] private GameObject clickMarkerPrefab;
    [SerializeField] private float markerDuration = 2f;
    
    [Header("Análisis de Colores")]
    [SerializeField] private bool analyzeOnStart = false;
    
    private Camera mainCamera;
    private List<GameObject> activeMarkers = new List<GameObject>();
    
    void Start()
    {
        mainCamera = Camera.main;
        
        if (planet == null)
            planet = GameObject.Find("Planet");
        
        if (analyzeOnStart && maskToCheck != null)
        {
            AnalyzeMaskColors();
        }
        
        if (showMaskOnPlanet)
        {
            ShowMaskOnPlanet();
        }
    }
    
    void Update()
    {
        if (visualizeClicks && Input.GetMouseButtonDown(0))
        {
            VisualizeClick();
        }
        
        // Limpiar marcadores antiguos
        activeMarkers.RemoveAll(marker => marker == null);
    }
    
    /// <summary>
    /// Analiza la máscara y lista todos los colores únicos encontrados
    /// </summary>
    [ContextMenu("Analizar Colores de la Máscara")]
    void AnalyzeMaskColors()
    {
        if (maskToCheck == null)
        {
            Debug.LogError("❌ No hay máscara asignada");
            return;
        }
        
        if (!maskToCheck.isReadable)
        {
            Debug.LogError("❌ La máscara debe tener Read/Write habilitado");
            return;
        }
        
        Debug.Log($"\n{'='*60}");
        Debug.Log($"🎨 ANÁLISIS DE MÁSCARA: {maskToCheck.name}");
        Debug.Log($"Resolución: {maskToCheck.width}x{maskToCheck.height}");
        Debug.Log($"{'='*60}\n");
        
        Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
        
        // Analizar todos los pixels
        for (int y = 0; y < maskToCheck.height; y++)
        {
            for (int x = 0; x < maskToCheck.width; x++)
            {
                Color pixel = maskToCheck.GetPixel(x, y);
                
                // Redondear para agrupar colores similares
                Color roundedColor = new Color(
                    Mathf.Round(pixel.r * 100f) / 100f,
                    Mathf.Round(pixel.g * 100f) / 100f,
                    Mathf.Round(pixel.b * 100f) / 100f,
                    1f
                );
                
                if (colorCounts.ContainsKey(roundedColor))
                {
                    colorCounts[roundedColor]++;
                }
                else
                {
                    colorCounts[roundedColor] = 1;
                }
            }
        }
        
        // Mostrar resultados ordenados por cantidad
        Debug.Log($"📊 Se encontraron {colorCounts.Count} colores únicos:\n");
        
        var sortedColors = new List<KeyValuePair<Color, int>>(colorCounts);
        sortedColors.Sort((a, b) => b.Value.CompareTo(a.Value));
        
        int index = 0;
        foreach (var pair in sortedColors)
        {
            Color c = pair.Key;
            int count = pair.Value;
            float percentage = (count * 100f) / (maskToCheck.width * maskToCheck.height);
            
            string colorName = GetColorName(c);
            
            Debug.Log($"{index}. RGB({c.r:F2}, {c.g:F2}, {c.b:F2}) - {colorName}");
            Debug.Log($"   Pixels: {count:N0} ({percentage:F2}%)");
            Debug.Log($"   Unity Inspector: ({c.r}, {c.g}, {c.b})");
            Debug.Log($"   Hex: #{ColorUtility.ToHtmlStringRGB(c)}\n");
            
            index++;
        }
        
        Debug.Log($"{'='*60}");
        Debug.Log("💡 COPIA estos valores RGB al Inspector de Unity");
        Debug.Log($"{'='*60}\n");
    }
    
    /// <summary>
    /// Muestra la máscara directamente en el planeta (para verificar alineación)
    /// </summary>
    [ContextMenu("Mostrar Máscara en Planeta")]
    void ShowMaskOnPlanet()
    {
        if (planet == null || maskToCheck == null)
        {
            Debug.LogWarning("⚠️ Falta planeta o máscara");
            return;
        }
        
        MeshRenderer renderer = planet.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = maskToCheck;
            Debug.Log("✅ Máscara visible en planeta - Presiona 'Ocultar Máscara' para restaurar");
        }
    }
    
    /// <summary>
    /// Restaura la textura original del planeta
    /// </summary>
    [ContextMenu("Ocultar Máscara del Planeta")]
    void HideMaskFromPlanet()
    {
        MultiLevelPlanetSystem system = FindFirstObjectByType<MultiLevelPlanetSystem>();
        if (system != null)
        {
            system.ResetToInitialView();
            Debug.Log("✅ Textura original restaurada");
        }
    }
    
    /// <summary>
    /// Visualiza dónde hizo click el usuario
    /// </summary>
    void VisualizeClick()
    {
        if (mainCamera == null || planet == null) return;
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == planet)
            {
                Vector2 uv = GetUVFromHit(hit);
                Color maskColor = Color.black;
                
                if (maskToCheck != null && maskToCheck.isReadable)
                {
                    int x = Mathf.FloorToInt(uv.x * maskToCheck.width);
                    int y = Mathf.FloorToInt(uv.y * maskToCheck.height);
                    x = Mathf.Clamp(x, 0, maskToCheck.width - 1);
                    y = Mathf.Clamp(y, 0, maskToCheck.height - 1);
                    maskColor = maskToCheck.GetPixel(x, y);
                }
                
                Debug.Log($"🎯 CLICK DETECTADO");
                Debug.Log($"   Posición 3D: {hit.point}");
                Debug.Log($"   UV: ({uv.x:F3}, {uv.y:F3})");
                Debug.Log($"   Color: RGB({maskColor.r:F2}, {maskColor.g:F2}, {maskColor.b:F2})");
                Debug.Log($"   Nombre: {GetColorName(maskColor)}");
                Debug.Log($"   Para copiar: new Color({maskColor.r}f, {maskColor.g}f, {maskColor.b}f)");
                
                // Crear marcador visual
                CreateClickMarker(hit.point, maskColor);
            }
        }
    }
    
    void CreateClickMarker(Vector3 position, Color color)
    {
        GameObject marker;
        
        if (clickMarkerPrefab != null)
        {
            marker = Instantiate(clickMarkerPrefab, position, Quaternion.identity);
        }
        else
        {
            // Crear esfera simple si no hay prefab
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.2f;
            
            // Remover collider
            Destroy(marker.GetComponent<Collider>());
            
            // Aplicar color
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = color;
                renderer.material = mat;
            }
        }
        
        marker.name = $"ClickMarker_{System.DateTime.Now.Ticks}";
        activeMarkers.Add(marker);
        
        // Destruir después de un tiempo
        Destroy(marker, markerDuration);
    }
    
    Vector2 GetUVFromHit(RaycastHit hit)
    {
        Vector2 uv = hit.textureCoord;
        
        if (uv.sqrMagnitude < 0.0001f && planet != null)
        {
            Vector3 localHitPoint = planet.transform.InverseTransformPoint(hit.point);
            Vector3 dir = localHitPoint.normalized;
            
            float u = 0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI);
            float v = 0.5f + Mathf.Asin(dir.y) / Mathf.PI;
            
            uv = new Vector2(u, v);
        }
        
        return uv;
    }
    
    /// <summary>
    /// Intenta dar nombre a los colores comunes
    /// </summary>
    string GetColorName(Color c)
    {
        // Negro (fondo)
        if (c.r < 0.1f && c.g < 0.1f && c.b < 0.1f)
            return "🖤 Negro (Fondo/Océano)";
        
        // Colores primarios
        if (c.r > 0.9f && c.g < 0.1f && c.b < 0.1f) return "🔴 Rojo";
        if (c.r < 0.1f && c.g > 0.9f && c.b < 0.1f) return "🟢 Verde";
        if (c.r < 0.1f && c.g < 0.1f && c.b > 0.9f) return "🔵 Azul";
        
        // Colores secundarios
        if (c.r > 0.9f && c.g > 0.9f && c.b < 0.1f) return "🟡 Amarillo";
        if (c.r > 0.9f && c.g < 0.1f && c.b > 0.9f) return "🟣 Magenta";
        if (c.r < 0.1f && c.g > 0.9f && c.b > 0.9f) return "🔵 Cyan";
        
        // Blanco
        if (c.r > 0.9f && c.g > 0.9f && c.b > 0.9f) return "⚪ Blanco";
        
        // Gris
        if (Mathf.Abs(c.r - c.g) < 0.1f && Mathf.Abs(c.g - c.b) < 0.1f)
            return $"⚫ Gris ({c.r:F2})";
        
        return "🎨 Color personalizado";
    }
    
    /// <summary>
    /// Genera código C# para copiar al Inspector
    /// </summary>
    [ContextMenu("Generar Código de Ejemplo")]
    void GenerateExampleCode()
    {
        if (maskToCheck == null || !maskToCheck.isReadable)
        {
            Debug.LogError("❌ Necesitas una máscara válida");
            return;
        }
        
        Debug.Log("\n// CÓDIGO DE EJEMPLO - Copia esto a tu configuración:");
        Debug.Log("// ============================================\n");
        
        Dictionary<Color, bool> uniqueColors = new Dictionary<Color, bool>();
        
        for (int y = 0; y < maskToCheck.height; y++)
        {
            for (int x = 0; x < maskToCheck.width; x++)
            {
                Color pixel = maskToCheck.GetPixel(x, y);
                Color rounded = new Color(
                    Mathf.Round(pixel.r * 100f) / 100f,
                    Mathf.Round(pixel.g * 100f) / 100f,
                    Mathf.Round(pixel.b * 100f) / 100f,
                    1f
                );
                
                if (rounded.r > 0.1f || rounded.g > 0.1f || rounded.b > 0.1f)
                {
                    uniqueColors[rounded] = true;
                }
            }
        }
        
        int index = 0;
        foreach (var color in uniqueColors.Keys)
        {
            Debug.Log($"// Región {index + 1}:");
            Debug.Log($"continentTexturesList[{index}].maskColor = new Color({color.r}f, {color.g}f, {color.b}f);\n");
            index++;
        }
        
        Debug.Log("// ============================================\n");
    }
    
    /// <summary>
    /// Exporta una imagen con los colores anotados
    /// </summary>
    [ContextMenu("Exportar Máscara Anotada")]
    void ExportAnnotatedMask()
    {
        if (maskToCheck == null)
        {
            Debug.LogError("❌ No hay máscara asignada");
            return;
        }
        
        string path = Application.dataPath + $"/{maskToCheck.name}_Annotated.png";
        byte[] bytes = maskToCheck.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        
        Debug.Log($"💾 Máscara exportada a: {path}");
        
#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }
}

#if UNITY_EDITOR
/// <summary>
/// Inspector personalizado con botones grandes
/// </summary>
[CustomEditor(typeof(DebugHelper))]
public class DebugHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        DebugHelper helper = (DebugHelper)target;
        
        GUILayout.Space(10);
        GUILayout.Label("🔍 HERRAMIENTAS DE ANÁLISIS", EditorStyles.boldLabel);
        
        if (GUILayout.Button("📊 Analizar Colores de la Máscara", GUILayout.Height(40)))
        {
            helper.SendMessage("AnalyzeMaskColors");
        }
        
        if (GUILayout.Button("👁️ Mostrar Máscara en Planeta", GUILayout.Height(40)))
        {
            helper.SendMessage("ShowMaskOnPlanet");
        }
        
        if (GUILayout.Button("🙈 Ocultar Máscara del Planeta", GUILayout.Height(40)))
        {
            helper.SendMessage("HideMaskFromPlanet");
        }
        
        if (GUILayout.Button("💾 Exportar Máscara Anotada", GUILayout.Height(40)))
        {
            helper.SendMessage("ExportAnnotatedMask");
        }
        
        if (GUILayout.Button("</> Generar Código de Ejemplo", GUILayout.Height(40)))
        {
            helper.SendMessage("GenerateExampleCode");
        }
    }
}
#endif