using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IndustrialDashboard : MonoBehaviour
{
    [Header("Referencias de UI - Se crean automáticamente")]
    private Canvas mainCanvas;

    [Header("Configuración de Colores")]
    public Color primaryColor = new Color(0.2f, 0.4f, 0.8f, 1f);    // Azul industrial
    public Color secondaryColor = new Color(0.3f, 0.3f, 0.3f, 1f);  // Gris oscuro
    public Color accentColor = new Color(0.0f, 0.8f, 0.4f, 1f);     // Verde
    public Color warningColor = new Color(1f, 0.6f, 0f, 1f);        // Naranja
    public Color dangerColor = new Color(0.8f, 0.2f, 0.2f, 1f);     // Rojo

    private GameObject kpisPanel;
    private GameObject prediccionesPanel;
    private Button kpisButton;
    private Button prediccionesButton;

    // Configuración de comportamiento
    [Header("Configuración de Comportamiento")]
    public bool showOnStart = false; // Por defecto FALSE para que no aparezca automáticamente

    // Datos del Dashboard
    [Header("Datos del Dashboard")]
    public List<KPIData> kpisData = new List<KPIData>
    {
        new KPIData("Selecciona un área", 0f, ""),
        new KPIData("para ver los KPIs", 0f, ""),
        new KPIData("correspondientes", 0f, "")
    };

    public List<string> prediccionesData = new List<string>
    {
        "Selecciona un área para ver predicciones específicas",
        "Las predicciones se generarán automáticamente",
        "basadas en los datos de rendimiento del área"
    };

    // Variables de control
    private string currentAreaName = "Ninguna";
    private GameObject areaTitle; // Para mostrar el nombre del área seleccionada

    void Start()
    {
        Debug.Log("Iniciando Industrial Dashboard...");

        // Crear Canvas principal si no existe
        CreateMainCanvas();

        // Configurar interfaz principal
        SetupMainInterface();

        // Solo mostrar si showOnStart es true
        if (!showOnStart)
        {
            HideInterface();
            Debug.Log("✓ Dashboard iniciado pero ocultado (esperando selección de área)");
        }
    }

    // Método para ocultar la interfaz
    public void HideInterface()
    {
        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(false);
        }
    }

    // Método para mostrar la interfaz
    public void ShowInterface()
    {
        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(true);
            Debug.Log("✓ Dashboard mostrado");
        }
    }

    // Método principal para actualizar con datos de área específica
    public void UpdateWithAreaData(string areaName, List<KPIData> newKPIs, List<string> newPredictions)
    {
        currentAreaName = areaName;
        kpisData = newKPIs ?? kpisData;
        prediccionesData = newPredictions ?? newPredictions;

        // Actualizar el título del área
        UpdateAreaTitle();

        // Recrear los paneles con los nuevos datos
        RefreshPanels();

        // Mostrar la interfaz si estaba oculta
        ShowInterface();

        Debug.Log($"✓ Dashboard actualizado con datos de: {areaName}");
    }

    // Método para actualizar el título del área
    void UpdateAreaTitle()
    {
        if (areaTitle != null)
        {
            Text titleText = areaTitle.GetComponent<Text>();
            if (titleText != null)
            {
                titleText.text = $"ÁREA: {currentAreaName.ToUpper()}";
            }
        }
    }

    // Método para refrescar los paneles
    void RefreshPanels()
    {
        // Destruir paneles existentes si existen
        if (kpisPanel != null)
        {
            DestroyImmediate(kpisPanel);
        }
        if (prediccionesPanel != null)
        {
            DestroyImmediate(prediccionesPanel);
        }

        // Recrear paneles con datos actualizados
        CreatePanels();

        // Reconfigurar eventos de botones
        SetupButtonEvents();
    }

    // Función auxiliar para crear GameObjects de UI correctamente
    GameObject CreateUIGameObject(string name, Transform parent = null)
    {
        GameObject obj = new GameObject(name);

        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        // Asegurar que tiene RectTransform para UI
        if (obj.GetComponent<RectTransform>() == null)
        {
            obj.AddComponent<RectTransform>();
        }

        return obj;
    }

    void CreateMainCanvas()
    {
        // Buscar si ya existe un Canvas
        mainCanvas = FindObjectOfType<Canvas>();

        if (mainCanvas == null)
        {
            // Crear GameObject para el Canvas
            GameObject canvasObj = new GameObject("UI_Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();

            // Configurar Canvas
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            // Agregar CanvasScaler para responsive design
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Agregar GraphicRaycaster para detección de clicks
            canvasObj.AddComponent<GraphicRaycaster>();

            Debug.Log("✓ Canvas principal creado automáticamente");
        }
        else
        {
            Debug.Log("✓ Canvas existente encontrado");
        }

        // Crear EventSystem si no existe
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Debug.Log("✓ EventSystem creado automáticamente");
        }
        else
        {
            Debug.Log("✓ EventSystem existente encontrado");
        }
    }

    void SetupMainInterface()
    {
        // Crear título del área
        CreateAreaTitle();

        // Crear botones principales
        CreateMainButtons();

        // Crear paneles (inicialmente ocultos)
        CreatePanels();

        // Configurar eventos
        SetupButtonEvents();
    }

    // Crear título del área seleccionada
    void CreateAreaTitle()
    {
        areaTitle = CreateUIGameObject("AreaTitle", mainCanvas.transform);

        Text titleText = areaTitle.AddComponent<Text>();
        titleText.text = $"ÁREA: {currentAreaName.ToUpper()}";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = primaryColor;
        titleText.alignment = TextAnchor.MiddleCenter;

        RectTransform titleRect = areaTitle.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(400, 40);
        titleRect.anchoredPosition = new Vector2(0, 350);

        // Botón de cerrar/volver
        CreateBackButton();
    }

    // Crear botón para volver/cerrar
    void CreateBackButton()
    {
        GameObject backButtonObj = CreateUIGameObject("BackButton", mainCanvas.transform);

        Button backButton = backButtonObj.AddComponent<Button>();
        Image backButtonImage = backButtonObj.AddComponent<Image>();
        backButtonImage.color = dangerColor;

        RectTransform backRect = backButtonObj.GetComponent<RectTransform>();
        backRect.sizeDelta = new Vector2(100, 40);
        backRect.anchoredPosition = new Vector2(-450, 350);

        GameObject backTextObj = CreateUIGameObject("Text", backButtonObj.transform);

        Text backText = backTextObj.AddComponent<Text>();
        backText.text = "✕ CERRAR";
        backText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        backText.fontSize = 12;
        backText.color = Color.white;
        backText.alignment = TextAnchor.MiddleCenter;

        RectTransform backTextRect = backTextObj.GetComponent<RectTransform>();
        backTextRect.sizeDelta = new Vector2(100, 40);
        backTextRect.anchoredPosition = Vector2.zero;

        backButton.onClick.AddListener(() => {
            Debug.Log("✅ Back button clicked! Cerrando dashboard...");
            HideInterface();

            // Notificar al AreaManager si existe
            AreaManager areaManager = FindObjectOfType<AreaManager>();
            if (areaManager != null)
            {
                areaManager.CloseDashboard();
            }
        });

        AddButtonHoverEffects(backButton, dangerColor);
    }

    void CreateMainButtons()
    {
        Debug.Log("Creando botones principales...");

        // Botón KPIs
        GameObject kpisButtonObj = CreateUIGameObject("KPIs_Button", mainCanvas.transform);

        // Asegurar componentes necesarios
        kpisButton = kpisButtonObj.AddComponent<Button>();
        Image kpisButtonImage = kpisButtonObj.AddComponent<Image>();
        kpisButtonImage.color = primaryColor;

        // Configurar RectTransform del botón KPIs
        RectTransform kpisRect = kpisButtonObj.GetComponent<RectTransform>();
        kpisRect.sizeDelta = new Vector2(200, 60);
        kpisRect.anchoredPosition = new Vector2(-120, 250);

        // Texto del botón KPIs
        GameObject kpisTextObj = CreateUIGameObject("Text", kpisButtonObj.transform);

        Text kpisText = kpisTextObj.AddComponent<Text>();
        kpisText.text = "KPIs";
        kpisText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        kpisText.fontSize = 18;
        kpisText.color = Color.white;
        kpisText.alignment = TextAnchor.MiddleCenter;

        RectTransform kpisTextRect = kpisTextObj.GetComponent<RectTransform>();
        kpisTextRect.sizeDelta = new Vector2(200, 60);
        kpisTextRect.anchoredPosition = Vector2.zero;

        // Botón Predicciones
        GameObject prediccionesButtonObj = CreateUIGameObject("Predicciones_Button", mainCanvas.transform);

        prediccionesButton = prediccionesButtonObj.AddComponent<Button>();
        Image prediccionesButtonImage = prediccionesButtonObj.AddComponent<Image>();
        prediccionesButtonImage.color = accentColor;

        // Configurar RectTransform del botón Predicciones
        RectTransform prediccionesRect = prediccionesButtonObj.GetComponent<RectTransform>();
        prediccionesRect.sizeDelta = new Vector2(200, 60);
        prediccionesRect.anchoredPosition = new Vector2(120, 250);

        // Texto del botón Predicciones
        GameObject prediccionesTextObj = CreateUIGameObject("Text", prediccionesButtonObj.transform);

        Text prediccionesText = prediccionesTextObj.AddComponent<Text>();
        prediccionesText.text = "PREDICCIONES";
        prediccionesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prediccionesText.fontSize = 16;
        prediccionesText.color = Color.white;
        prediccionesText.alignment = TextAnchor.MiddleCenter;

        RectTransform prediccionesTextRect = prediccionesTextObj.GetComponent<RectTransform>();
        prediccionesTextRect.sizeDelta = new Vector2(200, 60);
        prediccionesTextRect.anchoredPosition = Vector2.zero;

        // Efectos hover para botones
        AddButtonHoverEffects(kpisButton, primaryColor);
        AddButtonHoverEffects(prediccionesButton, accentColor);

        Debug.Log("✓ Botones creados - KPIs: " + (kpisButton != null) + ", Predicciones: " + (prediccionesButton != null));
    }

    void CreatePanels()
    {
        // Panel KPIs
        CreateKPIsPanel();

        // Panel Predicciones
        CreatePrediccionesPanel();
    }

    void CreateKPIsPanel()
    {
        kpisPanel = CreateUIGameObject("KPIs_Panel", mainCanvas.transform);

        Image panelImage = kpisPanel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        RectTransform panelRect = kpisPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(600, 400);
        panelRect.anchoredPosition = new Vector2(0, 0);

        // ScrollView para KPIs (versión simplificada que funciona)
        CreateSimpleKPIsList();

        // Botón cerrar
        CreateCloseButton(kpisPanel, "Cerrar KPIs");

        kpisPanel.SetActive(false);
    }

    void CreateSimpleKPIsList()
    {
        GameObject content = CreateUIGameObject("Content", kpisPanel.transform);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(580, 300);
        contentRect.anchoredPosition = new Vector2(0, 20);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 8f;
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;

        // Crear elementos KPI
        foreach (KPIData kpi in kpisData)
        {
            CreateKPIElement(content, kpi);
        }
    }

    void CreateKPIElement(GameObject parent, KPIData kpi)
    {
        GameObject kpiElement = CreateUIGameObject($"KPI_{kpi.name.Replace(" ", "_")}", parent.transform);

        Image elementBg = kpiElement.AddComponent<Image>();
        elementBg.color = secondaryColor;

        RectTransform elementRect = kpiElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(540, 35);

        // Layout horizontal
        HorizontalLayoutGroup horizontalLayout = kpiElement.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(15, 15, 5, 5);
        horizontalLayout.spacing = 10f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Nombre del KPI
        GameObject nameObj = CreateUIGameObject("Name", kpiElement.transform);

        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = kpi.name;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 12;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(200, 25);

        // Barra de progreso simple (solo si hay valor)
        if (kpi.value > 0)
        {
            CreateProgressBar(kpiElement, kpi.value);
        }

        // Valor numérico
        GameObject valueObj = CreateUIGameObject("Value", kpiElement.transform);

        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = kpi.value > 0 ? $"{kpi.value:F1}{kpi.unit}" : "";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = GetKPIColor(kpi.value, kpi.name);
        valueText.alignment = TextAnchor.MiddleCenter;

        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(80, 25);
    }

    void CreateProgressBar(GameObject parent, float value)
    {
        GameObject progressBarBg = CreateUIGameObject("ProgressBarBg", parent.transform);

        Image bgImage = progressBarBg.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        RectTransform bgRect = progressBarBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(120, 15);

        GameObject progressBarFill = CreateUIGameObject("ProgressBarFill", progressBarBg.transform);

        Image fillImage = progressBarFill.AddComponent<Image>();
        fillImage.color = GetProgressBarColor(value);

        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(120 * (value / 100f), 15);
        fillRect.anchoredPosition = new Vector2(-(120 * (1f - value / 100f)) / 2f, 0);
    }

    void CreatePrediccionesPanel()
    {
        prediccionesPanel = CreateUIGameObject("Predicciones_Panel", mainCanvas.transform);

        Image panelImage = prediccionesPanel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.15f, 0.1f, 0.95f);

        RectTransform panelRect = prediccionesPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(700, 450);
        panelRect.anchoredPosition = new Vector2(0, 0);

        // Título dinámico
        CreatePanelTitle(prediccionesPanel, $"PREDICCIONES - {currentAreaName.ToUpper()}");

        // Lista simple de predicciones
        CreateSimplePrediccionesList();

        // Botón cerrar
        CreateCloseButton(prediccionesPanel, "Cerrar Predicciones");

        prediccionesPanel.SetActive(false);
    }

    void CreatePanelTitle(GameObject panel, string titleText)
    {
        GameObject titleObj = CreateUIGameObject("Title", panel.transform);

        Text title = titleObj.AddComponent<Text>();
        title.text = titleText;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 18;
        title.fontStyle = FontStyle.Bold;
        title.color = accentColor;
        title.alignment = TextAnchor.MiddleCenter;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(680, 30);
        titleRect.anchoredPosition = new Vector2(0, 185);
    }

    void CreateSimplePrediccionesList()
    {
        GameObject content = CreateUIGameObject("Content", prediccionesPanel.transform);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(680, 320);
        contentRect.anchoredPosition = new Vector2(0, -10);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 5f;
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;

        // Crear elementos de predicciones
        for (int i = 0; i < prediccionesData.Count; i++)
        {
            CreatePrediccionElement(content, prediccionesData[i], i);
        }
    }

    void CreatePrediccionElement(GameObject parent, string prediccion, int index)
    {
        GameObject prediccionElement = CreateUIGameObject($"Prediccion_{index}", parent.transform);

        Image elementBg = prediccionElement.AddComponent<Image>();
        elementBg.color = new Color(0.15f, 0.2f, 0.15f, 0.8f);

        RectTransform elementRect = prediccionElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(640, 40);

        // Indicador de prioridad
        GameObject indicator = CreateUIGameObject("Indicator", prediccionElement.transform);

        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = GetPriorityColor(index);

        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.sizeDelta = new Vector2(6, 30);
        indicatorRect.anchoredPosition = new Vector2(-310, 0);

        // Texto de predicción
        GameObject textObj = CreateUIGameObject("Text", prediccionElement.transform);

        Text text = textObj.AddComponent<Text>();
        text.text = prediccion;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 11;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(600, 30);
        textRect.anchoredPosition = new Vector2(20, 0);
    }

    void CreateCloseButton(GameObject panel, string buttonText)
    {
        GameObject closeButtonObj = CreateUIGameObject("CloseButton", panel.transform);

        Button closeButton = closeButtonObj.AddComponent<Button>();
        Image closeButtonImage = closeButtonObj.AddComponent<Image>();
        closeButtonImage.color = warningColor;

        RectTransform closeRect = closeButtonObj.GetComponent<RectTransform>();
        closeRect.sizeDelta = new Vector2(120, 30);
        closeRect.anchoredPosition = new Vector2(0, -190);

        GameObject closeTextObj = CreateUIGameObject("Text", closeButtonObj.transform);

        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.text = "✕ CERRAR";
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 12;
        closeText.color = Color.white;
        closeText.alignment = TextAnchor.MiddleCenter;

        RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.sizeDelta = new Vector2(120, 30);
        closeTextRect.anchoredPosition = Vector2.zero;

        closeButton.onClick.AddListener(() => {
            Debug.Log("✅ Close button clicked! Cerrando panel...");
            panel.SetActive(false);
        });
        AddButtonHoverEffects(closeButton, warningColor);
    }

    void SetupButtonEvents()
    {
        if (kpisButton != null && prediccionesButton != null)
        {
            // Limpiar listeners anteriores
            kpisButton.onClick.RemoveAllListeners();
            prediccionesButton.onClick.RemoveAllListeners();

            // Verificar que los botones están configurados correctamente
            Debug.Log("KPIs Button interactable: " + kpisButton.interactable);
            Debug.Log("Predicciones Button interactable: " + prediccionesButton.interactable);

            kpisButton.onClick.AddListener(() => {
                Debug.Log("✅ KPIs button clicked! - Activando panel KPIs");
                kpisPanel.SetActive(true);
                prediccionesPanel.SetActive(false);
            });

            prediccionesButton.onClick.AddListener(() => {
                Debug.Log("✅ Predicciones button clicked! - Activando panel Predicciones");
                prediccionesPanel.SetActive(true);
                kpisPanel.SetActive(false);
            });

            Debug.Log("✓ Event listeners agregados correctamente");

            // Verificar que el EventSystem existe
            var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            Debug.Log("EventSystem encontrado: " + (eventSystem != null));
            if (eventSystem != null)
            {
                Debug.Log("EventSystem activo: " + eventSystem.gameObject.activeInHierarchy);
            }
        }
        else
        {
            Debug.LogError("❌ Error: Botones no encontrados para configurar eventos");
            Debug.LogError("KPIs Button: " + (kpisButton != null ? "OK" : "NULL"));
            Debug.LogError("Predicciones Button: " + (prediccionesButton != null ? "OK" : "NULL"));
        }
    }

    void AddButtonHoverEffects(Button button, Color originalColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = originalColor;
        colors.highlightedColor = new Color(originalColor.r * 1.2f, originalColor.g * 1.2f, originalColor.b * 1.2f, 1f);
        colors.pressedColor = new Color(originalColor.r * 0.8f, originalColor.g * 0.8f, originalColor.b * 0.8f, 1f);
        colors.disabledColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f);
        button.colors = colors;
    }

    Color GetKPIColor(float value, string kpiName)
    {
        // Si el valor es 0, no mostrar color específico
        if (value <= 0) return Color.white;

        // Lógica específica para diferentes KPIs
        if (kpiName.Contains("Inactividad") || kpiName.Contains("Desperdicio"))
        {
            if (value <= 10f) return accentColor;      // Bueno
            else if (value <= 20f) return warningColor; // Regular
            else return dangerColor;                     // Malo
        }
        else
        {
            if (value >= 90f) return accentColor;       // Excelente
            else if (value >= 75f) return warningColor; // Bueno
            else return dangerColor;                     // Necesita atención
        }
    }

    Color GetProgressBarColor(float value)
    {
        if (value >= 90f) return accentColor;
        else if (value >= 75f) return warningColor;
        else return dangerColor;
    }

    Color GetPriorityColor(int index)
    {
        // Rotar entre colores de prioridad
        Color[] priorityColors = { dangerColor, warningColor, accentColor, primaryColor };
        return priorityColors[index % priorityColors.Length];
    }

    // Método para forzar actualización (llamado desde AreaManager)
    public void ForceUpdate()
    {
        if (mainCanvas != null && mainCanvas.gameObject.activeInHierarchy)
        {
            RefreshPanels();
        }
    }
}

[System.Serializable]
public class KPIData
{
    public string name;
    public float value;
    public string unit;

    public KPIData(string name, float value, string unit)
    {
        this.name = name;
        this.value = value;
        this.unit = unit;
    }
}