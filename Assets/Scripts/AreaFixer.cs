using System.Collections.Generic;
using UnityEngine;

public class AreaPositionFixerV2 : MonoBehaviour
{
	[System.Serializable]
	public class AreaCorrection
	{
		public string areaName;
		public Vector3 targetAreaPosition;  // Donde debe estar el área padre
		public Vector3 targetCubePosition;  // Donde debe estar el cubo visual
		public string cubeChildName;        // Nombre del cubo hijo
	}

	[Header("Configuración")]
	public bool autoFixOnStart = false;
	public bool debugMode = true;

	[Header("Correcciones Configuradas")]
	public List<AreaCorrection> corrections = new List<AreaCorrection>();

	void Start()
	{
		InitializeCorrections();

		if (autoFixOnStart)
		{
			FixAreaPositionsAndChildren();
		}
	}

	void InitializeCorrections()
	{
		corrections.Clear();

		// ATHONDA - El área y su cubo deben estar en la misma posición
		corrections.Add(new AreaCorrection
		{
			areaName = "Area_ATHONDA",
			targetAreaPosition = new Vector3(-58.72f, 0.00f, 109.54f),
			targetCubePosition = new Vector3(-58.72f, 0.00f, 109.54f),
			cubeChildName = "Cube (8)"
		});

		// VCTL4 
		corrections.Add(new AreaCorrection
		{
			areaName = "Area_VCTL4",
			targetAreaPosition = new Vector3(-1.96f, 0.00f, 24.14f),
			targetCubePosition = new Vector3(-1.96f, 0.00f, 24.14f),
			cubeChildName = "Cube (52)"
		});

		// BUZZERL2
		corrections.Add(new AreaCorrection
		{
			areaName = "Area_BUZZERL2",
			targetAreaPosition = new Vector3(0.28f, 0.00f, -15.18f),
			targetCubePosition = new Vector3(0.28f, 0.00f, -15.18f),
			cubeChildName = "Cube (49)"
		});

		// VBL1 - Ya está correcta
		corrections.Add(new AreaCorrection
		{
			areaName = "Area_VBL1",
			targetAreaPosition = new Vector3(-15.92f, 1.50f, 153.32f),
			targetCubePosition = new Vector3(-0.92f, 0.00f, 146.04f), // Cubo principal
			cubeChildName = "Cube (60)"
		});

		if (debugMode)
		{
			Debug.Log("Correcciones V2 inicializadas: " + corrections.Count);
		}
	}

	[ContextMenu("Restaurar Posiciones Originales")]
	public void RestoreOriginalPositions()
	{
		Debug.Log("=== RESTAURANDO POSICIONES ORIGINALES ===");

		// Restaurar las posiciones basándose en el debug log original
		RestoreAreaToOriginal("Area_ATHONDA", new Vector3(-64.41f, 0.00f, 125.38f));
		RestoreAreaToOriginal("Area_VCTL4", new Vector3(-64.41f, 0.00f, 125.38f));
		RestoreAreaToOriginal("Area_BUZZERL2", new Vector3(-64.41f, 0.00f, 125.38f));
		// VBL1 no necesita restauración

		Debug.Log("=== RESTAURACIÓN COMPLETADA ===");
	}

	void RestoreAreaToOriginal(string areaName, Vector3 originalPosition)
	{
		GameObject areaObj = GameObject.Find(areaName);
		if (areaObj != null)
		{
			areaObj.transform.position = originalPosition;
			Debug.Log("Restaurada " + areaName + " a: " + originalPosition);
		}
	}

	[ContextMenu("Fix Areas and Children Correctly")]
	public void FixAreaPositionsAndChildren()
	{
		Debug.Log("=== CORRIGIENDO ÁREAS Y SUS HIJOS CORRECTAMENTE ===");

		foreach (AreaCorrection correction in corrections)
		{
			GameObject areaObj = GameObject.Find(correction.areaName);

			if (areaObj != null)
			{
				// MÉTODO 1: Mover el área al centro de sus elementos visuales
				CenterAreaOnVisualElements(areaObj, correction);
			}
			else
			{
				Debug.LogWarning("Área no encontrada: " + correction.areaName);
			}
		}

		Debug.Log("=== CORRECCIÓN COMPLETADA ===");
	}

