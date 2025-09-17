using UnityEngine;
using TMPro;

/// <summary>
/// Script para manejar textos manuales de áreas que solo se muestran en vista top-down estática.
/// Coloca este script en el GameObject padre que contiene los textos de cada área.
/// </summary>
public class ManualAreaLabel : MonoBehaviour
{
    [Header("Referencias de Textos")]
    [Tooltip("Asigna aquí el TextMeshPro del nombre del área")]
    public TextMeshProUGUI nameText;

    [Tooltip("Asigna aquí el TextMeshPro del porcentaje")]
    public TextMeshProUGUI percentText;

    [Header("Configuración")]
    [Tooltip("Clave del área para buscar datos (ATHONDA, VCTL4, BUZZERL2, VBL1)")]
    public string areaKey = "ATHONDA";

    [Tooltip("Si quieres usar un nombre personalizado en lugar del que viene del AreaManager")]
    public bool useCustomName = false;
    public string customNameText = "AT HONDA";

    [Header("Vista")]
    [Tooltip("Solo mostrar en vista top-down estática")]
    public bool onlyShowInStaticTopDown = true;

    // Referencias internas
    private AreaManager areaManager;
    private TopDownCameraController topDownController;
    private bool currentlyVisible = false;

    void Start()
    {
        // Encontrar referencias necesarias
        areaManager = FindObjectOfType<AreaManager>();
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            topDownController = mainCamera.GetComponent<TopDownCameraController>();
        }

        // Configurar el nombre inicial si es personalizado
        if (useCustomName && nameText != null && !string.IsNullOrEmpty(customNameText))
        {
            nameText.text = customNameText;
        }

        // Ocultar al inicio
        SetVisibility(false);

        // Actualizar datos iniciales
        UpdateTexts();
    }

    void Update()
    {
        // Verificar si debemos mostrar los textos
        bool shouldShow = ShouldShowTexts();

        if (shouldShow != currentlyVisible)
        {
            currentlyVisible = shouldShow;
            SetVisibility(currentlyVisible);

            // Actualizar el porcentaje cuando se muestre
            if (currentlyVisible)
            {
                UpdatePercentage();
            }
        }
    }

    bool ShouldShowTexts()
    {
        if (!onlyShowInStaticTopDown) return true;

        if (topDownController == null) return false;

        // Verificar si estamos en modo top-down Y usando vista estática
        return topDownController.enabled && topDownController.IsUsingFixedStaticView();
    }

    void SetVisibility(bool visible)
    {
        if (nameText != null)
            nameText.gameObject.SetActive(visible);

        if (percentText != null)
            percentText.gameObject.SetActive(visible);
    }

    void UpdateTexts()
    {
        UpdateName();
        UpdatePercentage();
    }

    void UpdateName()
    {
        if (nameText == null) return;

        if (useCustomName)
        {
            nameText.text = customNameText;
        }
        else if (areaManager != null)
        {
            // Obtener el nombre del AreaManager
            var areaData = GetAreaData();
            if (areaData != null)
            {
                nameText.text = areaData.displayName;
            }
        }
    }

    void UpdatePercentage()
    {
        if (percentText == null || areaManager == null) return;

        var areaData = GetAreaData();
        if (areaData != null)
        {
            // Actualizar texto del porcentaje
            percentText.text = $"{areaData.overallResult:F0}%";

            // Actualizar color según el valor
            UpdatePercentageColor(areaData.overallResult);
        }
        else
        {
            percentText.text = "0%";
        }
    }

    void UpdatePercentageColor(float overallResult)
    {
        if (percentText == null) return;

        Color targetColor;

        if (overallResult >= 90f)
            targetColor = new Color(0.0f, 0.6f, 0.2f); // Verde
        else if (overallResult >= 80f)
            targetColor = new Color(0.1f, 0.5f, 0.8f); // Azul  
        else if (overallResult >= 70f)
            targetColor = new Color(1f, 0.65f, 0f);     // Naranja
        else
            targetColor = new Color(0.9f, 0.2f, 0.2f);  // Rojo

        percentText.color = targetColor;
    }

    AreaManager.AreaData GetAreaData()
    {
        if (areaManager == null) return null;

        // Normalizar la clave del área
        string normalizedKey = NormalizeAreaKey(areaKey);
        return areaManager.GetAreaData(normalizedKey);
    }

    string NormalizeAreaKey(string key)
    {
        string upper = (key ?? "").ToUpperInvariant();
        upper = upper.Replace("AREA_", "").Replace(" ", "").Replace("_", "");

        // Mapear a las claves exactas que usa AreaManager
        if (upper.Contains("ATHONDA") || upper == "ATHONDA") return "ATHONDA";
        if (upper.Contains("VCTL4") || upper == "VCTL4") return "VCTL4";
        if (upper.Contains("BUZZERL2") || upper == "BUZZERL2") return "BUZZERL2";
        if (upper.Contains("VBL1") || upper == "VBL1") return "VBL1";

        return upper;
    }

    // Método público para forzar actualización (llamado desde AreaManager si es necesario)
    public void ForceRefresh()
    {
        UpdateTexts();
    }

    /// <summary>
    /// ✅ NUEVO: Método llamado por ManualLabelsManager para fijar visibilidad
    /// cuando cambia el estado de la vista top-down.
    /// </summary>
    public void SetTopDownVisibility(bool visible)
    {
        // Si este label solo debe mostrarse en la vista top-down estática,
        // respetamos el parámetro 'visible' que manda el manager.
        currentlyVisible = visible;
        SetVisibility(visible);

        if (visible)
        {
            // Aseguramos que los datos estén frescos al mostrarse.
            UpdateTexts();
        }
    }

    // Para depuración en el Inspector
    void OnValidate()
    {
        if (Application.isPlaying && currentlyVisible)
        {
            UpdateTexts();
        }
    }
}
