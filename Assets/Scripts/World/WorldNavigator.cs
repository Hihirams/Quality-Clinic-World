using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorldNavigator : MonoBehaviour
{
    public SceneLoader sceneLoader;       // arrastra AppRoot aquí en el inspector
    public string plantSceneName = "10_Plant_OP2";  // nombre exacto de tu escena de mapa

    public enum Level { Continent, Country, State, Plant }

    [Header("Refs")]
    public Transform earth;               // Earth
    public GlobePicker picker;            // WorldBrain(GlobePicker)
    public GlobeOrbitCamera orbitCam;     // MainCamera (GlobeOrbitCamera)
    public Button backButton;             // HUD/BackButton
    public TextMeshProUGUI breadcrumb;    // HUD/Breadcrumb

    [Header("Zoom")]
    public float focusDistance = 6.5f;

    [Serializable]
    public class Node { public string id, name, parentId; public float lat, lon; public Vector3 dirLocal; }
    [Header("Demo data")]
    public List<Node> continents = new();
    public List<Node> countries = new();
    public List<Node> states = new();
    public List<Node> plants = new();

    Level level = Level.Continent;
    string selContinent, selCountry, selState, selPlant;

    void Awake()
    {
        if (!earth) earth = GameObject.Find("Earth")?.transform;
        if (!picker) picker = FindObjectOfType<GlobePicker>();
        if (!orbitCam) orbitCam = FindObjectOfType<GlobeOrbitCamera>();

        // demo mínima
        if (continents.Count == 0) continents = new(){
            new Node{ id="NA", name="North America", lat=40,  lon=-100 },
            new Node{ id="EU", name="Europe",        lat=50,  lon=10   },
            new Node{ id="AS", name="Asia",          lat=30,  lon=90   },
            new Node{ id="AF", name="Africa",        lat=5,   lon=20   },
            new Node{ id="OC", name="Oceania",       lat=-25, lon=133  },
            new Node{ id="SA", name="South America", lat=-15, lon=-60  },
        };
        if (countries.Count == 0) countries = new(){
            new Node{ id="MX", name="México",         lat=23.6f, lon=-102.5f, parentId="NA" },
            new Node{ id="US", name="Estados Unidos", lat=39f,   lon=-98f,    parentId="NA" },
            new Node{ id="CA", name="Canadá",         lat=56f,   lon=-106f,   parentId="NA" },
        };
        if (states.Count == 0) states = new(){
            new Node{ id="NL",  name="Nuevo León", lat=25.67f, lon=-100.31f, parentId="MX" },
            new Node{ id="QRO", name="Querétaro",  lat=20.59f, lon=-100.39f, parentId="MX" },
        };
        if (plants.Count == 0) plants = new(){
            new Node{ id="Apodaca", name="Apodaca", lat=25.78f, lon=-100.23f, parentId="NL" },
            new Node{ id="Guadalupe",  name="Guadalupe",  lat=25.76f, lon=-100.26f, parentId="NL" },
        };

        Precalc(continents); Precalc(countries); Precalc(states); Precalc(plants);

        // Picker escucha nivel actual
        picker.OnRegionSelected.AddListener(OnContinentPicked);
        RebuildHotspotsFrom(continents, null);
        BuildMarkersFrom(continents, null);

        if (backButton) backButton.onClick.AddListener(GoBack);
        UpdateUI();
    }

    // ---------- Picks ----------
    void OnContinentPicked(string name)
    {
        if (level != Level.Continent) return;
        var best = BestByName(continents, name); if (best == null) return;

        selContinent = best.id; Focus(best);
        level = Level.Country;
        picker.OnRegionSelected.RemoveAllListeners();
        picker.OnRegionSelected.AddListener(OnCountryPicked);
        RebuildHotspotsFrom(countries, selContinent);
        BuildMarkersFrom(countries, selContinent);
        UpdateUI();

        // detener giro automático
        var spinner = earth.GetComponent<GlobeSpinner>();
        if (spinner) spinner.enabled = false;
    }

    void OnCountryPicked(string name)
    {
        if (level != Level.Country) return;
        var best = BestByName(FilterByParent(countries, selContinent), name); if (best == null) return;

        selCountry = best.id; Focus(best);
        level = Level.State;
        picker.OnRegionSelected.RemoveAllListeners();
        picker.OnRegionSelected.AddListener(OnStatePicked);
        RebuildHotspotsFrom(states, selCountry);
        BuildMarkersFrom(states, selCountry);
        UpdateUI();
    }

    void OnStatePicked(string name)
    {
        if (level != Level.State) return;
        var best = BestByName(FilterByParent(states, selCountry), name); if (best == null) return;

        selState = best.id; Focus(best);
        level = Level.Plant;
        picker.OnRegionSelected.RemoveAllListeners();
        picker.OnRegionSelected.AddListener(OnPlantPicked);
        RebuildHotspotsFrom(plants, selState);
        BuildMarkersFrom(plants, selState);
        UpdateUI();
    }

    void OnPlantPicked(string name)
    {
        if (level != Level.Plant) return;
        var best = BestByName(FilterByParent(plants, selState), name); 
        if (best == null)
        {
            Debug.LogError("[WorldNavigator] No se encontró la planta: " + name);
            return;
        }

        selPlant = best.id; 
        Focus(best);
        UpdateUI();
        
        Debug.Log($"[WorldNavigator] ✓ Planta seleccionada: {best.name} ({best.id})");
        Debug.Log($"[WorldNavigator] Intentando cargar escena: {plantSceneName}");
        
        // CARGAR ESCENA PARA CUALQUIER PLANTA
        if (sceneLoader != null)
        {
            Debug.Log("[WorldNavigator] ✓ SceneLoader encontrado, iniciando carga...");
            sceneLoader.LoadAdditiveAndSwap(plantSceneName, "01_Planet");
        }
        else
        {
            Debug.LogError("[WorldNavigator] ✗ ERROR: SceneLoader es NULL. Asígnalo en el Inspector!");
        }
    }

    // ---------- Back ----------
    void GoBack()
    {
        switch (level)
        {
            case Level.Country:
                level = Level.Continent;
                selContinent = null;
                picker.OnRegionSelected.RemoveAllListeners();
                picker.OnRegionSelected.AddListener(OnContinentPicked);
                RefreshLevelMarkers();
                break;

            case Level.State:
                level = Level.Country;
                selState = null;
                picker.OnRegionSelected.RemoveAllListeners();
                picker.OnRegionSelected.AddListener(OnCountryPicked);
                RefreshLevelMarkers();
                FocusById(countries, selCountry);
                break;

            case Level.Plant:
                level = Level.State;
                selPlant = null;
                picker.OnRegionSelected.RemoveAllListeners();
                picker.OnRegionSelected.AddListener(OnStatePicked);
                RefreshLevelMarkers();
                FocusById(states, selState);
                break;
        }
        UpdateUI();
    }

    // ---------- Helpers ----------
    void Focus(Node n)
    {
        if (n == null || !earth || !orbitCam) return;
        Vector3 dirWorld = earth.TransformDirection(n.dirLocal);
        orbitCam.FocusTowards(dirWorld, focusDistance, true);
    }
    void FocusById(List<Node> list, string id)
    {
        var n = list.FirstOrDefault(x => x.id == id);
        if (n != null) Focus(n);
    }

    void RebuildHotspotsFrom(List<Node> source, string parentId)
    {
        picker.regions.Clear();
        foreach (var n in source)
            if (string.IsNullOrEmpty(parentId) || n.parentId == parentId)
                picker.regions.Add(new GlobePicker.Region { name = n.name, lat = n.lat, lon = n.lon, dirLocal = n.dirLocal });
    }

    List<Node> FilterByParent(List<Node> list, string parentId) => list.Where(n => n.parentId == parentId).ToList();
    Node BestByName(List<Node> list, string name)
    {
        name = name.ToLower();
        return list.OrderByDescending(n => -Levenshtein(n.name.ToLower(), name)).FirstOrDefault();
    }

    void Precalc(List<Node> list)
    {
        foreach (var n in list)
        {
            float lat = n.lat * Mathf.Deg2Rad, lon = n.lon * Mathf.Deg2Rad;
            n.dirLocal = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon)).normalized;
        }
    }

    int Levenshtein(string a, string b)
    {
        int n = a.Length, m = b.Length; var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }

    void UpdateUI()
    {
        if (backButton) backButton.gameObject.SetActive(level != Level.Continent);
        if (breadcrumb)
        {
            string c = selContinent != null ? continents.FirstOrDefault(x => x.id == selContinent)?.name : "Continente";
            string p = selCountry != null ? countries.FirstOrDefault(x => x.id == selCountry)?.name : "País";
            string s = selState != null ? states.FirstOrDefault(x => x.id == selState)?.name : "Estado";
            string l = selPlant != null ? plants.FirstOrDefault(x => x.id == selPlant)?.name : "Planta";
            breadcrumb.text = $"{c} > {p} > {s} > {l}   ({level})";
        }
    }

    [Header("Markers")]
    public GameObject markerPrefab;
    public Transform markersRoot;

    void ClearMarkers()
    {
        if (!markersRoot) return;
        for (int i = markersRoot.childCount - 1; i >= 0; i--)
            Destroy(markersRoot.GetChild(i).gameObject);
    }

    [Header("Marker Style")]
    public Color continentColor = new Color(0.2f, 0.6f, 1f, 1f);   // azul
    public Color countryColor   = new Color(0.0f, 0.8f, 0.4f, 1f); // verde
    public Color stateColor     = new Color(1f,   0.65f, 0f, 1f);  // naranja
    public Color plantColor     = new Color(0.9f, 0.3f, 0.3f, 1f); // rojo