	void CenterAreaOnVisualElements(GameObject areaObj, AreaCorrection correction)
	{
		// Encontrar todos los cubos (elementos visuales) del área
		List<GameObject> visualCubes = new List<GameObject>();

		// Buscar cubos en los hijos
		foreach (Transform child in areaObj.transform)
		{
			if (child.name.Contains("Cube") && child.GetComponent<Renderer>() != null)
			{
				visualCubes.Add(child.gameObject);
			}
		}

		if (visualCubes.Count == 0)
		{
			Debug.LogWarning("No se encontraron cubos visuales en " + correction.areaName);
			return;
		}

		// Calcular el centro de todos los cubos visuales
		Vector3 centerPoint = Vector3.zero;
		foreach (GameObject cube in visualCubes)
		{
			centerPoint += cube.transform.position;
		}
		centerPoint /= visualCubes.Count;

		// Ajustar Y para que el área esté en el suelo
		centerPoint.y = 0f;
		if (correction.areaName == "Area_VBL1")
		{
			centerPoint.y = 1.5f; // VBL1 tiene altura especial
		}

		Debug.Log("Moviendo " + correction.areaName + " de " + areaObj.transform.position + " a " + centerPoint);

		// Mover el área al centro calculado
		areaObj.transform.position = centerPoint;

		Debug.Log("Área " + correction.areaName + " centrada en: " + centerPoint);
	}

	[ContextMenu("Alternative Fix - Adjust Local Positions")]
	public void AlternativeFixAdjustLocalPositions()
	{
		Debug.Log("=== MÉTODO ALTERNATIVO: AJUSTAR POSICIONES LOCALES ===");

		// Primero restaurar posiciones originales
		RestoreOriginalPositions();

		foreach (AreaCorrection correction in corrections)
		{
			GameObject areaObj = GameObject.Find(correction.areaName);

			if (areaObj != null)
			{
				// Mover el área padre a su posición correcta
				Vector3 oldAreaPos = areaObj.transform.position;
				areaObj.transform.position = correction.targetAreaPosition;

				// Calcular el offset que se aplicó
				Vector3 offset = correction.targetAreaPosition - oldAreaPos;

				Debug.Log("Área " + correction.areaName + ":");
				Debug.Log("  Movida de " + oldAreaPos + " a " + correction.targetAreaPosition);
				Debug.Log("  Offset aplicado: " + offset);

				// Ajustar posiciones locales de hijos para compensar el movimiento
				foreach (Transform child in areaObj.transform)
				{
					if (child.name.Contains("Cube"))
					{
						Vector3 oldLocalPos = child.localPosition;
						child.localPosition = oldLocalPos - offset;

						Debug.Log("  Hijo " + child.name + ":");
						Debug.Log("    Local pos ajustada de " + oldLocalPos + " a " + child.localPosition);
						Debug.Log("    World pos resultante: " + child.position);
					}
				}
			}
		}

		Debug.Log("=== AJUSTE ALTERNATIVO COMPLETADO ===");
	}

	[ContextMenu("Debug Current Positions")]
	public void DebugCurrentPositions()
	{
		Debug.Log("=== POSICIONES ACTUALES ===");

		string[] areaNames = { "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1" };

		foreach (string areaName in areaNames)
		{
			GameObject areaObj = GameObject.Find(areaName);
			if (areaObj != null)
			{
				Debug.Log("ÁREA: " + areaName);
				Debug.Log("  Posición: " + areaObj.transform.position);

				foreach (Transform child in areaObj.transform)
				{
					if (child.name.Contains("Cube"))
					{
						Debug.Log("  - " + child.name + ": " + child.position + " (local: " + child.localPosition + ")");
					}
				}
				Debug.Log("");
			}
		}

		Debug.Log("=== FIN DEBUG ===");
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.F4))
		{
			RestoreOriginalPositions();
		}

		if (Input.GetKeyDown(KeyCode.F5))
		{
			FixAreaPositionsAndChildren();
		}

		if (Input.GetKeyDown(KeyCode.F6))
		{
			AlternativeFixAdjustLocalPositions();
		}

		if (Input.GetKeyDown(KeyCode.F7))
		{
			DebugCurrentPositions();
		}
	}
}