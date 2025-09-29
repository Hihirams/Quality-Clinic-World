using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Corrige y valida las posiciones de áreas industriales y sus elementos visuales hijos.
/// Permite centrar áreas en base a sus cubos visuales o ajustar posiciones locales.
/// Se ejecuta antes que AreaManager para garantizar posiciones correctas en inicialización.
/// </summary>
/// <remarks>
/// Orden de ejecución: se fuerza a correr antes de AreaManager para que las áreas ya
/// estén en sus coordenadas definitivas cuando el manager inicialice overlays, labels, etc.
/// </remarks>
[DefaultExecutionOrder(-100)]
public class AreaPositionFixerV2 : MonoBehaviour
{
    #region Nested Types

    /// <summary>
    /// Define la corrección de posición para un área específica.
    /// </summary>
    [System.Serializable]
    public class AreaCorrection
    {
        /// <summary>Nombre del GameObject raíz del área en la jerarquía (p.ej. "Area_VBL1").</summary>
        public string areaName;

        /// <summary>Posición objetivo del objeto padre del área (si se usa restauración/alternativo).</summary>
        public Vector3 targetAreaPosition;

        /// <summary>Posición objetivo del cubo visual principal (informativo/depuración).</summary>
        public Vector3 targetCubePosition;

        /// <summary>Nombre del hijo que representa el cubo visual principal.</summary>
        public string cubeChildName;
    }

    #endregion

    #region Serialized Fields

    [Header("Configuración")]
    [Tooltip("Si está activado, corrige posiciones automáticamente al iniciar")]
    public bool autoFixOnStart = false;

    [Tooltip("Habilita logs detallados de corrección (usando QCLog.Info con QC_VERBOSE)")]
    public bool debugMode = true;

    [Header("Correcciones Configuradas")]
    [Tooltip("Lista de correcciones a aplicar por área")]
    public List<AreaCorrection> corrections = new List<AreaCorrection>();

    #endregion

    #region Private State

    // Caché de áreas procesadas para evitar búsquedas repetidas
    private readonly Dictionary<string, Transform> _areaCache = new Dictionary<string, Transform>();

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        InitializeCorrections();

