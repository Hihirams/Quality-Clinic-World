using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class GlobePicker : MonoBehaviour
{
    [Serializable] public class RegionSelectEvent : UnityEvent<string>{}

    [Header("Refs")]
    public Transform earth;                 // arrastra Earth
    public SphereCollider earthCollider;    // arrastra SphereCollider de Earth

    [Header("Detección")]
    [Range(1f,45f)] public float selectAngleThreshold = 18f;

    [Serializable]
    public class Region
    {
        public string name;
        public float lat; // -90..90
        public float lon; // -180..180
        [HideInInspector] public Vector3 dirLocal;
    }
    public List<Region> regions = new();

    public RegionSelectEvent OnRegionSelected;

    bool OverUI() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();

    void Awake()
    {
        if (!earth) earth = GameObject.Find("Earth")?.transform;
        if (!earthCollider && earth) earthCollider = earth.GetComponent<SphereCollider>();

        if (regions.Count == 0) // demo rápido
        {
            regions = new List<Region>{
                new Region{ name="N. America", lat= 40, lon=-100 },
                new Region{ name="S. America", lat=-15, lon= -60 },
                new Region{ name="Europe",     lat= 50, lon=  10 },
                new Region{ name="Africa",     lat=  5, lon=  20 },
                new Region{ name="Asia",       lat= 30, lon=  90 },
                new Region{ name="Oceania",    lat=-25, lon= 133 },
            };
        }

        foreach (var r in regions) r.dirLocal = LatLonToLocalDir(r.lat, r.lon);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !OverUI())
            TryPick();
    }

    void TryPick()
    {
        if (!earth || !earthCollider) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!earthCollider.Raycast(ray, out var hit, 5000f)) return;

        Vector3 local = earth.InverseTransformPoint(hit.point).normalized;

        float bestAngle = 999f;
        string best = null;
        foreach (var r in regions)
        {
            float a = Vector3.Angle(local, r.dirLocal);
            if (a < bestAngle) { bestAngle = a; best = r.name; }
        }

        if (best != null && bestAngle <= selectAngleThreshold)
        {
            Debug.Log($"[Globe] Región: {best}");
            OnRegionSelected?.Invoke(best);
        }
    }

    Vector3 LatLonToLocalDir(float latDeg, float lonDeg)
    {
        float lat = latDeg * Mathf.Deg2Rad;
        float lon = lonDeg * Mathf.Deg2Rad;
        float x = Mathf.Cos(lat) * Mathf.Sin(lon);
        float y = Mathf.Sin(lat);
        float z = Mathf.Cos(lat) * Mathf.Cos(lon);
        return new Vector3(x,y,z).normalized;
    }
}
