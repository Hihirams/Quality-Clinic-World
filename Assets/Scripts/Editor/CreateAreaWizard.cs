#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools → Quality Clinic → Create New Area
/// Crea un ScriptableObject de datos (AreaConfigSO) para que alguien sin código
/// defina una nueva área con sus KPIs.
/// </summary>
public class CreateAreaWizard : EditorWindow
{
    private string areaKey = "TEST";
    private string displayName = "Area Test";

    private float delivery = 95f;
    private float quality = 95f;
    private float parts = 95f;
    private float processManufacturing = 95f;
    private float trainingDNA = 95f;
    private float mtto = 95f;

    private const string DefaultFolder = "Assets/Resources/Areas/";

    [MenuItem("Tools/Quality Clinic/Create New Area")]
    public static void ShowWindow()
    {
        GetWindow<CreateAreaWizard>("Create New Area");
    }

    private void OnGUI()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Identidad", EditorStyles.boldLabel);
        areaKey     = EditorGUILayout.TextField("Area Key (única)", areaKey);
        displayName = EditorGUILayout.TextField("Display Name",    displayName);

        GUILayout.Space(6);
        EditorGUILayout.LabelField("KPIs (0–100)", EditorStyles.boldLabel);
        delivery            = EditorGUILayout.Slider("Delivery",            delivery, 0, 100);
        quality             = EditorGUILayout.Slider("Quality",             quality, 0, 100);
        parts               = EditorGUILayout.Slider("Parts",               parts, 0, 100);
        processManufacturing= EditorGUILayout.Slider("Process Mfg",         processManufacturing, 0, 100);
        trainingDNA         = EditorGUILayout.Slider("Training DNA",        trainingDNA, 0, 100);
        mtto                = EditorGUILayout.Slider("MTTO",                mtto, 0, 100);

        GUILayout.Space(10);
        if (GUILayout.Button("Create ScriptableObject", GUILayout.Height(32)))
        {
            CreateSO();
        }

        EditorGUILayout.HelpBox(
            "1) Llena nombre y KPIs.\n" +
            "2) Clic en 'Create ScriptableObject'.\n" +
            "3) Arrastra el asset creado al AreaBootstrapper del área en la escena.\n" +
            "4) Presiona 'Configure Now'.",
            MessageType.Info
        );
    }

    private void CreateSO()
    {
        if (!Directory.Exists(DefaultFolder))
            Directory.CreateDirectory(DefaultFolder);

        // normalizar clave
        string key = (areaKey ?? "NEW").Trim().Replace(" ", "").Replace("_", "").ToUpperInvariant();
        string path = $"{DefaultFolder}AreaConfig_{key}.asset";

        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                $"Ya existe un asset con la clave {key}.\n¿Sobrescribir?", "Sí", "Cancelar"))
                return;
        }

        var so = ScriptableObject.CreateInstance<AreaConfigSO>();
        so.areaKey = key;
        so.displayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
        so.delivery = delivery;
        so.quality = quality;
        so.parts = parts;
        so.processManufacturing = processManufacturing;
        so.trainingDNA = trainingDNA;
        so.mtto = mtto;
        so.ClampAll();
        so.RecalculateOverall();

        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = so;

        Debug.Log($"[CreateAreaWizard] Creado: {path}");
    }
}
#endif
