using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IndustrialDashboard : MonoBehaviour
{



    [Header("Referencias de UI - Se crean autom�ticamente")]
    private Canvas mainCanvas;

    [Header("Configuración de Colores - EXACTOS de la imagen")]
    public Color primaryColor = new Color(0.2f, 0.6f, 1f, 1f);        // Azul KPIs
    public Color secondaryColor = new Color(1f, 1f, 1f, 1f);          // Fondo blanco puro
    public Color accentColor = new Color(0.0f, 0.8f, 0.4f, 1f);       // Verde
    public Color warningColor = new Color(1f, 0.65f, 0f, 1f);         // Naranja
    public Color dangerColor = new Color(0.9f, 0.3f, 0.3f, 1f);       // Rojo
    public Color textDarkColor = new Color(0.2f, 0.2f, 0.2f, 1f);     // Texto oscuro
    public Color progressBgColor = new Color(0.88f, 0.88f, 0.88f, 1f);// Fondo barra gris claro
    public Color lightGrayColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Bot�n pill
    public Color panelGrayColor = new Color(0.45f, 0.45f, 0.45f, 1f); // Fondo panel gris

    // --- Estilo Apple para el panel de detalle ---
    private Transform detailContentRoot; // contenedor donde pintamos el contenido din�mico
    public Color subtleDivider = new Color(0.92f, 0.92f, 0.95f, 1f);
    public Color labelMuted = new Color(0.45f, 0.45f, 0.5f, 1f);
    public Color pillBg = new Color(0.96f, 0.96f, 0.98f, 1f);

    private GameObject kpisPanel;
    private GameObject prediccionesPanel;
    private Button kpisButton;
    private Button prediccionesButton;
    private GameObject mainPanel;

    // === Panel lateral de detalle ===
    private GameObject detailPanel;
    private Text detailTitleText;
    private Text detailAreaText;
    private Text detailBodyText; // fallback de texto simple

    [Header("Configuraci�n de Comportamiento")]
    public bool showOnStart = false;
    public bool enableDebugMode = true;

    public event System.Action OnHidden;

    // === API para detalle din�mico ===
    // Asigna esto desde otro script para generar el texto del detalle por (�rea, KPI).
    // Si es null, se usa un texto por defecto.
    public Func<string, KPIData, string> ProvideDetail;

    // Datos del Dashboard
    [Header("Datos del Dashboard")]
    public List<KPIData> kpisData = new List<KPIData>
    {
        new KPIData("Selecciona un �rea", 0f, ""),
        new KPIData("para ver los KPIs", 0f, ""),
        new KPIData("correspondientes", 0f, "")
    };

    public List<string> prediccionesData = new List<string>
    {
        "Selecciona un �rea para ver predicciones espec�ficas",
        "Las predicciones se generar�n autom�ticamente",
        "basadas en los datos de rendimiento del �rea"
    };

    // Variables de control
    private string currentAreaName = "Ninguna";
    private GameObject areaTitle;

    // Sprites para bordes redondeados
    private Sprite roundedRectSprite;
    private Sprite veryRoundedSprite; // Para el panel principal m�s redondeado

    void Start()
    {
        Debug.Log("Iniciando Industrial Dashboard...");

        CreateRoundedSprites();
        CreateMainCanvas();
        SetupMainInterface();

        // Crear panel lateral de detalle (oculto de inicio)
        CreateDetailPanel();
        HideDetailPanel();

        if (!showOnStart)
        {
            HideInterface();
            Debug.Log("? Dashboard iniciado pero ocultado (esperando selecci�n de �rea)");
        }
    }

    void CreateRoundedSprites()
    {
        roundedRectSprite = CreateRoundedRectSprite(6);
        veryRoundedSprite = CreateRoundedRectSprite(16); // Panel principal m�s redondeado
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
        bool isInCorner = (x < radius && y < radius) ||
                         (x >= width - radius && y < radius) ||
                         (x < radius && y >= height - radius) ||
                         (x >= width - radius && y >= height - radius);

        if (!isInCorner) return true;

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

        OnHidden?.Invoke();
    }

    public void ShowInterface()
    {
        if (mainCanvas != null)
        {
            mainCanvas.gameObject.SetActive(true);
            Debug.Log("? Dashboard mostrado");
        }
    }

    public void UpdateWithAreaData(string areaName, List<KPIData> newKPIs, List<string> newPredictions)
    {
        currentAreaName = areaName;
        kpisData = newKPIs ?? kpisData;
        prediccionesData = newPredictions ?? prediccionesData;

        UpdateAreaTitle();

        // Al cambiar de �rea, cerramos el panel de detalle para no mezclar contextos.
        HideDetailPanel();

        RefreshPanels();
        ShowInterface();

        Debug.Log($"? Dashboard actualizado con datos de: {areaName}");
    }

    void UpdateAreaTitle()
    {
        if (areaTitle != null)
        {
            Text titleText = areaTitle.GetComponent<Text>();
            if (titleText != null) titleText.text = $"�REA: {currentAreaName.ToUpper()}";
        }
    }

    void RefreshPanels()
    {
        if (kpisPanel != null) DestroyImmediate(kpisPanel);
        if (prediccionesPanel != null) DestroyImmediate(prediccionesPanel);

        CreatePanels();
        SetupButtonEvents();
    }

    GameObject CreateUIGameObject(string name, Transform parent = null)
    {
        GameObject obj = new GameObject(name);
        if (parent != null) obj.transform.SetParent(parent, false);
        if (obj.GetComponent<RectTransform>() == null) obj.AddComponent<RectTransform>();
        return obj;
    }

    // Canvas
    void CreateMainCanvas()
    {
        GameObject existing = GameObject.Find("UI_Canvas");
        if (existing != null)
        {
            mainCanvas = existing.GetComponent<Canvas>();
        }
        else
        {
            GameObject canvasObj = new GameObject("UI_Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var graphicRaycaster = canvasObj.AddComponent<GraphicRaycaster>();
            // CR�TICO: Hacer que solo intercepte elementos espec�ficos
            graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            Debug.Log("? Canvas principal creado autom�ticamente (UI_Canvas)");
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("? EventSystem creado autom�ticamente");
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
        panelImage.color = panelGrayColor;
        panelImage.sprite = veryRoundedSprite;
        panelImage.type = Image.Type.Sliced;
        // CR�TICO: El panel de fondo NO debe interceptar clicks
        panelImage.raycastTarget = false;

        RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(540, 700);
        panelRect.anchoredPosition = new Vector2(0, 0);
    }

    void CreateAreaTitle()
    {
        areaTitle = CreateUIGameObject("AreaTitle", mainPanel.transform);
        Text titleText = areaTitle.AddComponent<Text>();
        titleText.text = $"�REA: {currentAreaName.ToUpper()}";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = primaryColor;
        titleText.alignment = TextAnchor.MiddleLeft;

        RectTransform titleRect = areaTitle.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(350, 35);
        titleRect.anchoredPosition = new Vector2(-70, 315);

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
        closeRect.anchoredPosition = new Vector2(200, 315);

        GameObject closeTextObj = CreateUIGameObject("Text", closeButtonObj.transform);
        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.text = "? CERRAR";
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 11;
        closeText.fontStyle = FontStyle.Bold;
        closeText.color = textDarkColor;
        closeText.alignment = TextAnchor.MiddleCenter;

        RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.sizeDelta = new Vector2(90, 30);
        closeTextRect.anchoredPosition = Vector2.zero;

        closeButton.onClick.AddListener(() => {
            AreaManager areaManager = FindFirstObjectByType<AreaManager>();
            if (areaManager != null)
            {
                areaManager.CloseDashboard();
            }
            else
            {
                HideInterface();
            }
        });

        AddModernButtonHoverEffects(closeButton, lightGrayColor);
    }

    void CreateMainButtons()
    {
        // KPIs
        GameObject kpisButtonObj = CreateUIGameObject("KPIs_Button", mainPanel.transform);
        kpisButton = kpisButtonObj.AddComponent<Button>();
        Image kpisButtonImage = kpisButtonObj.AddComponent<Image>();
        kpisButtonImage.color = primaryColor;
        kpisButtonImage.sprite = roundedRectSprite;
        kpisButtonImage.type = Image.Type.Sliced;

        RectTransform kpisRect = kpisButtonObj.GetComponent<RectTransform>();
        kpisRect.sizeDelta = new Vector2(240, 45);
        kpisRect.anchoredPosition = new Vector2(-125, 250);

        GameObject kpisTextObj = CreateUIGameObject("Text", kpisButtonObj.transform);
        Text kpisText = kpisTextObj.AddComponent<Text>();
        kpisText.text = "KPIs";
        kpisText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        kpisText.fontSize = 16;
        kpisText.fontStyle = FontStyle.Bold;
        kpisText.color = Color.white;
        kpisText.alignment = TextAnchor.MiddleCenter;
        kpisTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 45);

        // Predicciones
        GameObject prediccionesButtonObj = CreateUIGameObject("Predicciones_Button", mainPanel.transform);
        prediccionesButton = prediccionesButtonObj.AddComponent<Button>();
        Image prediccionesButtonImage = prediccionesButtonObj.AddComponent<Image>();
        prediccionesButtonImage.color = accentColor;
        prediccionesButtonImage.sprite = roundedRectSprite;
        prediccionesButtonImage.type = Image.Type.Sliced;

        RectTransform prediccionesRect = prediccionesButtonObj.GetComponent<RectTransform>();
        prediccionesRect.sizeDelta = new Vector2(240, 45);
        prediccionesRect.anchoredPosition = new Vector2(125, 250);

        GameObject prediccionesTextObj = CreateUIGameObject("Text", prediccionesButtonObj.transform);
        Text prediccionesText = prediccionesTextObj.AddComponent<Text>();
        prediccionesText.text = "PREDICCIONES";
        prediccionesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prediccionesText.fontSize = 13;
        prediccionesText.fontStyle = FontStyle.Bold;
        prediccionesText.color = Color.white;
        prediccionesText.alignment = TextAnchor.MiddleCenter;
        prediccionesTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 45);

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
        panelRect.sizeDelta = new Vector2(500, 380);
        panelRect.anchoredPosition = new Vector2(0, 10);

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

        // KPIs que llegan desde AreaManager (incluye Overall Result)
        foreach (KPIData kpi in kpisData) CreateModernKPIElement(content, kpi);

        // Solo agrega "Overall Result" si NO viene en kpisData
        if (!kpisData.Exists(k => string.Equals(k.name, "Overall Result", StringComparison.OrdinalIgnoreCase)))
            CreateOverallResultElement(content);

        CreateImprovedAlertBox();
    }

    void CreateModernKPIElement(GameObject parent, KPIData kpi)
    {
        GameObject kpiElement = CreateUIGameObject($"KPI_{kpi.name.Replace(" ", "_")}", parent.transform);

        RectTransform elementRect = kpiElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(500, 48);

        HorizontalLayoutGroup horizontalLayout = kpiElement.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(10, 10, 10, 10);
        horizontalLayout.spacing = 8f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Nombre
        GameObject nameObj = CreateUIGameObject("Name", kpiElement.transform);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = kpi.name;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 14;
        nameText.color = textDarkColor;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 28);

        // Barra de progreso (180 px)
        GameObject progressContainer = CreateUIGameObject("ProgressContainer", kpiElement.transform);
        RectTransform containerRect = progressContainer.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(180, 20);

        GameObject progressBarBg = CreateUIGameObject("ProgressBarBg", progressContainer.transform);
        Image bgImage = progressBarBg.AddComponent<Image>();
        bgImage.color = progressBgColor;
        bgImage.sprite = roundedRectSprite;
        bgImage.type = Image.Type.Sliced;

        RectTransform bgRect = progressBarBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(180, 12);
        bgRect.anchoredPosition = Vector2.zero;

        GameObject progressBarFill = CreateUIGameObject("ProgressBarFill", progressBarBg.transform);
        Image fillImage = progressBarFill.AddComponent<Image>();
        fillImage.color = GetModernProgressBarColor(kpi.value);
        fillImage.sprite = roundedRectSprite;
        fillImage.type = Image.Type.Sliced;

        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        float fillWidth = 180 * Mathf.Clamp01(kpi.value / 100f);
        fillRect.sizeDelta = new Vector2(fillWidth, 12);
        fillRect.anchoredPosition = new Vector2(-(180 - fillWidth) / 2f, 0);

        // Valor %
        GameObject valueObj = CreateUIGameObject("Value", kpiElement.transform);
        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = kpi.value > 0 ? $"{kpi.value:F0}%" : "";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = textDarkColor;
        valueText.alignment = TextAnchor.MiddleRight;
        valueObj.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 28);

        // En IndustrialDashboard.cs, en el m�todo CreateModernKPIElement, 
        // reemplaza la secci�n del bot�n "Ver detalle":

        // Bot�n detalle - AGREGAR DESPU�S DE LA L�NEA EXISTENTE
        GameObject detailBtnObj = CreateUIGameObject("Btn_VerDetalle", kpiElement.transform);
        var detailBtn = detailBtnObj.AddComponent<Button>();
        var detailImg = detailBtnObj.AddComponent<Image>();
        detailImg.sprite = roundedRectSprite;
        detailImg.type = Image.Type.Sliced;
        detailImg.color = lightGrayColor;
        detailImg.raycastTarget = true;     // ? importante

        detailBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 28);

        var detailTxtObj = CreateUIGameObject("Text", detailBtnObj.transform);
        var detailTxt = detailTxtObj.AddComponent<Text>();
        detailTxt.text = "Ver detalle �";
        detailTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailTxt.fontSize = 12;
        detailTxt.color = textDarkColor;
        detailTxt.alignment = TextAnchor.MiddleCenter;
        detailTxt.raycastTarget = false;    // ? el texto NO intercepta clics
        detailTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 28);

        // Hover (ya lo tienes implementado):
        AddModernButtonHoverEffects(detailBtn, lightGrayColor);

        // Click handler
        detailBtn.onClick.AddListener(() => {
            if (enableDebugMode) Debug.Log($"Click en Ver detalle para: {kpi.name}");
            ShowKPIDetail(kpi);
        });

        // CR�TICO: Hacer que el bot�n NO bloquee clicks al mundo 3D
        // (esto lo maneja el m�todo IsPointerOverBlockingUI actualizado)
    }

    void CreateOverallResultElement(GameObject parent)
    {
        GameObject overallElement = CreateUIGameObject("OverallResult", parent.transform);

        RectTransform elementRect = overallElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(500, 48);

        HorizontalLayoutGroup horizontalLayout = overallElement.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.padding = new RectOffset(10, 10, 10, 10);
        horizontalLayout.spacing = 8f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Nombre
        GameObject nameObj = CreateUIGameObject("Name", overallElement.transform);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.text = "Overall Result";
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 14;
        nameText.color = textDarkColor;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 28);

        // Barra (180px)
        GameObject progressContainer = CreateUIGameObject("ProgressContainer", overallElement.transform);
        RectTransform containerRect = progressContainer.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(180, 20);

        GameObject progressBarBg = CreateUIGameObject("ProgressBarBg", progressContainer.transform);
        Image bgImage = progressBarBg.AddComponent<Image>();
        bgImage.color = progressBgColor;
        bgImage.sprite = roundedRectSprite;
        bgImage.type = Image.Type.Sliced;
        RectTransform bgRect = progressBarBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(180, 12);

        GameObject progressBarFill = CreateUIGameObject("ProgressBarFill", progressBarBg.transform);
        Image fillImage = progressBarFill.AddComponent<Image>();
        fillImage.color = GetModernProgressBarColor(92f);
        fillImage.sprite = roundedRectSprite;
        fillImage.type = Image.Type.Sliced;
        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        float fillWidth = 180 * 0.92f;
        fillRect.sizeDelta = new Vector2(fillWidth, 12);
        fillRect.anchoredPosition = new Vector2(-(180 - fillWidth) / 2f, 0);

        // Valor
        GameObject valueObj = CreateUIGameObject("Value", overallElement.transform);
        Text valueText = valueObj.AddComponent<Text>();
        valueText.text = "92%";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 14;
        valueText.fontStyle = FontStyle.Bold;
        valueText.color = textDarkColor;
        valueText.alignment = TextAnchor.MiddleRight;
        valueObj.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 28);

        // Bot�n detalle
        GameObject detailBtnObj = CreateUIGameObject("Btn_VerDetalle", overallElement.transform);
        Button detailBtn = detailBtnObj.AddComponent<Button>();
        Image detailBtnImg = detailBtnObj.AddComponent<Image>();
        detailBtnImg.color = lightGrayColor;
        detailBtnImg.sprite = roundedRectSprite;
        detailBtnImg.type = Image.Type.Sliced;
        detailBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 28);

        GameObject detailTxtObj = CreateUIGameObject("Text", detailBtnObj.transform);
        Text detailTxt = detailTxtObj.AddComponent<Text>();
        detailTxt.text = "Ver detalle �";
        detailTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailTxt.fontSize = 12;
        detailTxt.color = textDarkColor;
        detailTxt.alignment = TextAnchor.MiddleCenter;
        detailTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 28);

        AddModernButtonHoverEffects(detailBtn, lightGrayColor);
        detailBtn.onClick.AddListener(() => ShowKPIDetail(new KPIData("Overall Result", 92f, "%")));
    }

    void CreateTrueRoundedProgressBar(GameObject parent, float value)
    {
        GameObject progressContainer = CreateUIGameObject("ProgressContainer", parent.transform);

        RectTransform containerRect = progressContainer.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(200, 20);

        GameObject progressBarBg = CreateUIGameObject("ProgressBarBg", progressContainer.transform);
        Image bgImage = progressBarBg.AddComponent<Image>();
        bgImage.color = progressBgColor;
        bgImage.sprite = roundedRectSprite;
        bgImage.type = Image.Type.Sliced;

        RectTransform bgRect = progressBarBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(200, 12);
        bgRect.anchoredPosition = new Vector2(0, 0);

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
        GameObject alertBox = CreateUIGameObject("AlertBox", mainPanel.transform);

        Image alertBg = alertBox.AddComponent<Image>();
        alertBg.color = secondaryColor;
        alertBg.sprite = roundedRectSprite;
        alertBg.type = Image.Type.Sliced;

        RectTransform alertRect = alertBox.GetComponent<RectTransform>();
        alertRect.sizeDelta = new Vector2(500, 65);
        alertRect.anchoredPosition = new Vector2(0, -285);

        VerticalLayoutGroup verticalLayout = alertBox.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(20, 20, 12, 12);
        verticalLayout.spacing = 6f;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = false;

        GameObject topRow = CreateUIGameObject("TopRow", alertBox.transform);
        RectTransform topRowRect = topRow.GetComponent<RectTransform>();
        topRowRect.sizeDelta = new Vector2(460, 28);

        HorizontalLayoutGroup topRowLayout = topRow.AddComponent<HorizontalLayoutGroup>();
        topRowLayout.spacing = 12f;
        topRowLayout.childControlWidth = false;
        topRowLayout.childControlHeight = true;
        topRowLayout.childAlignment = TextAnchor.MiddleLeft;

        GameObject iconObj = CreateUIGameObject("Icon", topRow.transform);
        Text iconText = iconObj.AddComponent<Text>();
        iconText.text = "?";
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 22;
        iconText.color = dangerColor;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(35, 28);

        GameObject alertTextObj = CreateUIGameObject("AlertText", topRow.transform);
        Text alertText = alertTextObj.AddComponent<Text>();
        alertText.text = "Delivery bajo riesgo";
        alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        alertText.fontSize = 15;
        alertText.fontStyle = FontStyle.Bold;
        alertText.color = textDarkColor;
        alertText.alignment = TextAnchor.MiddleLeft;
        alertTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 28);

        GameObject bottomRow = CreateUIGameObject("BottomRow", alertBox.transform);
        RectTransform bottomRowRect = bottomRow.GetComponent<RectTransform>();
        bottomRowRect.sizeDelta = new Vector2(460, 22);

        GameObject recomendacionObj = CreateUIGameObject("Recomendacion", bottomRow.transform);
        Text recomendacionText = recomendacionObj.AddComponent<Text>();
        recomendacionText.text = "� Optimizaci�n recomendada para mejorar el rendimiento del �rea";
        recomendacionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recomendacionText.fontSize = 12;
        recomendacionText.color = accentColor;
        recomendacionText.alignment = TextAnchor.MiddleLeft;

        RectTransform recomendacionRect = recomendacionObj.GetComponent<RectTransform>();
        recomendacionRect.sizeDelta = new Vector2(460, 22);
        recomendacionRect.anchoredPosition = new Vector2(47, 0);
    }

    void CreatePrediccionesPanel()
    {
        prediccionesPanel = CreateUIGameObject("Predicciones_Panel", mainPanel.transform);

        RectTransform panelRect = prediccionesPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 380);
        panelRect.anchoredPosition = new Vector2(0, 10);

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

        Image elementBg = prediccionElement.AddComponent<Image>();
        elementBg.color = secondaryColor;
        elementBg.sprite = roundedRectSprite;
        elementBg.type = Image.Type.Sliced;

        RectTransform elementRect = prediccionElement.GetComponent<RectTransform>();
        elementRect.sizeDelta = new Vector2(470, 50);

        GameObject indicator = CreateUIGameObject("Indicator", prediccionElement.transform);
        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = GetPriorityColor(index);
        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.sizeDelta = new Vector2(8, 40);
        indicatorRect.anchoredPosition = new Vector2(-225, 0);

        GameObject textObj = CreateUIGameObject("Text", prediccionElement.transform);
        Text text = textObj.AddComponent<Text>();
        text.text = prediccion;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.color = textDarkColor;
        text.alignment = TextAnchor.MiddleLeft;
        textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 40);
        textObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(5, 0);
    }

    void SetupButtonEvents()
    {
        if (kpisButton != null && prediccionesButton != null)
        {
            kpisButton.onClick.RemoveAllListeners();
            prediccionesButton.onClick.RemoveAllListeners();

            kpisButton.onClick.AddListener(() => {
                kpisPanel.SetActive(true);
                prediccionesPanel.SetActive(false);
            });

            prediccionesButton.onClick.AddListener(() => {
                prediccionesPanel.SetActive(true);
                kpisPanel.SetActive(false);
            });
        }
    }

    // En IndustrialDashboard.cs, reemplaza el m�todo AddModernButtonHoverEffects:

    void AddModernButtonHoverEffects(Button button, Color originalColor)
    {
        if (button == null) return;

        // Asegurar que tenga sprite (cr�tico para el hover)
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null && buttonImage.sprite == null)
        {
            buttonImage.sprite = roundedRectSprite;
            buttonImage.type = Image.Type.Sliced;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = originalColor;

        // Hover m�s visible
        colors.highlightedColor = new Color(
            originalColor.r * 0.85f,
            originalColor.g * 0.85f,
            originalColor.b * 0.85f,
            1f
        );

        // Click m�s oscuro
        colors.pressedColor = new Color(
            originalColor.r * 0.70f,
            originalColor.g * 0.70f,
            originalColor.b * 0.70f,
            1f
        );

        colors.disabledColor = new Color(
            originalColor.r * 0.5f,
            originalColor.g * 0.5f,
            originalColor.b * 0.5f,
            0.5f
        );

        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.15f; // Transici�n m�s suave

        button.colors = colors;

        // Asegurar que sea interactuable
        button.interactable = true;

        if (enableDebugMode)
            Debug.Log($"Hover effects aplicados a bot�n: {button.name}");
    }

    Color GetModernProgressBarColor(float value)
    {
        if (value >= 90f) return accentColor;
        else if (value >= 75f) return warningColor;
        else return dangerColor;
    }

    Color GetPriorityColor(int index)
    {
        Color[] priorityColors = { dangerColor, warningColor, accentColor, primaryColor };
        return priorityColors[index % priorityColors.Length];
    }

    public void ForceUpdate()
    {
        if (mainCanvas != null && mainCanvas.gameObject.activeInHierarchy) RefreshPanels();
    }

    // =========================
    // ====== DETALLE KPI ======
    // =========================

    void CreateDetailPanel()
    {
        // Panel padre (al lado derecho del MainPanel)
        detailPanel = CreateUIGameObject("DetailPanel", mainCanvas.transform);

        Image bg = detailPanel.AddComponent<Image>();
        bg.color = secondaryColor;             // fondo blanco
        bg.sprite = veryRoundedSprite;
        bg.type = Image.Type.Sliced;

        RectTransform rect = detailPanel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420, 700);
        // MainPanel ancho 540 ? mitad = 270; margen 30 px; mitad del detail 210
        rect.anchoredPosition = new Vector2(270 + 30 + 210, 0);

        // T�tulo
        GameObject titleObj = CreateUIGameObject("Title", detailPanel.transform);
        detailTitleText = titleObj.AddComponent<Text>();
        detailTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailTitleText.fontSize = 20;
        detailTitleText.fontStyle = FontStyle.Bold;
        detailTitleText.color = textDarkColor;
        detailTitleText.alignment = TextAnchor.MiddleLeft;
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 34);
        titleObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, 310);

        // Subt�tulo (�rea)
        GameObject areaObj = CreateUIGameObject("Area", detailPanel.transform);
        detailAreaText = areaObj.AddComponent<Text>();
        detailAreaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailAreaText.fontSize = 12;
        detailAreaText.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        detailAreaText.alignment = TextAnchor.MiddleLeft;
        areaObj.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 20);
        areaObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, 285);

        // Contenedor con scroll
        GameObject scrollObj = CreateUIGameObject("ScrollView", detailPanel.transform);
        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.98f, 0.98f, 0.98f, 1f);
        scrollBg.sprite = roundedRectSprite;
        scrollBg.type = Image.Type.Sliced;
        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(380, 560);
        scrollRect.anchoredPosition = new Vector2(0, -30);

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.vertical = true;
        sr.scrollSensitivity = 24f;

        // Viewport
        GameObject viewport = CreateUIGameObject("Viewport", scrollObj.transform);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.white;

        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0, 0);
        vpRect.anchorMax = new Vector2(1, 1);
        vpRect.offsetMin = new Vector2(10, 10);
        vpRect.offsetMax = new Vector2(-10, -10);

        // === Scrollbar vertical tipo Apple (siempre visible cuando hay overflow) ===
        GameObject vbarObj = CreateUIGameObject("VerticalScrollbar", scrollObj.transform);

        // Track fino (semi-transparente)
        Image vbarTrack = vbarObj.AddComponent<Image>();
        vbarTrack.color = new Color(0f, 0f, 0f, 0.06f); // leve sombra, estilo iOS/macOS
        vbarTrack.sprite = roundedRectSprite;
        vbarTrack.type = Image.Type.Sliced;

        RectTransform vbarRt = vbarObj.GetComponent<RectTransform>();
        vbarRt.anchorMin = new Vector2(1f, 0f);
        vbarRt.anchorMax = new Vector2(1f, 1f);
        vbarRt.pivot = new Vector2(1f, 0.5f);
        vbarRt.sizeDelta = new Vector2(6f, 0f);     // barra delgadita
        vbarRt.anchoredPosition = Vector2.zero;

        // Sliding area + handle
        GameObject slidingArea = CreateUIGameObject("SlidingArea", vbarObj.transform);
        RectTransform slidingRt = slidingArea.GetComponent<RectTransform>();
        slidingRt.anchorMin = new Vector2(0f, 0f);
        slidingRt.anchorMax = new Vector2(1f, 1f);
        slidingRt.offsetMin = Vector2.zero;
        slidingRt.offsetMax = Vector2.zero;

        GameObject handleObj = CreateUIGameObject("Handle", slidingArea.transform);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = new Color(0f, 0f, 0f, 0.18f); // �pill� oscuro sutil
        handleImg.sprite = roundedRectSprite;
        handleImg.type = Image.Type.Sliced;
        RectTransform handleRt = handleObj.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(1f, 1f);
        handleRt.offsetMin = new Vector2(1f, 2f);
        handleRt.offsetMax = new Vector2(-1f, -2f);

        Scrollbar vbar = vbarObj.AddComponent<Scrollbar>();
        vbar.direction = Scrollbar.Direction.BottomToTop;
        vbar.handleRect = handleRt;
        vbar.targetGraphic = handleImg;

        // Deja espacio a la derecha del viewport para que la barra no tape contenido
        vpRect.offsetMax = new Vector2(-16f, -10f);

        // Content
        GameObject content = CreateUIGameObject("Content", viewport.transform);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vLayout = content.AddComponent<VerticalLayoutGroup>();
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;   // permite que el Text tome su alto preferido
        vLayout.childForceExpandHeight = false;
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.spacing = 8f;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Guardar el root para construir UI din�mica
        detailContentRoot = content.transform;

        // Fallback de cuerpo (por si algo falla)
        GameObject bodyObj = CreateUIGameObject("Body", content.transform);
        detailBodyText = bodyObj.AddComponent<Text>();
        detailBodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailBodyText.fontSize = 14;
        detailBodyText.color = textDarkColor;
        detailBodyText.alignment = TextAnchor.UpperLeft;
        detailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailBodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);

        // Conectar ScrollRect
        sr.viewport = vpRect;
        sr.content = contentRt;
        sr.verticalScrollbar = vbar;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // Bot�n cerrar del panel de detalle (arriba derecha)
        GameObject closeBtn = CreateUIGameObject("CloseDetail", detailPanel.transform);
        Button close = closeBtn.AddComponent<Button>();
        Image closeImg = closeBtn.AddComponent<Image>();
        closeImg.color = lightGrayColor;
        closeImg.sprite = roundedRectSprite;
        closeImg.type = Image.Type.Sliced;
        RectTransform closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(90, 30);
        closeRt.anchoredPosition = new Vector2(150, 315);

        GameObject closeTxtObj = CreateUIGameObject("Text", closeBtn.transform);
        Text closeTxt = closeTxtObj.AddComponent<Text>();
        closeTxt.text = "Ocultar";
        closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeTxt.fontSize = 12;
        closeTxt.color = textDarkColor;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 30);

        AddModernButtonHoverEffects(close, lightGrayColor);
        close.onClick.AddListener(HideDetailPanel);
    }

    void ShowKPIDetail(KPIData kpi)
    {
        if (detailPanel == null) CreateDetailPanel();

        detailTitleText.text = kpi.name;
        detailAreaText.text = $"�rea: {currentAreaName}";

        // 1) texto �sem�ntico� (fallback) por si algo falla
        string body = null;
        if (ProvideDetail != null)
        {
            try { body = ProvideDetail.Invoke(currentAreaName, kpi); }
            catch (Exception e) { Debug.LogWarning($"ProvideDetail lanz� excepci�n: {e.Message}"); }
        }
        if (string.IsNullOrWhiteSpace(body)) body = GetDefaultDetailText(currentAreaName, kpi);

        // 2) UI rica estilo Apple
        BuildDetailUIRich(kpi, currentAreaName, body);

        detailPanel.SetActive(true);
    }

    void HideDetailPanel()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    string GetDefaultDetailText(string area, KPIData kpi)
    {
        return
            $"Resumen de {kpi.name}\n\n" +
            $"� �rea: {area}\n" +
            $"� Valor actual: {kpi.value:F1}{(string.IsNullOrEmpty(kpi.unit) ? "%" : kpi.unit)}\n\n" +
            $"Interpretaci�n:\n" +
            $"- 90�100: Excelente (verde)\n" +
            $"- 75�89: Aceptable (naranja)\n" +
            $"- <75: Riesgo (rojo)\n\n" +
            $"Recomendaciones:\n" +
            $"- Revise tendencias de las �ltimas 4 semanas\n" +
            $"- Identifique cuellos de botella\n" +
            $"- Genere acciones correctivas puntuales";
    }

    // ===============================
    // Apple-style Detail UI (din�mico)
    // ===============================

    void BuildDetailUIRich(KPIData kpi, string area, string semanticText)
    {
        ClearDetailDynamic();
        if (detailBodyText != null) detailBodyText.gameObject.SetActive(false); // ocultamos el fallback

        // --- C�lculos b�sicos ---
        float value = Mathf.Clamp(kpi.value, 0, 100);
        float target = GetTargetFor(kpi.name);
        float gap = value - target;
        string status;
        Color statusColor;
        GetStatusFor(value, target, out status, out statusColor);

        // --- Row de pills: Estado + Meta ---
        var pillsRow = CreateHRow("dyn_pillsRow", detailContentRoot, 8, TextAnchor.MiddleLeft);
        CreatePill(pillsRow.transform, status, statusColor, 110);
        CreatePill(pillsRow.transform, $"Meta: {target:F0}%", labelMuted, 100);

        // --- Metric strip: Actual / Meta / Brecha ---
        var metricsRow = CreateHRow("dyn_metricsRow", detailContentRoot, 10, TextAnchor.MiddleLeft);
        CreateMetricCard(metricsRow.transform, "Actual", $"{value:F0}%", 120);
        CreateMetricCard(metricsRow.transform, "Meta", $"{target:F0}%", 120);
        CreateMetricCard(metricsRow.transform, gap >= 0 ? "Sobre meta" : "Brecha", $"{gap:+0;-0;0}%", 140);

        CreateDivider(detailContentRoot);

        // --- Secci�n: Por qu� este valor ---
        CreateSectionTitle(detailContentRoot, "�Por qu� este valor?");
        foreach (var line in GetReasonsFor(kpi, value)) CreateBullet(detailContentRoot, line);

        CreateDivider(detailContentRoot);

        // --- Secci�n: Siguientes pasos ---
        CreateSectionTitle(detailContentRoot, "Siguientes pasos");
        foreach (var step in GetActionsFor(kpi, value)) CreateBullet(detailContentRoot, step);

        // --- Notas (del delegado) opcionales ---
        if (!string.IsNullOrWhiteSpace(semanticText))
        {
            CreateDivider(detailContentRoot);
            CreateSectionTitle(detailContentRoot, "Notas");
            CreateParagraph(detailContentRoot, semanticText);
        }
    }

    void ClearDetailDynamic()
    {
        if (detailContentRoot == null) return;
        for (int i = detailContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = detailContentRoot.GetChild(i);
            if (detailBodyText != null && child == detailBodyText.transform) continue; // conserva el fallback
            Destroy(child.gameObject);
        }
    }

    // ---------- UI atoms ----------
    GameObject CreateHRow(string name, Transform parent, float spacing, TextAnchor align)
    {
        var row = CreateUIGameObject(name, parent);
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 0);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(0, 0, 0, 0);
        h.spacing = spacing;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childAlignment = align;
        return row;
    }

    void CreatePill(Transform parent, string text, Color textColor, float width)
    {
        var pill = CreateUIGameObject("Pill", parent);
        var img = pill.AddComponent<Image>();
        img.color = pillBg;
        img.sprite = roundedRectSprite;
        img.type = Image.Type.Sliced;

        var rt = pill.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 26);

        var label = CreateUIGameObject("Text", pill.transform).AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 12;
        label.color = textColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 26);
    }

    void CreateMetricCard(Transform parent, string caption, string valueText, float width)
    {
        var card = CreateUIGameObject("Metric", parent);
        var img = card.AddComponent<Image>();
        img.color = secondaryColor;
        img.sprite = roundedRectSprite;
        img.type = Image.Type.Sliced;

        var rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 64);

        var v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 8, 8);
        v.spacing = 2f;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;

        var cap = CreateUIGameObject("Caption", card.transform).AddComponent<Text>();
        cap.text = caption.ToUpper();
        cap.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cap.fontSize = 10;
        cap.color = labelMuted;
        cap.alignment = TextAnchor.UpperLeft;

        var val = CreateUIGameObject("Value", card.transform).AddComponent<Text>();
        val.text = valueText;
        val.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        val.fontSize = 18;
        val.fontStyle = FontStyle.Bold;
        val.color = textDarkColor;
        val.alignment = TextAnchor.UpperLeft;
    }

    void CreateSectionTitle(Transform parent, string title)
    {
        var t = CreateUIGameObject("SectionTitle", parent).AddComponent<Text>();
        t.text = title;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 13;
        t.fontStyle = FontStyle.Bold;
        t.color = textDarkColor;
        t.alignment = TextAnchor.MiddleLeft;
        t.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 24);
    }

    void CreateBullet(Transform parent, string text)
    {
        var row = CreateHRow("Bullet", parent, 8, TextAnchor.UpperLeft);

        var dot = CreateUIGameObject("Dot", row.transform);
        var dotImg = dot.AddComponent<Image>();
        dotImg.color = labelMuted;
        dotImg.sprite = roundedRectSprite;
        dotImg.type = Image.Type.Sliced;
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(6, 6);

        var tx = CreateUIGameObject("Text", row.transform).AddComponent<Text>();
        tx.text = text;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tx.fontSize = 12;
        tx.color = textDarkColor;
        tx.alignment = TextAnchor.UpperLeft;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tx.GetComponent<RectTransform>().sizeDelta = new Vector2(330, 0);
    }

    void CreateParagraph(Transform parent, string text)
    {
        var tx = CreateUIGameObject("Paragraph", parent).AddComponent<Text>();
        tx.text = text;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tx.fontSize = 12;
        tx.color = textDarkColor;
        tx.alignment = TextAnchor.UpperLeft;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tx.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 0);
    }

    void CreateDivider(Transform parent)
    {
        var d = CreateUIGameObject("Divider", parent);
        var img = d.AddComponent<Image>();
        img.color = subtleDivider;
        d.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
    }

    // ---------- L�gica de negocio ligera (heur�sticas coherentes con AreaManager) ----------
    float GetTargetFor(string kpiName)
    {
        string n = (kpiName ?? "").ToLowerInvariant();
        if (n.Contains("delivery")) return 95f;
        if (n.Contains("quality")) return 90f;
        if (n.Contains("parts")) return 95f;
        if (n.Contains("process")) return 90f;
        if (n.Contains("training")) return 95f;
        if (n.Contains("manten") || n.Contains("mtto")) return 90f;
        if (n.Contains("overall")) return 90f;
        return 90f;
    }

    void GetStatusFor(float value, float target, out string status, out Color statusColor)
    {
        if (value >= target) { status = "Excelente"; statusColor = accentColor; }
        else if (value >= target - 10) { status = "En riesgo"; statusColor = warningColor; }
        else { status = "Cr�tico"; statusColor = dangerColor; }
    }

    IEnumerable<string> GetReasonsFor(KPIData kpi, float value)
    {
        string n = (kpi.name ?? "").ToLowerInvariant();
        if (n.Contains("delivery"))
        {
            yield return $"�rdenes planificadas: {GetEstOrders(value)}";
            yield return $"Incumplimientos detectados: {GetIncidences(value)}";
            yield return $"Retraso promedio: {GetDelayMins(value)} min";
            yield break;
        }
        if (n.Contains("quality"))
        {
            yield return $"PPM estimado: {GetPpm(value)}";
            yield return $"Top defectos: Falta de componente, Cosm�tico, Torque";
            yield return $"Retrabajos/d�a: {GetReworks(value)}";
            yield break;
        }
        if (n.Contains("parts"))
        {
            yield return $"SKU cr�ticos: {GetCriticalSkus(value)}";
            yield return $"Backorders: {GetBackorders(value)}";
            yield return $"Cobertura: {GetCoverageDays(value)} d�a(s)";
            yield break;
        }
        if (n.Contains("process"))
        {
            yield return $"OEE estimado: {GetOee(value)}%";
            yield return $"Cuellos de botella: {GetBottlenecksCount(value)}";
            yield return $"SMED pendientes: {GetSmedPend(value)}";
            yield break;
        }
        if (n.Contains("training"))
        {
            yield return $"Cursos cr�ticos vencidos: {GetExpiredCourses(value)}";
            yield return $"Polivalencia: {GetPolyvalence(value)}%";
            yield return $"Rotaci�n mes: {GetTurnover()}%";
            yield break;
        }
        if (n.Contains("manten") || n.Contains("mtto"))
        {
            yield return $"WO abiertas: {GetOpenWo(value)}";
            yield return $"Paros mayores: {GetMajorStops(value)}";
            yield return $"Pr�x. PM: {GetNextPMDate()}";
            yield break;
        }
        if (n.Contains("overall"))
        {
            yield return "Consolidado de KPIs principales (ponderados).";
            yield break;
        }
    }

    IEnumerable<string> GetActionsFor(KPIData kpi, float value)
    {
        string n = (kpi.name ?? "").ToLowerInvariant();
        if (n.Contains("delivery"))
        {
            yield return "Asegurar JIT en materiales cr�ticos.";
            yield return "Balanceo de l�nea y secuencia de empaque.";
            yield return "Monitoreo de transporte en tiempo real.";
            yield break;
        }
        if (n.Contains("quality"))
        {
            yield return "Gemba r�pido en estaciones con scrap.";
            yield return "5-Why al defecto principal; contenci�n si PPM > objetivo.";
            yield break;
        }
        if (n.Contains("parts"))
        {
            yield return "Escalonar compras urgentes y validar alternos.";
            yield return "Revisi�n de cobertura/consumo por SKU.";
            yield break;
        }
        if (n.Contains("process"))
        {
            yield return "Kaizen corto en cuello de botella principal.";
            yield return "Plan SMED para cambios frecuentes.";
            yield break;
        }
        if (n.Contains("training"))
        {
            yield return "Reentrenar est�ndar de trabajo en estaciones clave.";
            yield return "Plan de cierre para cursos cr�ticos vencidos.";
            yield break;
        }
        if (n.Contains("manten") || n.Contains("mtto"))
        {
            yield return "Cerrar WO con >72h abiertas.";
            yield return "Asegurar refacciones de falla repetitiva.";
            yield break;
        }
        if (n.Contains("overall"))
        {
            yield return "Foco en la palanca m�s d�bil para +5 pts/2 semanas.";
            yield break;
        }
    }

    // --- peque�as heur�sticas para las secciones (coherentes con AreaManager) ---
    int GetEstOrders(float delivery) => Mathf.Clamp(Mathf.RoundToInt(50f * (delivery / 100f) + 5), 5, 60);
    int GetIncidences(float delivery) => delivery < 50 ? 5 : (delivery < 80 ? 2 : 0);
    int GetDelayMins(float delivery) => delivery < 50 ? 35 : (delivery < 80 ? 12 : 3);
    int GetPpm(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) * 120f), 0, 12000);
    int GetReworks(float quality) => Mathf.Clamp(Mathf.RoundToInt((100f - quality) / 5f), 0, 6);
    int GetCriticalSkus(float parts) => Mathf.Clamp(Mathf.RoundToInt((100f - parts) / 12f), 0, 8);
    int GetBackorders(float parts) => Mathf.Clamp(Mathf.RoundToInt((100f - parts) / 20f), 0, 5);
    int GetCoverageDays(float parts) => Mathf.Clamp(Mathf.RoundToInt(parts / 20f), 0, 7);
    int GetOee(float pm) => Mathf.Clamp(Mathf.RoundToInt(pm * 0.85f), 20, 100);
    int GetBottlenecksCount(float pm) => pm < 60 ? 2 : (pm < 85 ? 1 : 0);
    int GetSmedPend(float pm) => pm < 80 ? 3 : 1;
    int GetExpiredCourses(float t) => t < 70 ? 4 : (t < 90 ? 1 : 0);
    int GetPolyvalence(float t) => Mathf.Clamp(Mathf.RoundToInt(t * 0.6f), 30, 90);
    int GetTurnover() => 3;
    int GetOpenWo(float mtto) => mtto < 60 ? 7 : (mtto < 90 ? 3 : 1);
    int GetMajorStops(float mtto) => mtto < 60 ? 2 : 0;
    string GetNextPMDate() => System.DateTime.Now.AddDays(3).ToString("dd/MM");
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