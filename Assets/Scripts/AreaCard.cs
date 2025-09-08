using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AreaCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Configuración del Área")]
    public string areaName;
    public AreaData areaData;

    [Header("Referencias UI - Se crean automáticamente")]
    private Canvas cardCanvas;
    private GameObject cardPanel;
    private Text areaNameText;
    private Text overallResultText;
    private Image backgroundImage;
    private Image shadowImage;

    [Header("Configuración Visual")]
    public float cardWidth = 900f;
    public float cardHeight = 450f;
    public float elevationHeight = 3f;
    public Vector3 cardOffset = new Vector3(0, 25, 0);

    [Header("Animación")]
    public float hoverScale = 1.05f;
    public float animationSpeed = 8f;
    public float floatAmplitude = 0.3f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isHovering = false;
    private Camera playerCamera;

    // Colores según el estado
    private Color optimusColor = new Color(0.2f, 0.8f, 0.3f, 1f);
    private Color healthyColor = new Color(0.1f, 0.6f, 0.9f, 1f);
    private Color sickColor = new Color(1f, 0.7f, 0.1f, 1f);
    private Color highRiskColor = new Color(0.95f, 0.3f, 0.3f, 1f);

    void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<Camera>();

        InitializeAreaData();
        CreateFloatingCard();
        SetupAreaCollider();

        originalScale = cardPanel.transform.localScale;
        originalPosition = cardPanel.transform.position;
    }

    void SetupAreaCollider()
    {
        Collider areaCollider = GetComponent<Collider>();
        if (areaCollider == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(15f, 8f, 15f);
            boxCollider.center = new Vector3(0, 4f, 0);
            Debug.Log($"✓ BoxCollider agregado automáticamente a {areaName}");
        }
        else
        {
            Debug.Log($"✓ Collider existente encontrado en {areaName}");
        }
    }

    void InitializeAreaData()
    {
        switch (areaName.ToUpper())
        {
            case "AT HONDA":
            case "ATHONDA":
            case "AREA_ATHONDA":
                areaData = new AreaData
                {
                    areaName = "AT HONDA",
                    delivery = 100f,
                    quality = 83f,
                    parts = 100f,
                    processManufacturing = 100f,
                    trainingDNA = 100f,
                    mtto = 100f,
                    overallResult = 95f
                };
                break;

            case "VCT L4":
            case "VCTL4":
            case "AREA_VCTL4":
                areaData = new AreaData
                {
                    areaName = "VCT L4",
                    delivery = 77f,
                    quality = 83f,
                    parts = 100f,
                    processManufacturing = 100f,
                    trainingDNA = 81f,
                    mtto = 100f,
                    overallResult = 92f
                };
                break;

            case "BUZZER L2":
            case "BUZZERL2":
            case "AREA_BUZZERL2":
                areaData = new AreaData
                {
                    areaName = "BUZZER L2",
                    delivery = 91f,
                    quality = 83f,
                    parts = 81f,
                    processManufacturing = 89f,
                    trainingDNA = 62f,
                    mtto = 100f,
                    overallResult = 73f
                };
                break;

            case "VB L1":
            case "VBL1":
            case "AREA_VBL1":
                areaData = new AreaData
                {
                    areaName = "VB L1",
                    delivery = 29f,
                    quality = 83f,
                    parts = 100f,
                    processManufacturing = 32f,
                    trainingDNA = 100f,
                    mtto = 47f,
                    overallResult = 49f
                };
                break;

            default:
                Debug.LogWarning($"Área no reconocida: {areaName}. Usando datos por defecto.");
                areaData = new AreaData
                {
                    areaName = areaName,
                    delivery = 75f,
                    quality = 80f,
                    parts = 85f,
                    processManufacturing = 70f,
                    trainingDNA = 90f,
                    mtto = 88f,
                    overallResult = 81f
                };
                break;
        }
    }

    void CreateFloatingCard()
    {
        // Crear Canvas para la tarjeta
        GameObject canvasObj = new GameObject($"Canvas_{areaData.areaName}");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = cardOffset;

        cardCanvas = canvasObj.AddComponent<Canvas>();
        cardCanvas.renderMode = RenderMode.WorldSpace;
        cardCanvas.sortingOrder = 100;

        // ESCALA MÁS GRANDE para que el texto se vea mejor
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        canvasRect.localScale = Vector3.one * 0.02f; // Escala aumentada de 0.015f a 0.02f

        canvasObj.AddComponent<GraphicRaycaster>();

        // Crear sombra PRIMERO (atrás)
        CreateCardShadow(canvasObj);

        // Panel principal de la tarjeta
        cardPanel = new GameObject("CardPanel");
        cardPanel.transform.SetParent(canvasObj.transform, false);

        backgroundImage = cardPanel.AddComponent<Image>();
        backgroundImage.color = GetAreaColor(areaData.overallResult);

        RectTransform panelRect = cardPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        panelRect.anchoredPosition = Vector2.zero;

        // Crear contenido de la tarjeta
        CreateCardContent();

        // Configurar para que siempre mire a la cámara
        StartCoroutine(LookAtCamera());
    }

    void CreateCardShadow(GameObject parent)
    {
        GameObject shadow = new GameObject("CardShadow");
        shadow.transform.SetParent(parent.transform, false);
        shadow.transform.SetAsFirstSibling();

        RectTransform shadowRect = shadow.AddComponent<RectTransform>();
        shadowRect.sizeDelta = new Vector2(cardWidth + 10, cardHeight + 10);
        shadowRect.anchoredPosition = new Vector2(8, -8);

        shadowImage = shadow.AddComponent<Image>();
        shadowImage.color = new Color(0, 0, 0, 0.4f);
    }

    void CreateCardContent()
    {
        // NO necesitamos contenedor adicional, usar directamente el cardPanel
        CreateAreaNameText(cardPanel);
        CreateOverallResultText(cardPanel);
    }

    void CreateAreaNameText(GameObject parent)
    {
        GameObject nameObj = new GameObject("AreaName");
        nameObj.transform.SetParent(parent.transform, false);

        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        // El texto del área ocupa la mitad superior de la tarjeta
        nameRect.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.4f); // 40% de la altura
        nameRect.anchoredPosition = new Vector2(0, cardHeight * 0.15f); // Posición en el tercio superior

        areaNameText = nameObj.AddComponent<Text>();
        areaNameText.text = areaData.areaName.ToUpper();
        areaNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // TAMAÑO DE FUENTE MASIVO para llenar el espacio
        areaNameText.fontSize = 80; // Aumentado de 32 a 80
        areaNameText.fontStyle = FontStyle.Bold;
        areaNameText.color = Color.white;
        areaNameText.alignment = TextAnchor.MiddleCenter;

        // IMPORTANTE: Permitir que el texto se ajuste automáticamente
        areaNameText.resizeTextForBestFit = true;
        areaNameText.resizeTextMinSize = 30;
        areaNameText.resizeTextMaxSize = 120;
    }

    void CreateOverallResultText(GameObject parent)
    {
        GameObject resultObj = new GameObject("OverallResult");
        resultObj.transform.SetParent(parent.transform, false);

        RectTransform resultRect = resultObj.AddComponent<RectTransform>();
        // El porcentaje ocupa la parte inferior sin solaparse
        resultRect.sizeDelta = new Vector2(cardWidth - 40, cardHeight * 0.55f); // 55% de la altura
        resultRect.anchoredPosition = new Vector2(0, -cardHeight * 0.15f); // Posición más abajo

        overallResultText = resultObj.AddComponent<Text>();
        overallResultText.text = $"{areaData.overallResult:F0}%";
        overallResultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // TAMAÑO DE FUENTE AJUSTADO para evitar solapamiento
        overallResultText.fontSize = 180; // Reducido de 200 a 180
        overallResultText.fontStyle = FontStyle.Bold;
        overallResultText.color = Color.white;
        overallResultText.alignment = TextAnchor.MiddleCenter;

        // IMPORTANTE: Permitir que el texto se ajuste automáticamente
        overallResultText.resizeTextForBestFit = true;
        overallResultText.resizeTextMinSize = 60;
        overallResultText.resizeTextMaxSize = 220;
    }

    Color GetAreaColor(float overallResult)
    {
        if (overallResult >= 90f) return optimusColor;
        else if (overallResult >= 80f) return healthyColor;
        else if (overallResult >= 70f) return sickColor;
        else return highRiskColor;
    }

    IEnumerator LookAtCamera()
    {
        while (true)
        {
            if (playerCamera != null && cardCanvas != null)
            {
                Vector3 directionToCamera = playerCamera.transform.position - cardCanvas.transform.position;
                cardCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    void Update()
    {
        if (cardPanel != null)
        {
            Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;
            cardPanel.transform.localScale = Vector3.Lerp(cardPanel.transform.localScale, targetScale, Time.deltaTime * animationSpeed);

            float floatingOffset = Mathf.Sin(Time.time * 1.5f) * floatAmplitude;
            Vector3 targetPosition = originalPosition + new Vector3(0, floatingOffset, 0);
            cardPanel.transform.position = Vector3.Lerp(cardPanel.transform.position, targetPosition, Time.deltaTime * 3f);

            if (shadowImage != null && isHovering)
            {
                Color shadowColor = shadowImage.color;
                shadowColor.a = Mathf.Lerp(shadowColor.a, 0.6f, Time.deltaTime * 5f);
                shadowImage.color = shadowColor;
            }
            else if (shadowImage != null)
            {
                Color shadowColor = shadowImage.color;
                shadowColor.a = Mathf.Lerp(shadowColor.a, 0.4f, Time.deltaTime * 5f);
                shadowImage.color = shadowColor;
            }
        }
    }

    // Eventos de interacción
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        Debug.Log($"Mouse sobre área: {areaData.areaName}");

        if (backgroundImage != null)
        {
            Color currentColor = backgroundImage.color;
            backgroundImage.color = new Color(
                Mathf.Min(currentColor.r * 1.1f, 1f),
                Mathf.Min(currentColor.g * 1.1f, 1f),
                Mathf.Min(currentColor.b * 1.1f, 1f),
                currentColor.a
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (backgroundImage != null)
        {
            backgroundImage.color = GetAreaColor(areaData.overallResult);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"✅ Click en área: {areaData.areaName}");

        StartCoroutine(ClickAnimation());

        AreaManager areaManager = FindFirstObjectByType<AreaManager>();
        if (areaManager != null)
        {
            areaManager.OnAreaClicked(this);
        }
        else
        {
            Debug.LogError("AreaManager no encontrado!");
        }
    }

    IEnumerator ClickAnimation()
    {
        Vector3 originalScale = cardPanel.transform.localScale;

        float timer = 0f;
        while (timer < 0.1f)
        {
            float scale = Mathf.Lerp(1f, 0.95f, timer / 0.1f);
            cardPanel.transform.localScale = originalScale * scale;
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        while (timer < 0.1f)
        {
            float scale = Mathf.Lerp(0.95f, 1f, timer / 0.1f);
            cardPanel.transform.localScale = originalScale * scale;
            timer += Time.deltaTime;
            yield return null;
        }

        cardPanel.transform.localScale = originalScale;
    }

    // Métodos públicos para obtener datos
    public AreaData GetAreaData()
    {
        return areaData;
    }

    public string GetAreaName()
    {
        return areaData.areaName;
    }
}

// Estructura para datos de área
[System.Serializable]
public class AreaData
{
    public string areaName;
    public float delivery;
    public float quality;
    public float parts;
    public float processManufacturing;
    public float trainingDNA;
    public float mtto;
    public float overallResult;
}