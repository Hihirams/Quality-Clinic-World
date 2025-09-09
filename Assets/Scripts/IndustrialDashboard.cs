using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IndustrialDashboard : MonoBehaviour
{
    [Header("Referencias de UI - Se crean automáticamente")]
    private Canvas mainCanvas;

    [Header("Configuración de Colores - EXACTOS de la imagen")]
    public Color primaryColor = new Color(0.2f, 0.6f, 1f, 1f);        // Azul KPIs
    public Color secondaryColor = new Color(1f, 1f, 1f, 1f);          // FONDO BLANCO PURO
    public Color accentColor = new Color(0.0f, 0.8f, 0.4f, 1f);       // Verde PREDICCIONES
    public Color warningColor = new Color(1f, 0.65f, 0f, 1f);         // Naranja barras
    public Color dangerColor = new Color(0.9f, 0.3f, 0.3f, 1f);       // Rojo alertas
    public Color textDarkColor = new Color(0.2f, 0.2f, 0.2f, 1f);     // Texto oscuro
    public Color progressBgColor = new Color(0.88f, 0.88f, 0.88f, 1f); // Fondo barras gris claro
    public Color lightGrayColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Botón cerrar
    public Color panelGrayColor = new Color(0.45f, 0.45f, 0.45f, 1f); // Fondo panel GRIS como imagen

    private GameObject kpisPanel;
    private GameObject prediccionesPanel;
    private Button kpisButton;
    private Button prediccionesButton;
    private GameObject mainPanel;

    // Configuración de comportamiento
    [Header("Configuración de Comportamiento")]
    public bool showOnStart = false;

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
    private GameObject areaTitle;

    // Sprites para bordes redondeados
    private Sprite roundedRectSprite;
    private Sprite veryRoundedSprite; // Para el panel principal más redondeado

    void Start()
    {
        Debug.Log("Iniciando Industrial Dashboard...");

        CreateRoundedSprites();
        CreateMainCanvas();
        SetupMainInterface();

        if (!showOnStart)
        {
            HideInterface();
            Debug.Log("✓ Dashboard iniciado pero ocultado (esperando selección de área)");
        }
    }

    void CreateRoundedSprites()
    {
        roundedRectSprite = CreateRoundedRectSprite(6);
        veryRoundedSprite = CreateRoundedRectSprite(16); // Panel principal más redondeado
    }

    Sprite CreateRoundedRectSprite(int cornerRadius = 6)
    {
        int width = 64;
        int height = 64;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isInside = IsInsideRoundedRect(x, y, width, height, cornerRadius);
                pixels[y * width + x] = isInside ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
    }

    bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
    {
        // Esquinas
        bool isInCorner = (x < radius && y < radius) ||
                         (x >= width - radius && y < radius) ||
                         (x < radius && y >= height - radius) ||
                         (x >= width - radius && y >= height - radius);

        if (!isInCorner) return true;

        // Calcular distancia desde la esquina más cercana
        Vector2 cornerCenter;
        if (x < radius && y < radius) cornerCenter = new Vector2(radius, radius);
        else if (x >= width - radius && y < radius) cornerCenter = new Vector2(width - radius, radius);
        else if (x < radius && y >= height - radius) cornerCenter = new Vector2(radius, height - radius);
        else cornerCenter = new Vector2(width - radius, height - radius);

        return Vector2.Distance(new Vector2(x, y), cornerCenter) <= radius;
    }

    public void HideInterface()
    {
        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(false);
        }
    }

    public void ShowInterface()
    {
        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(true);
            Debug.Log("✓ Dashboard mostrado");
        }
    }

    public void UpdateWithAreaData(string areaName, List<KPIData> newKPIs, List<string> newPredictions)
    {
        currentAreaName = areaName;
        kpisData = newKPIs ?? kpisData;
        prediccionesData = newPredictions ?? newPredictions;

        UpdateAreaTitle();
        RefreshPanels();
        ShowInterface();

        Debug.Log($"✓ Dashboard actualizado con datos de: {areaName}");
    }

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

    void RefreshPanels()
    {
        if (kpisPanel != null)
        {
            DestroyImmediate(kpisPanel);
        }
        if (prediccionesPanel != null)
        {
            DestroyImmediate(prediccionesPanel);
        }

        CreatePanels();
        SetupButtonEvents();
    }

    GameObject CreateUIGameObject(string name, Transform parent = null)
    {
        GameObject obj = new GameObject(name);

        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        if (obj.GetComponent<RectTransform>() == null)
        {
            obj.AddComponent<RectTransform>();
        }

        return obj;
    }

    // En IndustrialDashboard.cs
    void CreateMainCanvas()
    {
        // 1) Usar SOLO un canvas llamado "UI_Canvas"
        GameObject existing = GameObject.Find("UI_Canvas");
        if (existing != null)
        {
            mainCanvas = existing.GetComponent<Canvas>();
        }
        else
        {
            // 2) Si no existe, créalo (no uses FindObjectOfType<Canvas>())
            GameObject canvasObj = new GameObject("UI_Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("✓ Canvas principal creado automáticamente (UI_Canvas)");
        }

        // EventSystem asegurado
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("✓ EventSystem creado automáticamente");
        }
    }


    void SetupMainInterface()
    {
        CreateMainPanel();
        CreateAreaTitle();
        CreateMainButtons();
        CreatePanels();
        SetupButtonEvents();
    }

    void CreateMainPanel()
    {
        mainPanel = CreateUIGameObject("MainPanel", mainCanvas.transform);

        Image panelImage = mainPanel.AddComponent<Image>();
        panelImage.color = panelGrayColor; // FONDO GRIS como en la imagen
        panelImage.sprite = veryRoundedSprite; // Sprite más redondeado
        panelImage.type = Image.Type.Sliced;

        RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(540, 700); // Más alto para acomodar mejor el mensaje
        panelRect.anchoredPosition = new Vector2(0, 0);
    }

    void CreateAreaTitle()
    {
        areaTitle = CreateUIGameObject("AreaTitle", mainPanel.transform);

        Text titleText = areaTitle.AddComponent<Text>();
        titleText.text = $"ÁREA: {currentAreaName.ToUpper()}";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = primaryColor; // Azul
        titleText.alignment = TextAnchor.MiddleLeft;

        RectTransform titleRect = areaTitle.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(350, 35);
        titleRect.anchoredPosition = new Vector2(-70, 315); // Ajustado para el panel más alto

        CreateCloseButton();
    }

    void CreateCloseButton()
    {
        GameObject closeButtonObj = CreateUIGameObject("CloseButton", mainPanel.transform);

        Button closeButton = closeButtonObj.AddComponent<Button>();
        Image closeButtonImage = closeButtonObj.AddComponent<Image>();
        closeButtonImage.color = lightGrayColor;
        closeButtonImage.sprite = roundedRectSprite;
        closeButtonImage.type = Image.Type.Sliced;

        RectTransform closeRect = closeButtonObj.GetComponent<RectTransform>();
        closeRect.sizeDelta = new Vector2(90, 30);
        closeRect.anchoredPosition = new Vector2(200, 315); // Ajustado para el panel más alto

        GameObject closeTextObj = CreateUIGameObject("Text", closeButtonObj.transform);

        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.text = "✕ CERRAR";
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 11;
        closeText.fontStyle = FontStyle.Bold;
        closeText.color = textDarkColor;
        closeText.alignment = TextAnchor.MiddleCenter;

        RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.sizeDelta = new Vector2(90, 30);
        closeTextRect.anchoredPosition = Vector2.zero;

        closeButton.onClick.AddListener(() => {
            Debug.Log("✅ Close button clicked! Cerrando dashboard...");
            HideInterface();

            AreaManager areaManager = FindObjectOfType<AreaManager>();
            if (areaManager != null)
            {
                areaManager.CloseDashboard();
            }
        });

        AddModernButtonHoverEffects(closeButton, lightGrayColor);
    }

    void CreateMainButtons()
    {
        // Botón KPIs (Azul)
        GameObject kpisButtonObj = CreateUIGameObject("KPIs_Button", mainPanel.transform);

        kpisButton = kpisButtonObj.AddComponent<Button>();
        Image kpisButtonImage = kpisButtonObj.AddComponent<Image>();
        kpisButtonImage.color = primaryColor;
        kpisButtonImage.sprite = roundedRectSprite;
        kpisButtonImage.type = Image.Type.Sliced;

        RectTransform kpisRect = kpisButtonObj.GetComponent<RectTransform>();
        kpisRect.sizeDelta = new Vector2(240, 45);
        kpisRect.anchoredPosition = new Vector2(-125, 250); // Ajustado para el panel más alto

        GameObject kpisTextObj = CreateUIGameObject("Text", kpisButtonObj.transform);
        Text kpisText = kpisTextObj.AddComponent<Text>();
        kpisText.text = "KPIs";
        kpisText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        kpisText.fontSize = 16;
        kpisText.fontStyle = FontStyle.Bold;
        kpisText.color = Color.white;
        kpisText.alignment = TextAnchor.MiddleCenter;

        RectTransform kpisTextRect = kpisTextObj.GetComponent<RectTransform>();
        kpisTextRect.sizeDelta = new Vector2(240, 45);
        kpisTextRect.anchoredPosition = Vector2.zero;

        // Botón Predicciones (Verde)
        GameObject prediccionesButtonObj = CreateUIGameObject("Predicciones_Button", mainPanel.transform);

        prediccionesButton = prediccionesButtonObj.AddComponent<Button>();
        Image prediccionesButtonImage = prediccionesButtonObj.AddComponent<Image>();
        prediccionesButtonImage.color = accentColor;
        prediccionesButtonImage.sprite = roundedRectSprite;
        prediccionesButtonImage.type = Image.Type.Sliced;

        RectTransform prediccionesRect = prediccionesButtonObj.GetComponent<RectTransform>();
        prediccionesRect.sizeDelta = new Vector2(240, 45);
        prediccionesRect.anchoredPosition = new Vector2(125, 250); // Ajustado para el panel más alto

        GameObject prediccionesTextObj = CreateUIGameObject("Text", prediccionesButtonObj.transform);
        Text prediccionesText = prediccionesTextObj.AddComponent<Text>();
        prediccionesText.text = "PREDICCIONES";
        prediccionesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prediccionesText.fontSize = 13;
        prediccionesText.fontStyle = FontStyle.Bold;
        prediccionesText.color = Color.white;
        prediccionesText.alignment = TextAnchor.MiddleCenter;

        RectTransform prediccionesTextRect = prediccionesTextObj.GetComponent<RectTransform>();
        prediccionesTextRect.sizeDelta = new Vector2(240, 45);
        prediccionesTextRect.anchoredPosition = Vector2.zero;

        AddModernButtonHoverEffects(kpisButton, primaryColor);
        AddModernButtonHoverEffects(prediccionesButton, accentColor);
    }

    void CreatePanels()
    {
        CreateKPIsPanel();
        CreatePrediccionesPanel();
    }

    void CreateKPIsPanel()
    {
        kpisPanel = CreateUIGameObject("KPIs_Panel", mainPanel.transform);

        RectTransform panelRect = kpisPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 380); // Contenido dentro del panel
        panelRect.anchoredPosition = new Vector2(0, 10); // Ajustado para el panel más alto

        CreateModernKPIsList();
        kpisPanel.SetActive(true); // Mostrar KPIs por defecto
    }

    void CreateModernKPIsList()
    {
        GameObject content = CreateUIGameObject("Content", kpisPanel.transform);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(500, 380);
        contentRect.anchoredPosition = new Vector2(0, 0);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 12f;
        layoutGroup.padding = new RectOffset(15, 15, 15, 15);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;

        foreach (KPIData kpi in kpisData)
        {
            CreateModernKPIElement(content, kpi);
        }

        // Añadir "Overall Result" al final
        CreateOverallResultElement(content);

        // Crear alert separado FUERA del contenido principal
        CreateImprovedAlertBox();
    }

    void CreateModernKPIElement(GameObject parent, KPIData kpi)
    {
        GameObject kpiElement = CreateUIGameObject($"KPI_{kpi.name.Replace(" ", "_")}", parent.transform);

        RectTransform elementRect = kpiElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(470, 48);

        HorizontalLayoutGroup horizontalLayout = kpiElement.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(5, 15, 12, 12);
        horizontalLayout.spacing = 12f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Nombre del KPI
        GameObject nameObj = CreateUIGameObject("Name", kpiElement.transform);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = kpi.name;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 14;
        nameText.color = textDarkColor; // Texto NEGRO
        nameText.alignment = TextAnchor.MiddleLeft;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(150, 40);

        // Barra de progreso
        if (kpi.value > 0)
        {
            CreateTrueRoundedProgressBar(kpiElement, kpi.value);
        }

        // Valor numérico
        GameObject valueObj = CreateUIGameObject("Value", kpiElement.transform);
        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = kpi.value > 0 ? $"{kpi.value:F0}%" : "";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = textDarkColor;
        valueText.alignment = TextAnchor.MiddleRight;

        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(50, 40);
    }

    void CreateOverallResultElement(GameObject parent)
    {
        GameObject overallElement = CreateUIGameObject("OverallResult", parent.transform);

        RectTransform elementRect = overallElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(470, 48);

        HorizontalLayoutGroup horizontalLayout = overallElement.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(5, 15, 12, 12);
        horizontalLayout.spacing = 12f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Nombre "Overall Result"
        GameObject nameObj = CreateUIGameObject("Name", overallElement.transform);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = "Overall Result";
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 14;
        nameText.color = textDarkColor;
        nameText.alignment = TextAnchor.MiddleLeft;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(150, 40);

        // Barra verde al 92%
        CreateTrueRoundedProgressBar(overallElement, 92f);

        // Valor 92%
        GameObject valueObj = CreateUIGameObject("Value", overallElement.transform);
        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = "92%";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = textDarkColor;
        valueText.alignment = TextAnchor.MiddleRight;

        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(50, 40);
    }

    void CreateTrueRoundedProgressBar(GameObject parent, float value)
    {
        GameObject progressContainer = CreateUIGameObject("ProgressContainer", parent.transform);

        RectTransform containerRect = progressContainer.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(200, 20); // Ancho ligeramente mayor

        // Fondo de la barra
        GameObject progressBarBg = CreateUIGameObject("ProgressBarBg", progressContainer.transform);

        Image bgImage = progressBarBg.AddComponent<Image>();
        bgImage.color = progressBgColor; // Gris claro
        bgImage.sprite = roundedRectSprite;
        bgImage.type = Image.Type.Sliced;

        RectTransform bgRect = progressBarBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(200, 12);
        bgRect.anchoredPosition = new Vector2(0, 0);

        // Barra de progreso coloreada
        GameObject progressBarFill = CreateUIGameObject("ProgressBarFill", progressBarBg.transform);

        Image fillImage = progressBarFill.AddComponent<Image>();
        fillImage.color = GetModernProgressBarColor(value);
        fillImage.sprite = roundedRectSprite;
        fillImage.type = Image.Type.Sliced;

        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        float fillWidth = 200 * (value / 100f);
        fillRect.sizeDelta = new Vector2(fillWidth, 12);
        fillRect.anchoredPosition = new Vector2(-(200 - fillWidth) / 2f, 0);
    }

    void CreateImprovedAlertBox()
    {
        // Alert box mejorado con más espacio y mejor diseño
        GameObject alertBox = CreateUIGameObject("AlertBox", mainPanel.transform);

        Image alertBg = alertBox.AddComponent<Image>();
        alertBg.color = secondaryColor; // Fondo BLANCO
        alertBg.sprite = roundedRectSprite;
        alertBg.type = Image.Type.Sliced;

        RectTransform alertRect = alertBox.GetComponent<RectTransform>();
        alertRect.sizeDelta = new Vector2(500, 65); // Más alto para mejor presentación
        alertRect.anchoredPosition = new Vector2(0, -285); // Posición ajustada para panel más grande

        // Layout vertical para mejor organización
        VerticalLayoutGroup verticalLayout = alertBox.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(20, 20, 12, 12);
        verticalLayout.spacing = 6f;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = false;

        // Fila superior: Icono + Alerta principal
        GameObject topRow = CreateUIGameObject("TopRow", alertBox.transform);
        RectTransform topRowRect = topRow.GetComponent<RectTransform>();
        topRowRect.sizeDelta = new Vector2(460, 28);

        HorizontalLayoutGroup topRowLayout = topRow.AddComponent<HorizontalLayoutGroup>();
        topRowLayout.spacing = 12f;
        topRowLayout.childControlWidth = false;
        topRowLayout.childControlHeight = true;
        topRowLayout.childAlignment = TextAnchor.MiddleLeft;

        // Icono triangular rojo más grande
        GameObject iconObj = CreateUIGameObject("Icon", topRow.transform);
        Text iconText = iconObj.AddComponent<Text>();
        iconText.text = "⚠";
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 22;
        iconText.color = dangerColor;
        iconText.alignment = TextAnchor.MiddleCenter;

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(35, 28);

        // Texto de alerta principal
        GameObject alertTextObj = CreateUIGameObject("AlertText", topRow.transform);
        Text alertText = alertTextObj.AddComponent<Text>();
        alertText.text = "Delivery bajo riesgo";
        alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        alertText.fontSize = 15;
        alertText.fontStyle = FontStyle.Bold;
        alertText.color = textDarkColor;
        alertText.alignment = TextAnchor.MiddleLeft;

        RectTransform alertTextRect = alertTextObj.GetComponent<RectTransform>();
        alertTextRect.sizeDelta = new Vector2(400, 28);

        // Fila inferior: Recomendación
        GameObject bottomRow = CreateUIGameObject("BottomRow", alertBox.transform);
        RectTransform bottomRowRect = bottomRow.GetComponent<RectTransform>();
        bottomRowRect.sizeDelta = new Vector2(460, 22);

        // Texto de recomendación con mejor espaciado
        GameObject recomendacionObj = CreateUIGameObject("Recomendacion", bottomRow.transform);
        Text recomendacionText = recomendacionObj.AddComponent<Text>();
        recomendacionText.text = "• Optimización recomendada para mejorar el rendimiento del área";
        recomendacionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recomendacionText.fontSize = 12;
        recomendacionText.color = accentColor;
        recomendacionText.alignment = TextAnchor.MiddleLeft;

        RectTransform recomendacionRect = recomendacionObj.GetComponent<RectTransform>();
        recomendacionRect.sizeDelta = new Vector2(460, 22);
        recomendacionRect.anchoredPosition = new Vector2(47, 0); // Alineado con el texto principal
    }

    void CreatePrediccionesPanel()
    {
        prediccionesPanel = CreateUIGameObject("Predicciones_Panel", mainPanel.transform);

        RectTransform panelRect = prediccionesPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 380);
        panelRect.anchoredPosition = new Vector2(0, 10); // Ajustado para el panel más alto

        CreateModernPrediccionesList();
        prediccionesPanel.SetActive(false);
    }

    void CreateModernPrediccionesList()
    {
        GameObject content = CreateUIGameObject("Content", prediccionesPanel.transform);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(500, 380);
        contentRect.anchoredPosition = new Vector2(0, 0);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 15f;
        layoutGroup.padding = new RectOffset(15, 15, 20, 20);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;

        for (int i = 0; i < prediccionesData.Count; i++)
        {
            CreatePrediccionElement(content, prediccionesData[i], i);
        }
    }

    void CreatePrediccionElement(GameObject parent, string prediccion, int index)
    {
        GameObject prediccionElement = CreateUIGameObject($"Prediccion_{index}", parent.transform);

        // Fondo BLANCO para alta visibilidad
        Image elementBg = prediccionElement.AddComponent<Image>();
        elementBg.color = secondaryColor; // BLANCO PURO
        elementBg.sprite = roundedRectSprite;
        elementBg.type = Image.Type.Sliced;

        RectTransform elementRect = prediccionElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(470, 50);

        // Indicador lateral colorido más visible
        GameObject indicator = CreateUIGameObject("Indicator", prediccionElement.transform);

        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = GetPriorityColor(index);

        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.sizeDelta = new Vector2(8, 40); // Más ancho para ser más visible
        indicatorRect.anchoredPosition = new Vector2(-225, 0);

        // Texto de predicción con mejor contraste
        GameObject textObj = CreateUIGameObject("Text", prediccionElement.transform);

        Text text = textObj.AddComponent<Text>();
        text.text = prediccion;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.color = textDarkColor; // Texto OSCURO sobre fondo blanco
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(420, 40);
        textRect.anchoredPosition = new Vector2(5, 0);
    }

    void SetupButtonEvents()
    {
        if (kpisButton != null && prediccionesButton != null)
        {
            kpisButton.onClick.RemoveAllListeners();
            prediccionesButton.onClick.RemoveAllListeners();

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
        }
    }

    void AddModernButtonHoverEffects(Button button, Color originalColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = originalColor;
        colors.highlightedColor = new Color(originalColor.r * 0.9f, originalColor.g * 0.9f, originalColor.b * 0.9f, 1f);
        colors.pressedColor = new Color(originalColor.r * 0.8f, originalColor.g * 0.8f, originalColor.b * 0.8f, 1f);
        colors.disabledColor = new Color(originalColor.r * 0.5f, originalColor.g * 0.5f, originalColor.b * 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }

    Color GetModernProgressBarColor(float value)
    {
        if (value >= 90f) return accentColor;        // Verde para excelente (92%, 100%)
        else if (value >= 75f) return warningColor;  // Naranja para bueno (77%, 83%, 81%)
        else return dangerColor;                     // Rojo para bajo
    }

    Color GetPriorityColor(int index)
    {
        Color[] priorityColors = { dangerColor, warningColor, accentColor, primaryColor };
        return priorityColors[index % priorityColors.Length];
    }

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