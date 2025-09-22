using System.Collections.Generic;
using UnityEngine;

public class ManualLabelsManager : MonoBehaviour
{
    private static ManualLabelsManager _instance;
    private readonly HashSet<ManualAreaLabel> _labels = new HashSet<ManualAreaLabel>();
    private bool _currentTopDownMode = false;

    [Header("Debug")]
    public bool enableDebug = true;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebug) Debug.Log("[ManualLabelsManager] Instancia creada");
    }

    public static void Register(ManualAreaLabel label)
    {
        if (_instance == null)
        {
            var go = new GameObject("ManualLabelsManager");
            _instance = go.AddComponent<ManualLabelsManager>();
            DontDestroyOnLoad(go);
            Debug.Log("[ManualLabelsManager] Instancia creada automáticamente");
        }

        _instance._labels.Add(label);

        // Aplicar estado actual inmediatamente
        label.SetTopDownVisibility(_instance._currentTopDownMode);

        if (_instance.enableDebug)
            Debug.Log($"[ManualLabelsManager] Label registrado: {label.name}. Total: {_instance._labels.Count}");
    }

    public static void Unregister(ManualAreaLabel label)
    {
        if (_instance != null)
        {
            _instance._labels.Remove(label);
            if (_instance.enableDebug)
                Debug.Log($"[ManualLabelsManager] Label desregistrado: {label.name}. Total: {_instance._labels.Count}");
        }
    }

    public void SetTopDownMode(bool enabled)
    {
        _currentTopDownMode = enabled;

        if (enableDebug)
            Debug.Log($"[ManualLabelsManager] Modo top-down: {enabled}. Aplicando a {_labels.Count} labels.");

        // Limpiar referencias nulas primero
        _labels.RemoveWhere(l => l == null);

        foreach (var label in _labels)
        {
            if (label != null)
            {
                label.SetTopDownVisibility(enabled);
            }
        }
    }

    public void ForceRefreshAll()
    {
        if (enableDebug)
            Debug.Log($"[ManualLabelsManager] Refrescando {_labels.Count} labels.");

        // Limpiar referencias nulas
        _labels.RemoveWhere(l => l == null);

        foreach (var label in _labels)
        {
            if (label != null)
            {
                label.ForceRefresh();
            }
        }
    }

    public bool GetCurrentTopDownMode() => _currentTopDownMode;
    public int GetLabelCount() => _labels.Count;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            Debug.Log("=== MANUAL LABELS DEBUG ===");
            Debug.Log($"Modo Top-Down actual: {_currentTopDownMode}");
            Debug.Log($"Labels registrados: {_labels.Count}");

            foreach (var label in _labels)
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
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _labels.Clear();
            _instance = null;
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsOnLoad()
    {
        _instance = null;
    }
#endif
}