using System.Collections.Generic;
using UnityEngine;

public class ManualLabelsManager : MonoBehaviour
{
    private static ManualLabelsManager _instance;
    private readonly HashSet<ManualAreaLabel> _labels = new HashSet<ManualAreaLabel>();

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    public static void Register(ManualAreaLabel l)
    {
        if (_instance == null) _instance = new GameObject("ManualLabelsManager").AddComponent<ManualLabelsManager>();
        _instance._labels.Add(l);
    }

    public static void Unregister(ManualAreaLabel l)
    {
        if (_instance != null) _instance._labels.Remove(l);
    }

    public void SetTopDownMode(bool enabled)
    {
        foreach (var l in _labels)
            if (l) l.SetTopDownVisibility(enabled);
    }

    public void ForceRefreshAll()
    {
        foreach (var l in _labels)
            if (l) l.ForceRefresh();
    }
}
