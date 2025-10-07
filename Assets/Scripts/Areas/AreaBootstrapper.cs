using System.Collections.Generic;
using UnityEngine;
using AreaData = AreaManager.AreaData;
using TMPro;            // <— ¡siempre!
using UnityEngine.UI;   // <— ¡siempre!
#if UNITY_EDITOR

#endif

/// <summary>
/// Configurador “one–click” de un área existente en la escena.
/// No toca geometría: sólo detecta cubos/renderers del área, crea colliders precisos,
/// prepara la tarjeta (AreaCard), el label (ManualAreaLabel) y registra el área en AreaManager.
/// </summary>
[DisallowMultipleComponent]
public class AreaBootstrapper : MonoBehaviour
{
    [Header("Datos")]
    [Tooltip("Asset con los KPIs y el nombre a mostrar")]
    public AreaConfigSO config;

    [Header("Detección de Geometría")]
    [Tooltip("Si está activo, busca automáticamente MeshRenderers/MeshFilters bajo este objeto")]
    public bool autoDetectChildCubes = true;

    [Tooltip("Capa que usa AreaManager para raycast de clicks (debe existir)")]
    public string areasLayerName = "Areas";

    [Header("Colliders")]
    [Tooltip("Crea/actualiza un BoxCollider preciso por cada MeshFilter hijo")]
    public bool perChildBoxColliders = true;

    [Tooltip("Crea/actualiza un BoxCollider grande en el padre (útil para clicks)")]
    public bool parentEnclosingCollider = true;

    [Header("AreaCard")]
    [Tooltip("Altura (Y) relativa para colocar la tarjeta sobre el área")]
    public float cardHeightOffset = 1.6f;

    [Tooltip("Si existe un AreaCard, lo reutiliza; si no, crea uno")]
    public bool ensureAreaCard = true;

    [Header("Label (ManualAreaLabel)")]
    [Tooltip("Crea o reutiliza el label Manual (Canvas WorldSpace + Name/Percent)")]
    public bool ensureManualLabel = true;

    [Tooltip("Nombre del GameObject del label que se creará/reutilizará")]
    public string labelObjectPrefix = "Label_";

    [Tooltip("Altura Y del Canvas/Label")]
    public float labelHeightY = 0.25f;

    [Tooltip("Orden de sorting del Canvas del Label")]
    public int labelSortingOrder = 5000;

    // ---- cache interno
    private List<MeshFilter> _meshFilters = new List<MeshFilter>();
    private Bounds _worldBounds;

    #region Botón principal (lo llama el editor personalizado)
    /// <summary>
    /// Ejecuta toda la configuración. Se puede llamar en Editor o en Play.
    /// </summary>
    public void ConfigureNow()
    {
        if (config == null)
        {
            Debug.LogError($"[AreaBootstrapper:{name}] No hay AreaConfigSO asignado.");
            return;
        }

        // 1) Normalización de nombre del área (opcional, no obligatorio)
        TryNormalizeAreaGameObjectName();

        // 2) Detectar geometría
        DetectRenderers();

        // 3) Colliders
        SetupColliders();

        // 4) AreaCard
        if (ensureAreaCard) SetupAreaCard();

        // 5) Label manual
        if (ensureManualLabel) SetupManualLabel();

        // 6) Registrar en AreaManager (para que pueda recibir clicks desde su lista)
        RegisterInAreaManager();

        Debug.Log($"[AreaBootstrapper:{name}] Configuración completa ✔");

        gameObject.layer = LayerMask.NameToLayer("Areas");
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = gameObject.layer;
    
    // 7) Inyectar datos al AreaManager + registrar overlay (AUTO-COLOR)
var mgr = FindFirstObjectByType<AreaManager>();
if (mgr != null && config != null)
{
    // 7.1 Datos para esa área desde tu asset
    var data = config.ToAreaData();            // usa overall, displayName, etc.
    // 7.2 Registrar/actualizar el área en el manager
    // (usa el nombre real del GO; el manager normaliza "Area_XXX" a "XXX")
    mgr.AddArea(gameObject, data);             // o AddOrUpdateArea si ese es el nombre en tu clase

    // 7.3 Avisar al painter para crear su overlay y colorearlo
    var painter = FindFirstObjectByType<AreaOverlayPainter>();
    if (painter != null)
    {
        painter.RefreshAreaVisual(gameObject); // aplica color según overall
    }
}


    }
    #endregion

