using System.Linq;
using UnityEngine;

public class GlobeFocus : MonoBehaviour
{
    [Header("Refs")]
    public Transform earth;               // arrastra Earth
    public GlobePicker picker;            // arrastra WorldBrain(GlobePicker)
    public GlobeOrbitCamera orbitCam;     // arrastra Main Camera (GlobeOrbitCamera)

    [Header("Enfoque")]
    public float focusDistance = 6.5f;    // qué tan cerca de la superficie queda la cámara
    public float lerp = 6f;               // suavidad de movimiento/rotación

    Vector3 targetPos;
    Quaternion targetRot;
    bool hasTarget;

    void Awake()
    {
        if (!earth) earth = GameObject.Find("Earth")?.transform;
        if (!picker) picker = FindObjectOfType<GlobePicker>();
        if (!orbitCam) orbitCam = FindObjectOfType<GlobeOrbitCamera>();
    }

    void OnEnable()
    {
        if (picker) picker.OnRegionSelected.AddListener(FocusByName);
    }
    void OnDisable()
    {
        if (picker) picker.OnRegionSelected.RemoveListener(FocusByName);
    }

    void FocusByName(string regionName)
    {
        if (!picker || !earth || !orbitCam) return;

        var r = picker.regions.FirstOrDefault(x => x.name == regionName);
        if (r == null) return;

        // dirección en MUNDO desde el centro de la Tierra hacia el hotspot
        Vector3 dirWorld = earth.TransformDirection(r.dirLocal);

        // pide al orbit que enfoque con suavizado y distancia deseada
        orbitCam.FocusTowards(dirWorld, focusDistance, smoothFocus: true);
    }

}
