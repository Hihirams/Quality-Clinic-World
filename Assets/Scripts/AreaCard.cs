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
	private Image statusIndicator;

	[Header("Configuración Visual")]
	public float cardWidth = 300f;      // Más ancha
	public float cardHeight = 120f;     // Más alta
	public float floatingHeight = 2f;
	public Vector3 cardOffset = new Vector3(0, 5, 0);  // Más alta

	[Header("Animación")]
	public float hoverScale = 1.1f;
	public float animationSpeed = 5f;

	private Vector3 originalScale;
	private Vector3 originalPosition;
	private bool isHovering = false;
	private Camera playerCamera;

	// Colores según el estado
	private Color optimusColor = new Color(0.1f, 0.6f, 0.1f, 0.9f);      // Verde oscuro
	private Color healthyColor = new Color(0.3f, 0.8f, 0.3f, 0.9f);      // Verde claro  
	private Color sickColor = new Color(1f, 0.8f, 0.1f, 0.9f);           // Amarillo
	private Color highRiskColor = new Color(0.9f, 0.2f, 0.2f, 0.9f);     // Rojo

	void Start()
	{
		playerCamera = Camera.main;
		if (playerCamera == null)
			playerCamera = FindFirstObjectByType<Camera>();

		InitializeAreaData();
		CreateFloatingCard();
		SetupAreaCollider();  // Nueva función para configurar clicks en el área

		originalScale = cardPanel.transform.localScale;
		originalPosition = cardPanel.transform.position;
	}

	void SetupAreaCollider()
	{
		// Agregar collider para detección de clicks en toda el área
		Collider areaCollider = GetComponent<Collider>();
		if (areaCollider == null)
		{
			// Si el objeto no tiene collider, agregar uno grande
			BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
			boxCollider.size = new Vector3(10f, 5f, 10f); // Área grande para clicks fáciles
			boxCollider.center = new Vector3(0, 2.5f, 0);
			Debug.Log($"✓ BoxCollider agregado automáticamente a {areaName}");
		}
		else
		{
			Debug.Log($"✓ Collider existente encontrado en {areaName}");
		}
	}

	void InitializeAreaData()
	{
		// Datos específicos de cada área
		switch (areaName.ToUpper())
		{
			case "AT HONDA":
			case "ATHONDA":
			case "AREA_ATHONDA":
				areaData = new AreaData
				{
					areaName = "AT Honda",
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
					areaName = "Buzzer L2",
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

		// Configurar tamaño del canvas
		RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
		canvasRect.sizeDelta = new Vector2(cardWidth, cardHeight);
		canvasRect.localScale = Vector3.one * 0.02f; // Escala más grande para mundo 3D

		// Agregar GraphicRaycaster para detección de clicks
		canvasObj.AddComponent<GraphicRaycaster>();

		// Panel principal de la tarjeta
		cardPanel = new GameObject("CardPanel");
		cardPanel.transform.SetParent(canvasObj.transform, false);

		backgroundImage = cardPanel.AddComponent<Image>();
		backgroundImage.color = GetAreaColor(areaData.overallResult);

		RectTransform panelRect = cardPanel.GetComponent<RectTransform>();
		panelRect.sizeDelta = new Vector2(cardWidth, cardHeight);
		panelRect.anchoredPosition = Vector2.zero;

		// Agregar sombra/borde
		CreateCardBorder();

		// Indicador de estado (círculo colorido)
		CreateStatusIndicator();

		// Texto del nombre del área
		CreateAreaNameText();

		// Texto del resultado overall
		CreateOverallResultText();

		// Configurar para que siempre mire a la cámara
		StartCoroutine(LookAtCamera());
	}

	void CreateCardBorder()
	{
		GameObject border = new GameObject("Border");
		border.transform.SetParent(cardPanel.transform, false);
		border.transform.SetAsFirstSibling(); // Poner detrás

		Image borderImage = border.AddComponent<Image>();
		borderImage.color = new Color(0, 0, 0, 0.3f); // Sombra oscura

		RectTransform borderRect = border.GetComponent<RectTransform>();
		borderRect.sizeDelta = new Vector2(cardWidth + 4, cardHeight + 4);
		borderRect.anchoredPosition = new Vector2(2, -2);
	}

	void CreateStatusIndicator()
	{
		GameObject indicator = new GameObject("StatusIndicator");
		indicator.transform.SetParent(cardPanel.transform, false);

		statusIndicator = indicator.AddComponent<Image>();
		statusIndicator.color = GetAreaColor(areaData.overallResult);

		RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
		indicatorRect.sizeDelta = new Vector2(16, 16); // Más grande
		indicatorRect.anchoredPosition = new Vector2(-130, 35); // Reposicionado

		// Hacer el indicador circular
		statusIndicator.type = Image.Type.Filled;
	}

	void CreateAreaNameText()
	{
		GameObject nameObj = new GameObject("AreaName");
		nameObj.transform.SetParent(cardPanel.transform, false);

		areaNameText = nameObj.AddComponent<Text>();
		areaNameText.text = areaData.areaName.ToUpper();
		areaNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		areaNameText.fontSize = 18; // Más grande
		areaNameText.fontStyle = FontStyle.Bold;
		areaNameText.color = Color.white;
		areaNameText.alignment = TextAnchor.MiddleLeft;

		RectTransform nameRect = nameObj.GetComponent<RectTransform>();
		nameRect.sizeDelta = new Vector2(200, 40); // Más grande
		nameRect.anchoredPosition = new Vector2(-40, 20); // Reposicionado
	}

	void CreateOverallResultText()
	{
		GameObject resultObj = new GameObject("OverallResult");
		resultObj.transform.SetParent(cardPanel.transform, false);

		overallResultText = resultObj.AddComponent<Text>();
		overallResultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		overallResultText.fontSize = 16; // Más grande
		overallResultText.fontStyle = FontStyle.Bold;
		overallResultText.color = new Color(1f, 1f, 1f, 0.9f);
		overallResultText.alignment = TextAnchor.MiddleLeft;

		RectTransform resultRect = resultObj.GetComponent<RectTransform>();
		resultRect.sizeDelta = new Vector2(200, 30); // Más grande
		resultRect.anchoredPosition = new Vector2(-40, -20); // Reposicionado

		// Agregar ícono de estado
		string statusIcon = GetStatusIcon(areaData.overallResult);
		overallResultText.text = $"{statusIcon} {areaData.overallResult:F0}%";
	}

	Color GetAreaColor(float overallResult)
	{
		if (overallResult >= 90f) return optimusColor;      // Optimus
		else if (overallResult >= 80f) return healthyColor; // Healthy  
		else if (overallResult >= 70f) return sickColor;    // Sick
		else return highRiskColor;                           // High Risk
	}

	string GetStatusIcon(float overallResult)
	{
		if (overallResult >= 90f) return "✅";      // Optimus
		else if (overallResult >= 80f) return "✅"; // Healthy
		else if (overallResult >= 70f) return "⚠️"; // Sick  
		else return "❌";                            // High Risk
	}

	string GetStatusText(float overallResult)
	{
		if (overallResult >= 90f) return "OPTIMUS";
		else if (overallResult >= 80f) return "HEALTHY";
		else if (overallResult >= 70f) return "SICK";
		else return "HIGH RISK";
	}

	IEnumerator LookAtCamera()
	{
		while (true)
		{
			if (playerCamera != null && cardCanvas != null)
			{
				// Hacer que la tarjeta siempre mire a la cámara
				Vector3 targetRotation = playerCamera.transform.rotation.eulerAngles;
				cardCanvas.transform.rotation = Quaternion.Euler(targetRotation.x, targetRotation.y + 180, targetRotation.z);
			}
			yield return new WaitForSeconds(0.1f);
		}
	}

	void Update()
	{
		// Animación suave de hover
		if (cardPanel != null)
		{
			Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;
			cardPanel.transform.localScale = Vector3.Lerp(cardPanel.transform.localScale, targetScale, Time.deltaTime * animationSpeed);

			// Efecto de flotación sutil
			float floatingOffset = Mathf.Sin(Time.time * 2f) * 0.1f;
			Vector3 targetPosition = originalPosition + new Vector3(0, floatingOffset, 0);
			cardPanel.transform.position = Vector3.Lerp(cardPanel.transform.position, targetPosition, Time.deltaTime * 2f);
		}
	}

	// Eventos de interacción
	public void OnPointerEnter(PointerEventData eventData)
	{
		isHovering = true;
		Debug.Log($"Mouse sobre área: {areaData.areaName}");

		// Efecto visual adicional
		if (backgroundImage != null)
		{
			Color currentColor = backgroundImage.color;
			backgroundImage.color = new Color(currentColor.r * 1.2f, currentColor.g * 1.2f, currentColor.b * 1.2f, currentColor.a);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		isHovering = false;

		// Restaurar color original
		if (backgroundImage != null)
		{
			backgroundImage.color = GetAreaColor(areaData.overallResult);
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		Debug.Log($"✅ Click en área: {areaData.areaName}");

		// Buscar el AreaManager y notificar el click
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

// Estructura para datos de área (debe estar fuera de la clase AreaCard)
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