    #region Geometría / Bounds
    private void DetectRenderers()
    {
        _meshFilters.Clear();

        if (autoDetectChildCubes)
        {
            GetComponentsInChildren<MeshFilter>(true, _meshFilters);
        }
        else
        {
            // Si alguien desactiva la autodetección, igual aseguramos al menos un bounds
            var mf = GetComponentInChildren<MeshFilter>(true);
            if (mf != null) _meshFilters.Add(mf);
        }

        if (_meshFilters.Count == 0)
        {
            Debug.LogWarning($"[AreaBootstrapper:{name}] No se encontraron MeshFilters hijos.");
        }

        // Bounds mundial de TODOS los renderers
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            _worldBounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) _worldBounds.Encapsulate(rends[i].bounds);
        }
        else
        {
            _worldBounds = new Bounds(transform.position, Vector3.one * 0.5f);
        }
    }
    #endregion

    #region Colliders
    private void SetupColliders()
    {
        // Capa esperada por AreaManager
        TryEnsureLayer();

        if (perChildBoxColliders)
        {
            foreach (var mf in _meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                var go = mf.gameObject;
                var bc = go.GetComponent<BoxCollider>();
                if (bc == null) bc = go.AddComponent<BoxCollider>();

                // Usar bounds del mesh local (más preciso para cubos y piezas)
                bc.center = mf.sharedMesh.bounds.center;
                bc.size   = mf.sharedMesh.bounds.size;

                go.layer = LayerMask.NameToLayer(areasLayerName);
            }
        }

        if (parentEnclosingCollider)
        {
            var bc = GetComponent<BoxCollider>();
            if (bc == null) bc = gameObject.AddComponent<BoxCollider>();

            // Convertir bounds mundiales a espacio local del padre (aproximación robusta)
            bc.center = transform.InverseTransformPoint(_worldBounds.center);

            var lossy = transform.lossyScale;
            var size = _worldBounds.size;
            bc.size = new Vector3(
                SafeDiv(size.x, Mathf.Abs(lossy.x)),
                SafeDiv(size.y, Mathf.Abs(lossy.y)),
                SafeDiv(size.z, Mathf.Abs(lossy.z))
            );

            gameObject.layer = LayerMask.NameToLayer(areasLayerName);
        }
    }

    private static float SafeDiv(float v, float by) => (by > 1e-5f) ? v / by : v;
    #endregion

    #region AreaCard
private void SetupAreaCard()
{
    // Intentar obtener componente existente en el mismo objeto (no hijos)
    AreaCard card = GetComponent<AreaCard>();
    if (card == null)
    {
        card = gameObject.AddComponent<AreaCard>();
    }

    // Cargar datos desde el config
    card.areaName = "AREA_" + config.areaKey;
    if (card.areaData == null)
        card.areaData = new AreaData();

    card.areaData.areaName      = config.areaKey;
    card.areaData.displayName   = config.displayName;
    card.areaData.overallResult = config.OverallResult;

    // Mantener activa la tarjeta (se autogenera visualmente desde AreaCard)
    card.SetCardActive(true);
}

    #endregion

    #region ManualAreaLabel
