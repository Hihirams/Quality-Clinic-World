using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton que gestiona la visibilidad de todos los ManualAreaLabel según el modo de cámara.
/// Permite alternar entre vista libre (Sims) y top-down sin perder referencias.
/// </summary>
public class ManualLabelsManager : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Debug")]
    [Tooltip("Habilita logs informativos en consola (usa QCLog cuando QC_VERBOSE está definido)")]
    public bool enableDebug = true;
    
    #endregion

    #region Private State
    
    /// <summary>Instancia singleton del manager.</summary>
    private static ManualLabelsManager instance;
    
    /// <summary>Colección de todos los labels registrados. HashSet previene duplicados.</summary>
    private readonly HashSet<ManualAreaLabel> labels = new HashSet<ManualAreaLabel>();
    
    /// <summary>Estado actual del modo top-down. Se aplica a nuevos labels al registrarse.</summary>
    private bool currentTopDownMode = false;
    
    #endregion

    #region Unity Callbacks
    
    void Awake()
    {
        // Patrón singleton: destruir duplicados
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebug)
        {
            QCLog.Info("[ManualLabelsManager] Instancia singleton creada");
        }
    }

    void Update()
    {
        // Tecla de debug F10 para inspeccionar estado en runtime
        if (Input.GetKeyDown(KeyCode.F10))
        {
            DebugPrintAllLabels();
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            labels.Clear();
            instance = null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Resetea statics al salir de Play Mode en Editor (evita state leaks entre sesiones).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsOnLoad()
    {
        instance = null;
    }
#endif

    #endregion

    #region Public API

    /// <summary>
    /// Registra un label para ser gestionado. Si no existe instancia, la crea automáticamente.
    /// El label recibe inmediatamente el estado actual de top-down mode.
    /// </summary>
    /// <param name="label">Label a registrar</param>
    public static void Register(ManualAreaLabel label)
    {
        if (label == null)
        {
            Debug.LogError("[ManualLabelsManager] Intento de registrar label nulo");
            return;
        }

        // Auto-crear instancia si no existe (lazy initialization)
        if (instance == null)
        {
            CreateInstance();
        }

        instance.labels.Add(label);

        // Aplicar estado actual inmediatamente para consistencia
        label.SetTopDownVisibility(instance.currentTopDownMode);

        if (instance.enableDebug)
        {
            QCLog.Info($"[ManualLabelsManager] Label registrado: {label.name}. Total: {instance.labels.Count}");
        }
    }

    /// <summary>
    /// Elimina un label del sistema de gestión.
    /// </summary>
    /// <param name="label">Label a desregistrar</param>
    public static void Unregister(ManualAreaLabel label)
    {
        if (instance != null && label != null)
        {
            instance.labels.Remove(label);
            
            if (instance.enableDebug)
            {
                QCLog.Info($"[ManualLabelsManager] Label desregistrado: {label.name}. Total: {instance.labels.Count}");
            }
        }
    }

    /// <summary>
    /// Cambia el modo de visualización de todos los labels registrados.
    /// Top-down: labels visibles en vista cenital.
    /// Free camera: labels ocultos o ajustados según implementación de ManualAreaLabel.
    /// </summary>
    /// <param name="enabled">True para modo top-down, false para cámara libre</param>
    public void SetTopDownMode(bool enabled)
    {
        currentTopDownMode = enabled;

        if (enableDebug)
        {
            QCLog.Info($"[ManualLabelsManager] Modo top-down: {enabled}. Aplicando a {labels.Count} labels.");
        }

        // Limpieza preventiva de referencias destruidas
        labels.RemoveWhere(l => l == null);

        // Aplicar estado a todos los labels válidos
        foreach (var label in labels)
        {
            if (label != null)
            {
                label.SetTopDownVisibility(enabled);
            }
        }
    }

    /// <summary>
    /// Fuerza un refresh de todos los labels (útil tras cambios de configuración o escena).
    /// </summary>
    public void ForceRefreshAll()
    {
        if (enableDebug)
        {
            QCLog.Info($"[ManualLabelsManager] Refrescando {labels.Count} labels.");
        }

        // Limpiar referencias nulas antes del refresh
        labels.RemoveWhere(l => l == null);

        foreach (var label in labels)
        {
            if (label != null)
            {
                label.ForceRefresh();
            }
        }
    }

    /// <summary>
    /// Obtiene el estado actual del modo top-down.
    /// </summary>
    /// <returns>True si está en modo top-down, false si en cámara libre</returns>
    public bool GetCurrentTopDownMode() => currentTopDownMode;

    /// <summary>
    /// Retorna el número de labels actualmente registrados (tras limpieza de nulos).
    /// </summary>
    /// <returns>Cantidad de labels válidos</returns>
    public int GetLabelCount() => labels.Count;

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Crea la instancia singleton de forma lazy (solo cuando se necesita).
    /// </summary>
    private static void CreateInstance()
    {
        var go = new GameObject("ManualLabelsManager");
        instance = go.AddComponent<ManualLabelsManager>();
        DontDestroyOnLoad(go);
        
        QCLog.Info("[ManualLabelsManager] Instancia creada automáticamente (lazy init)");
    }

    #endregion

    #region Debug

    /// <summary>
    /// Imprime información detallada de todos los labels registrados.
    /// Útil para debugging en runtime (activar con F10).
    /// </summary>
    private void DebugPrintAllLabels()
    {
        Debug.Log("=== MANUAL LABELS DEBUG ===");
        Debug.Log($"Modo Top-Down actual: {currentTopDownMode}");
        Debug.Log($"Labels registrados: {labels.Count}");

        foreach (var label in labels)
        {
            if (label != null)
            {
                var canvas = label.GetComponentInChildren<Canvas>();
                var camera = canvas != null ? canvas.worldCamera : null;

                Debug.Log($"- {label.name}:");
                Debug.Log($"  Área: {label.areaKey}");
                Debug.Log($"  Activo: {label.gameObject.activeInHierarchy}");
                Debug.Log($"  Posición: {label.transform.position}");
                Debug.Log($"  Canvas: {(canvas != null ? canvas.renderMode.ToString() : "NULL")}");
                Debug.Log($"  Cámara Canvas: {(camera != null ? camera.name : "NULL")}");
            }
        }
    }

    #endregion
}