using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Configuración de datos puros de un área (sin geometría).
/// Se edita como asset ScriptableObject para que cualquiera pueda crear nuevas áreas sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "AreaConfig_", menuName = "Quality Clinic/Area Config", order = 10)]
public class AreaConfigSO : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("Clave única en MAYÚSCULAS sin espacios. Ej: ATHONDA, VCTL4, TEST1")]
    public string areaKey = "ATHONDA";

    [Tooltip("Nombre mostrado en UI. Ej: \"AT Honda\"")]
    public string displayName = "AT Honda";

    [Header("KPIs (0 - 100)")]
    [Range(0f, 100f)] public float delivery = 100f;
    [Range(0f, 100f)] public float quality = 100f;
    [Range(0f, 100f)] public float parts = 100f;
    [Range(0f, 100f)] public float processManufacturing = 100f;
    [Range(0f, 100f)] public float trainingDNA = 100f;
    [Range(0f, 100f)] public float mtto = 100f;

    [Header("Resultado (auto)")]
    [Tooltip("Promedio simple de los 6 KPIs. Se recalcula automáticamente.")]
    [SerializeField, Range(0f, 100f)]
    private float overallResult = 100f;

    /// <summary>Promedio actual (solo lectura pública).</summary>
    public float OverallResult => overallResult;

    /// <summary>Recalcula y normaliza campos cada vez que se edita el asset.</summary>
    private void OnValidate()
    {
        // Normalizar clave
        if (!string.IsNullOrEmpty(areaKey))
        {
            areaKey = areaKey.Trim().Replace(" ", "").Replace("_", "").ToUpperInvariant();
        }

        ClampAll();
        RecalculateOverall();
        ValidateDuplicateKeysInProject();
    }

    /// <summary>Limita KPIs al rango 0..100.</summary>
    public void ClampAll()
    {
        delivery = Mathf.Clamp(delivery, 0f, 100f);
        quality = Mathf.Clamp(quality, 0f, 100f);
        parts = Mathf.Clamp(parts, 0f, 100f);
        processManufacturing = Mathf.Clamp(processManufacturing, 0f, 100f);
        trainingDNA = Mathf.Clamp(trainingDNA, 0f, 100f);
        mtto = Mathf.Clamp(mtto, 0f, 100f);
    }

    /// <summary>Promedio simple de los 6 KPIs (0..100).</summary>
    public void RecalculateOverall()
    {
        overallResult = Mathf.Clamp(
            (delivery + quality + parts + processManufacturing + trainingDNA + mtto) / 6f,
            0f, 100f
        );
    }

#if UNITY_EDITOR
    /// <summary>Chequeo suave para evitar duplicados de areaKey entre assets.</summary>
    private void ValidateDuplicateKeysInProject()
    {
        // Solo en editor: busca otros AreaConfigSO con la misma key
        string thisPath = AssetDatabase.GetAssetPath(this);
        var guids = AssetDatabase.FindAssets("t:AreaConfigSO");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == thisPath) continue;
            var other = AssetDatabase.LoadAssetAtPath<AreaConfigSO>(path);
            if (other != null && other.areaKey == areaKey && !string.IsNullOrEmpty(areaKey))
            {
                Debug.LogWarning($"[AreaConfigSO] Clave duplicada: {areaKey} en {path}. Debe ser única.");
            }
        }
    }
#endif

    /// <summary>
    /// Convierte a la estructura AreaManager.AreaData para la futura integración.
    /// (No se usa todavía hasta que conectemos el AreaManager.)
    /// </summary>
    public AreaManager.AreaData ToAreaData()
    {
        var data = new AreaManager.AreaData
        {
            areaName = areaKey,
            displayName = string.IsNullOrWhiteSpace(displayName) ? areaKey : displayName,
            delivery = delivery,
            quality = quality,
            parts = parts,
            processManufacturing = processManufacturing,
            trainingDNA = trainingDNA,
            mtto = mtto,
            overallResult = overallResult,
            status = "", // AreaManager lo puede calcular si lo desea
            statusColor = AppleTheme.Status(overallResult)
        };
        return data;
    }
}
