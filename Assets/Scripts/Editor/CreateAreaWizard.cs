#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using TMPro;
using UnityEngine.UI;   

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

    private void BuildAutoLabel(GameObject areaRoot, string areaKey, AreaConfigSO cfg)
{
    // 1) Crear raíz del Label
    var labelGO = new GameObject("Label");
    labelGO.transform.SetParent(areaRoot.transform, false);
    labelGO.transform.localPosition = Vector3.zero;

    // 2) Canvas WorldSpace para que se vea en TopDown (y el Manager lo enciende/apaga)
    var canvas = labelGO.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.WorldSpace;
    canvas.sortingOrder = 5000; // por encima de overlays
    var gr = labelGO.AddComponent<GraphicRaycaster>(); gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;

    var rt = labelGO.GetComponent<RectTransform>();
    rt.sizeDelta = new Vector2(1.5f, 1.0f);
    rt.localScale = Vector3.one * 0.02f; // tamaño cómodo en tu escena

    // 3) Crear los textos hijos (TMP)
    // NameText
    var nameGO = new GameObject("NameText", typeof(TextMeshProUGUI));
    nameGO.transform.SetParent(labelGO.transform, false);
    var nameRT = nameGO.GetComponent<RectTransform>();
    nameRT.anchorMin = nameRT.anchorMax = new Vector2(0.5f, 0.65f);
    nameRT.sizeDelta = new Vector2(1.4f, 0.4f);
    var nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
    nameTMP.text = string.IsNullOrEmpty(cfg.displayName) ? areaKey : cfg.displayName.ToUpperInvariant();
    nameTMP.fontSize = 36;
    nameTMP.alignment = TextAlignmentOptions.Center;

    // PercentText
    var pctGO = new GameObject("PercentText", typeof(TextMeshProUGUI));
    pctGO.transform.SetParent(labelGO.transform, false);
    var pctRT = pctGO.GetComponent<RectTransform>();
    pctRT.anchorMin = pctRT.anchorMax = new Vector2(0.5f, 0.25f);
    pctRT.sizeDelta = new Vector2(1.4f, 0.4f);
    var pctTMP = pctGO.GetComponent<TextMeshProUGUI>();
    var pct = Mathf.Clamp(cfg.OverallResult, 0f, 100f);
    pctTMP.text = $"{pct:F0}%";
    pctTMP.fontSize = 44;
    pctTMP.fontStyle = FontStyles.Bold;
    pctTMP.alignment = TextAlignmentOptions.Center;

    // 4) Componente de control
    var label = labelGO.AddComponent<ManualAreaLabel>();
    label.autoDetectAreaFromHierarchy = true;          // toma la key desde "Area_*"
    label.onlyShowInStaticTopDown = true;              // visible solo en TopDown
    label.whiteWithBlackOutlineInStatic = true;        // estilo legible en mapa
    label.canvasSortingOrder = 5000;
    label.nameText = nameTMP;
    label.percentText = pctTMP;

    // Fallbacks desde el SO (por si aún no existen datos en AreaManager)
    label.GetType().GetField("fallbackDisplayName", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        ?.SetValue(label, string.IsNullOrEmpty(cfg.displayName) ? areaKey : cfg.displayName);
    label.GetType().GetField("fallbackOverall", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        ?.SetValue(label, cfg.OverallResult <= 0 ? 81f : cfg.OverallResult);

    // 5) Ajuste de altura y escala cómodos (coinciden con tus labels actuales)
    label.labelHeightY = 0.25f;
    label.canvasBaseScale = 1.0f;
    label.mapModeScale = 1.2f;
}
}
#endif