void BuildMarkersFrom(List<Node> source, string parentId)
{
    ClearMarkers();
    if (!markerPrefab || !earth || !orbitCam) return;

    Color c = level == Level.Continent ? continentColor :
              level == Level.Country   ? countryColor   :
              level == Level.State     ? stateColor     : plantColor;

    foreach (var n in source)
    {
        if (!string.IsNullOrEmpty(parentId) && n.parentId != parentId) continue;

        var go = Instantiate(markerPrefab, markersRoot, false);

        var hs = go.GetComponent<HotspotBillboard>();
        if (hs)
        {
            hs.altitude = 0.1f;
            hs.Init(earth, Camera.main, n.dirLocal, n.name);

            // color especial para NA si quieres
            Color markerColor = (n.id == "NA") ? Color.yellow : c;
            hs.SetVisual(markerColor, 2.2f);

            // por si el label no está asignado en el prefab
            if (!hs.label) hs.label = go.GetComponentInChildren<TextMeshPro>();
            if (hs.label)  hs.label.text = n.name;
        }
    }
}


    

    void RefreshLevelMarkers()
    {

        
        switch (level)
        {
            case Level.Continent:
                RebuildHotspotsFrom(continents, null);
                BuildMarkersFrom(continents, null);
                break;

            case Level.Country:
                RebuildHotspotsFrom(countries, selContinent);
                BuildMarkersFrom(countries, selContinent);
                break;

            case Level.State:
                RebuildHotspotsFrom(states, selCountry);
                BuildMarkersFrom(states, selCountry);
                break;

            case Level.Plant:
                RebuildHotspotsFrom(plants, selState);
                BuildMarkersFrom(plants, selState);
                break;
        }
    }
}