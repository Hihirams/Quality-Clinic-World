using System.Collections.Generic;
using UnityEngine;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
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
        painter.RegisterArea(gameObject);      // crea overlay (mesh/quad)
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
        // Buscar AreaCard existente en hijos
        AreaCard card = GetComponentInChildren<AreaCard>(true);
        if (card == null)
        {
            // Crear holder
            var cardGO = new GameObject("AreaCard");
            cardGO.transform.SetParent(transform, false);

            // Colocar sobre el área
            cardGO.transform.position = new Vector3(
                _worldBounds.center.x,
                _worldBounds.max.y + cardHeightOffset,
                _worldBounds.center.z
            );

            card = cardGO.AddComponent<AreaCard>();
        }

        // Asignar info mínima a la card
        card.areaName = "AREA_" + config.areaKey; // la card usa este nombre textual
        if (card.areaData == null) card.areaData = new AreaData();
        card.areaData.areaName = config.displayName;
        card.areaData.overallResult = config.OverallResult;

        // Activar la card (si tu script tiene API pública para ello)
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
            // Crear label vacío
            var labelGO = new GameObject(expectedName);
            labelGO.transform.SetParent(transform, false);
            labelGO.transform.position = new Vector3(
                _worldBounds.center.x,
                labelHeightY,
                _worldBounds.center.z
            );
            labelTr = labelGO.transform;

            // Canvas worldspace + textos
            var canvas = labelGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = labelSortingOrder;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
            var nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(canvas.transform, false);
            var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = config.displayName;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.fontSize = 6f;

            var pctObj = new GameObject("PercentText");
            pctObj.transform.SetParent(canvas.transform, false);
            var pctTMP = pctObj.AddComponent<TextMeshProUGUI>();
            pctTMP.text = $"{config.OverallResult:F0}%";
            pctTMP.alignment = TextAlignmentOptions.Center;
            pctTMP.fontSize = 6f;

            // Centrar ambos en el canvas
            nameTMP.rectTransform.anchoredPosition = new Vector2(0f, 0.18f);
            pctTMP.rectTransform.anchoredPosition  = new Vector2(0f, -0.14f);
#endif
        }

        // Asegurar componente ManualAreaLabel y enlazar referencias si existen
        var label = labelTr.GetComponent<ManualAreaLabel>();
        if (label == null) label = labelTr.gameObject.AddComponent<ManualAreaLabel>();

        label.areaKey = config.areaKey;                 // autodetección también funciona si está vacío
        label.onlyShowInStaticTopDown = true;           // como usas vista de mapa
        label.canvasSortingOrder = labelSortingOrder;
        label.labelHeightY = labelHeightY;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        // Intenta enlazar textos si existen
        if (label.nameText == null)
        {
            var nameTMP = labelTr.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameTMP != null && nameTMP.name.Contains("Name")) label.nameText = nameTMP;
        }
        if (label.percentText == null)
        {
            foreach (var tmp in labelTr.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.name.Contains("Percent")) { label.percentText = tmp; break; }
            }
        }
#endif

        // Como todavía el AreaManager no conoce esta área, el porcentaje del label
        // se verá 0% cuando el script de label consulte al manager.
        // Por ahora dejamos los textos iniciales (ya seteados arriba).
        // En cuanto conectemos el AreaManager, el label se actualizará solo.
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