private void SetupManualLabel()
{
    // Buscar label existente por convención
    string expectedName = (labelObjectPrefix + config.areaKey);
    Transform labelTr = transform.Find(expectedName);

    if (labelTr == null)
    {
        // Crear label vacío (padre)
        var labelGO = new GameObject(expectedName);
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.position = new Vector3(
            _worldBounds.center.x,
            labelHeightY,
            _worldBounds.center.z
        );
        labelTr = labelGO.transform;

        // --- Canvas WorldSpace + Raycaster ---
        var canvas = labelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = labelSortingOrder;
        labelGO.AddComponent<GraphicRaycaster>();

        var crt = canvas.transform as RectTransform;
        crt.sizeDelta = new Vector2(2f, 2f);
        crt.localScale = Vector3.one * 0.2f;

        // --- NameText (TMP) ---
        var nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(canvas.transform, false);
        var nameTMP = nameObj.GetComponent<TextMeshProUGUI>();
        nameTMP.text = string.IsNullOrWhiteSpace(config.displayName) ? config.areaKey : config.displayName;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = false;
        nameTMP.fontSize = 36f;
        var nrt = nameTMP.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, 0.35f);

        // --- PercentText (TMP) ---
        var pctObj = new GameObject("PercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
        pctObj.transform.SetParent(canvas.transform, false);
        var pctTMP = pctObj.GetComponent<TextMeshProUGUI>();
        pctTMP.text = $"{config.OverallResult:F0}%";
        pctTMP.alignment = TextAlignmentOptions.Center;
        pctTMP.enableWordWrapping = false;
        pctTMP.fontSize = 42f;
        pctTMP.fontStyle = FontStyles.Bold;
        var prt = pctTMP.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0f, -0.35f);

        // --- ManualAreaLabel (control y visibilidad TopDown) ---
        var label = labelGO.AddComponent<ManualAreaLabel>();
        label.autoDetectAreaFromHierarchy = true;
        label.onlyShowInStaticTopDown = true;
        label.whiteWithBlackOutlineInStatic = true;
        label.canvasSortingOrder = labelSortingOrder;
        label.labelHeightY = labelHeightY;
        label.nameText = nameTMP;
        label.percentText = pctTMP;

        // Fallbacks (por si AreaManager aún no tiene datos)
        var t = label.GetType();
        t.GetField("fallbackDisplayName",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            ?.SetValue(label, nameTMP.text);
        t.GetField("fallbackOverall",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            ?.SetValue(label, config.OverallResult);
    }
    else
    {
        // Si ya había Label_*, asegurar componente y enlazar textos si faltan
        var label = labelTr.GetComponent<ManualAreaLabel>() ?? labelTr.gameObject.AddComponent<ManualAreaLabel>();
        label.onlyShowInStaticTopDown = true;
        label.canvasSortingOrder = labelSortingOrder;
        label.labelHeightY = labelHeightY;

        // Enlazar NameText y PercentText si existen
        var tmps = labelTr.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            if (tmp.name.Contains("Name") && label.nameText == null) label.nameText = tmp;
            if (tmp.name.Contains("Percent") && label.percentText == null) label.percentText = tmp;
        }
    }
}

    #endregion

    #region Registro en AreaManager
    private void RegisterInAreaManager()
    {
        var mgr = FindFirstObjectByType<AreaManager>();
        if (mgr == null) return;

        // Añadir a la lista si no está
        if (!mgr.areaObjects.Contains(gameObject))
        {
            mgr.areaObjects.Add(gameObject);
        }

        // (Aun no podemos inyectar datos en areaDataDict porque es privado)
        // Esto quedará resuelto cuando apliquemos la integración que te paso al final.
    }
    #endregion

    #region Aux
    private void TryNormalizeAreaGameObjectName()
    {
        // No imprescindible, pero ayuda a mantener convención "Area_<KEY>"
        string expected = $"Area_{config.areaKey}";
        if (name != expected && !string.IsNullOrEmpty(config.areaKey))
            name = expected;
    }

    private void TryEnsureLayer()
    {
        int layer = LayerMask.NameToLayer(areasLayerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[AreaBootstrapper] La capa \"{areasLayerName}\" no existe. Crea esa Layer para que AreaManager detecte clicks correctamente.");
            return;
        }

        // Asignar capa recursivamente a esta jerarquía (solo donde tenga sentido)
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            tr.gameObject.layer = layer;
        }
    }
    #endregion
}
