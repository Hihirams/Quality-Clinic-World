using System.Collections.Generic;
using UnityEngine;

public class ManualLabelsManager : MonoBehaviour
{
    private static ManualLabelsManager _instance;
    private readonly HashSet<ManualAreaLabel> _labels = new HashSet<ManualAreaLabel>();
    private bool _currentTopDownMode = false;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Register(ManualAreaLabel label)
    {
        if (_instance == null)
        {
            var go = new GameObject("ManualLabelsManager");
            _instance = go.AddComponent<ManualLabelsManager>();
        }

        _instance._labels.Add(label); // ✅ CORREGIDO: era *instance.*labels

        // Si ya tenemos un estado establecido, aplicarlo inmediatamente
        label.SetTopDownVisibility(_instance._currentTopDownMode);

        Debug.Log($"ManualAreaLabel registrado: {label.name}. Total labels: {_instance._labels.Count}");
    }

    public static void Unregister(ManualAreaLabel label)
    {
        if (_instance != null)
        {
            _instance._labels.Remove(label); // ✅ CORREGIDO: era *instance.*labels
            Debug.Log($"ManualAreaLabel desregistrado: {label.name}. Total labels: {_instance._labels.Count}");
        }
    }

    public void SetTopDownMode(bool enabled)
    {
        _currentTopDownMode = enabled;

        Debug.Log($"ManualLabelsManager: Cambiando modo top-down a {enabled}. Aplicando a {_labels.Count} labels.");

        foreach (var label in _labels)
        {
            if (label != null)
            {
                label.SetTopDownVisibility(enabled);
            }
        }

        // Limpiar referencias nulas
        _labels.RemoveWhere(l => l == null);
    }

    public void ForceRefreshAll()
    {
        Debug.Log($"ManualLabelsManager: Refrescando {_labels.Count} labels.");

        foreach (var label in _labels)
        {
            if (label != null)
            {
                label.ForceRefresh();
            }
        }

        // Limpiar referencias nulas
        _labels.RemoveWhere(l => l == null);
    }

    public bool GetCurrentTopDownMode()
    {
        return _currentTopDownMode;
    }

    public int GetLabelCount()
    {
        return _labels.Count;
    }

    // Método para debug
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10) && Application.isEditor)
        {
            Debug.Log($"=== MANUAL LABELS DEBUG ===");
            Debug.Log($"Modo Top-Down actual: {_currentTopDownMode}");
            Debug.Log($"Labels registrados: {_labels.Count}");

            foreach (var label in _labels)
            {
                if (label != null)
                {
                    Debug.Log($"- {label.name} | Área: {label.areaKey} | Activo: {label.gameObject.activeInHierarchy}");
                }
            }
        }
    }
}