        if (autoFixOnStart)
        {
            // Método recomendado: centrar por cubos visuales
            FixAreaPositionsAndChildren();
        }
    }

    private void Update()
    {
        // Hotkeys de utilidad (solo en desarrollo)
        if (Input.GetKeyDown(KeyCode.F4))
            RestoreOriginalPositions();

        if (Input.GetKeyDown(KeyCode.F5))
            FixAreaPositionsAndChildren();

        if (Input.GetKeyDown(KeyCode.F6))
            AlternativeFixAdjustLocalPositions();

        if (Input.GetKeyDown(KeyCode.F7))
            DebugCurrentPositions();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Restaura todas las áreas a sus posiciones originales antes de cualquier corrección.
    /// </summary>
    [ContextMenu("Restaurar Posiciones Originales")]
    public void RestoreOriginalPositions()
    {
        if (debugMode) QCLog.Info("=== RESTAURANDO POSICIONES ORIGINALES ===");

        // Basado en el log de referencia original del proyecto (no tocar nombres)
        RestoreAreaToOriginal("Area_ATHONDA", new Vector3(-64.41f, 0.00f, 125.38f));
        RestoreAreaToOriginal("Area_VCTL4",   new Vector3(-64.41f, 0.00f, 125.38f));
        RestoreAreaToOriginal("Area_BUZZERL2",new Vector3(-64.41f, 0.00f, 125.38f));
        // VBL1 no necesita restauración

        if (debugMode) QCLog.Info("=== RESTAURACIÓN COMPLETADA ===");
    }

    /// <summary>
    /// Corrige las posiciones de áreas centrándolas en sus elementos visuales.
    /// Método principal recomendado (no altera la forma relativa de los cubos).
    /// </summary>
    [ContextMenu("Fix Areas and Children Correctly")]
    public void FixAreaPositionsAndChildren()
    {
        if (debugMode) QCLog.Info("=== CORRIGIENDO ÁREAS Y SUS HIJOS CORRECTAMENTE ===");

        foreach (AreaCorrection correction in corrections)
        {
            Transform areaTransform = FindAreaTransform(correction.areaName);

            if (areaTransform != null)
            {
                // Centrar el padre en el centro geométrico de sus cubos visibles
                CenterAreaOnVisualElements(areaTransform.gameObject, correction);
            }
            else
            {
                QCLog.Warn($"Área no encontrada: {correction.areaName}");
            }
        }

        if (debugMode) QCLog.Info("=== CORRECCIÓN COMPLETADA ===");
    }

    /// <summary>
    /// Método alternativo: mueve el padre y reajusta las posiciones locales de los cubos
    /// para mantener su posición global (útil si quieres mover el pivot sin “arrastrar” visuales).
    /// </summary>
    [ContextMenu("Alternative Fix - Adjust Local Positions")]
    public void AlternativeFixAdjustLocalPositions()
    {
        if (debugMode) QCLog.Info("=== MÉTODO ALTERNATIVO: AJUSTAR POSICIONES LOCALES ===");

        // Garantizar estado base antes de aplicar el alternativo
        RestoreOriginalPositions();

        foreach (AreaCorrection correction in corrections)
        {
            Transform areaTransform = FindAreaTransform(correction.areaName);

            if (areaTransform != null)
            {
                // Mover el área padre a su posición correcta
                Vector3 oldAreaPos = areaTransform.position;
                areaTransform.position = correction.targetAreaPosition;

                // Calcular y registrar offset aplicado
                Vector3 offset = correction.targetAreaPosition - oldAreaPos;

                if (debugMode)
                {
                    QCLog.Info($"Área {correction.areaName}:");
                    QCLog.Info($"  Movida de {oldAreaPos} a {correction.targetAreaPosition}");
                    QCLog.Info($"  Offset aplicado: {offset}");
                }

                // Ajustar posiciones locales de hijos "Cube" para que su world-pos no cambie
                AdjustChildrenLocalPositions(areaTransform, offset);
            }
        }

        if (debugMode) QCLog.Info("=== AJUSTE ALTERNATIVO COMPLETADO ===");
    }

    /// <summary>
    /// Lista por consola las posiciones actuales de áreas y sus cubos (depuración).
    /// </summary>
    [ContextMenu("Debug Current Positions")]
    public void DebugCurrentPositions()
    {
        QCLog.Info("=== POSICIONES ACTUALES ===");

        string[] areaNames = { "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1" };

        foreach (string name in areaNames)
        {
            Transform areaTransform = FindAreaTransform(name);
            if (areaTransform != null)
            {
                QCLog.Info($"ÁREA: {name}");
                QCLog.Info($"  Posición: {areaTransform.position}");

                foreach (Transform child in areaTransform)
                {
                    if (child.name.Contains("Cube"))
                    {
                        QCLog.Info($"  - {child.name}: {child.position} (local: {child.localPosition})");
                    }
                }

                QCLog.Info(string.Empty);
            }
        }

        QCLog.Info("=== FIN DEBUG ===");
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Inicializa la lista de correcciones con los valores predefinidos para cada área.
    /// </summary>
    private void InitializeCorrections()
    {
        corrections.Clear();

        // ATHONDA
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

        // VBL1 (altura especial en Y = 1.5f)
        corrections.Add(new AreaCorrection
        {
            areaName = "Area_VBL1",
            targetAreaPosition = new Vector3(-15.92f, 1.50f, 153.32f),
            targetCubePosition = new Vector3(-0.92f, 0.00f, 146.04f),
            cubeChildName = "Cube (60)"
        });

        if (debugMode)
        {
            QCLog.Info($"Correcciones V2 inicializadas: {corrections.Count}");
        }
    }

    /// <summary>
    /// Restaura un área específica a su posición original.
    /// </summary>
    private void RestoreAreaToOriginal(string areaName, Vector3 originalPosition)
    {
        Transform areaTransform = FindAreaTransform(areaName);
        if (areaTransform != null)
        {
            areaTransform.position = originalPosition;
            if (debugMode) QCLog.Info($"Restaurada {areaName} a: {originalPosition}");
        }
    }

    /// <summary>
    /// Centra un área en el punto medio de todos sus cubos visuales visibles.
    /// Conserva altura Y de acuerdo con necesidades por área (VBL1 = 1.5; resto = 0).
    /// </summary>
    private void CenterAreaOnVisualElements(GameObject areaObj, AreaCorrection correction)
    {
        // 1) Recolectar cubos visibles (con Renderer)
        List<Transform> visualCubes = FindVisualCubes(areaObj.transform);
        if (visualCubes.Count == 0)
        {
            QCLog.Warn($"No se encontraron cubos visuales en {correction.areaName}");
            return;
        }

        // 2) Centro geométrico de las posiciones
        Vector3 centerPoint = CalculateGeometricCenter(visualCubes);

        // 3) Ajuste de altura (suelo / caso especial)
        centerPoint.y = correction.areaName == "Area_VBL1" ? 1.5f : 0f;

        if (debugMode)
            QCLog.Info($"Moviendo {correction.areaName} de {areaObj.transform.position} a {centerPoint}");

        // 4) Aplicar
        areaObj.transform.position = centerPoint;

        if (debugMode)
            QCLog.Info($"Área {correction.areaName} centrada en: {centerPoint}");
    }

    /// <summary>
    /// Devuelve todos los hijos tipo "Cube" que tengan Renderer (visibles).
    /// </summary>
    private List<Transform> FindVisualCubes(Transform areaTransform)
    {
        var list = new List<Transform>();

        foreach (Transform child in areaTransform)
        {
            if (child.name.Contains("Cube") && child.TryGetComponent<Renderer>(out _))
                list.Add(child);
        }

        return list;
    }

    /// <summary>
    /// Calcula el centro geométrico (promedio) de una lista de transforms.
    /// </summary>
    private Vector3 CalculateGeometricCenter(List<Transform> transforms)
    {
        Vector3 acc = Vector3.zero;
        for (int i = 0; i < transforms.Count; i++)
            acc += transforms[i].position;

        return acc / Mathf.Max(1, transforms.Count);
    }

    /// <summary>
    /// Ajusta localPosition de hijos "Cube" para compensar un desplazamiento del padre (mantener world-pos).
    /// </summary>
    private void AdjustChildrenLocalPositions(Transform areaTransform, Vector3 offset)
    {
        foreach (Transform child in areaTransform)
        {
            if (!child.name.Contains("Cube")) continue;

            Vector3 oldLocalPos = child.localPosition;
            child.localPosition = oldLocalPos - offset;

            if (debugMode)
            {
                QCLog.Info($"  Hijo {child.name}:");
                QCLog.Info($"    Local pos ajustada de {oldLocalPos} a {child.localPosition}");
                QCLog.Info($"    World pos resultante: {child.position}");
            }
        }
    }

    /// <summary>
    /// Busca y cachea el Transform de un área por nombre (evita Find repetido).
    /// </summary>
    private Transform FindAreaTransform(string areaName)
    {
        // 1) Intentar desde caché
        if (_areaCache.TryGetValue(areaName, out Transform cached) && cached != null)
            return cached;

        // 2) Buscar en escena
        GameObject areaObj = GameObject.Find(areaName);
        if (areaObj != null)
        {
            _areaCache[areaName] = areaObj.transform;
            return areaObj.transform;
        }

        return null;
    }

    #endregion

    #region Debug
    // Los métodos de depuración están en Public API para poder invocarlos por ContextMenu.
    #endregion
}
