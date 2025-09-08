using System.Collections.Generic;
using UnityEngine;

public class AreaManager : MonoBehaviour
{
    [Header("Referencias del Sistema")]
    public IndustrialDashboard dashboard;

    [Header("Configuración de Áreas")]
    public List<GameObject> areaObjects = new List<GameObject>();

    [Header("Configuración de Debug")]
    public bool enableDebugMode = true;

    // Lista de datos de las áreas
    private Dictionary<string, AreaData> areaDataDict = new Dictionary<string, AreaData>();

    // SOLUCION: Solo registrar posiciones reales, NO mover objetos
    private Dictionary<string, Vector3> realAreaPositions = new Dictionary<string, Vector3>();

    // Estructura para datos de área
    [System.Serializable]
    public class AreaData
    {
        public string areaName;
        public string displayName;
        public float delivery;
        public float quality;
        public float parts;
        public float processManufacturing;
        public float trainingDNA;
        public float mtto;
        public float overallResult;
        public string status;
        public Color statusColor;
    }

    void Start()
    {
        InitializeAreaData();

        // Buscar dashboard automáticamente si no está asignado
        if (dashboard == null)
        {
            dashboard = FindFirstObjectByType<IndustrialDashboard>();
            if (dashboard != null)
            {
                Debug.Log("Dashboard encontrado automáticamente");
            }
            else
            {
                Debug.LogError("No se encontró IndustrialDashboard en la escena");
                return;
            }
        }

        // Buscar áreas automáticamente si la lista está vacía
        if (areaObjects.Count == 0)
        {
            FindAreasAutomatically();
        }

        // SOLUCION: Solo registrar posiciones REALES, no resolver conflictos
        RegisterRealAreaPositions();

        // Crear tarjetas para todas las áreas encontradas
        CreateAreaCards();

        // Ocultar dashboard al inicio
        if (dashboard != null)
        {
            dashboard.HideInterface();
        }
    }

    // NUEVO METODO: Solo registrar las posiciones reales tal como están en el mapa
    void RegisterRealAreaPositions()
    {
        Debug.Log("=== REGISTRANDO POSICIONES REALES (SIN MOVER NADA) ===");

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            Vector3 realPosition = areaObj.transform.position;

            // Guardar la posición REAL sin modificar el objeto
            realAreaPositions[areaKey] = realPosition;

            Debug.Log($"✅ {areaObj.name} (Key: {areaKey}) - Posición REAL preservada: {realPosition}");
        }

        Debug.Log("=== RESUMEN DE POSICIONES REALES ===");
        foreach (var kvp in realAreaPositions)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
        Debug.Log("====================================");
    }

    void Update()
    {
        // Detección de clicks con raycast SIMPLIFICADA
        if (Input.GetMouseButtonDown(0)) // Click izquierdo
        {
            HandleAreaClickSimplified();
        }

        // Tecla de debug para mostrar info de áreas
        if (Input.GetKeyDown(KeyCode.I) && enableDebugMode)
        {
            ShowAreaDebugInfo();
        }

        // Debug de posiciones con P
        if (Input.GetKeyDown(KeyCode.P) && enableDebugMode)
        {
            DebugRealAreaPositions();
        }

        // NUEVO: Debug de posiciones con tecla U
        if (Input.GetKeyDown(KeyCode.U) && enableDebugMode)
        {
            DebugAreaPositionsDetailed();
        }

        // Debug completo de jerarquía con H
        if (Input.GetKeyDown(KeyCode.H) && enableDebugMode)
        {
            DebugAreaHierarchy();
        }

        // Tecla ESC para cerrar dashboard
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (dashboard != null)
            {
                dashboard.HideInterface();
            }
        }
    }

    // MÉTODO NUEVO: Debug detallado de posiciones para resolver conflictos
    void DebugAreaPositionsDetailed()
    {
        Debug.Log("=== 🔍 ANÁLISIS DETALLADO DE POSICIONES DE ÁREAS ===");
        Debug.Log($"📍 Total de áreas registradas: {areaObjects.Count}");
        Debug.Log("");

        Dictionary<Vector3, List<string>> positionGroups = new Dictionary<Vector3, List<string>>();

        // Agrupar áreas por posición
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            Vector3 pos = areaObj.transform.position;
            string areaInfo = $"{areaObj.name} (Key: {GetAreaKey(areaObj.name)})";

            // Buscar si ya existe esta posición (con tolerancia)
            bool found = false;
            foreach (var kvp in positionGroups)
            {
                if (Vector3.Distance(kvp.Key, pos) < 0.1f)
                {
                    kvp.Value.Add(areaInfo);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                positionGroups[pos] = new List<string> { areaInfo };
            }
        }

        // Mostrar resultados
        Debug.Log("📊 RESUMEN POR POSICIONES:");
        Debug.Log("");

        int conflictCount = 0;
        foreach (var kvp in positionGroups)
        {
            Vector3 position = kvp.Key;
            List<string> areas = kvp.Value;

            if (areas.Count > 1)
            {
                conflictCount++;
                Debug.Log($"⚠️  CONFLICTO #{conflictCount} - Posición: {position}");
                Debug.Log($"   🏭 Áreas en conflicto ({areas.Count}):");
                for (int i = 0; i < areas.Count; i++)
                {
                    Debug.Log($"      {i + 1}. {areas[i]}");
                }
                Debug.Log("");
            }
            else
            {
                Debug.Log($"✅ Posición única: {position}");
                Debug.Log($"   🏭 Área: {areas[0]}");
                Debug.Log("");
            }
        }

        if (conflictCount == 0)
        {
            Debug.Log("🎉 ¡PERFECTO! No se encontraron conflictos de posición");
        }
        else
        {
            Debug.Log($"⚠️  TOTAL DE CONFLICTOS ENCONTRADOS: {conflictCount}");
            Debug.Log("");
            Debug.Log("💡 RECOMENDACIONES:");
            Debug.Log("1. Ve a la escena de Unity");
            Debug.Log("2. Selecciona cada área en conflicto");
            Debug.Log("3. Verifica su Transform Position en el Inspector");
            Debug.Log("4. Mueve manualmente las áreas a sus posiciones correctas");
            Debug.Log("5. VBL1 parece estar en la posición correcta - úsala como referencia");
        }

        Debug.Log("========================================");
    }

    // MÉTODO NUEVO: Debug completo de jerarquía de objetos
    void DebugAreaHierarchy()
    {
        Debug.Log("=== 🔍 ANÁLISIS COMPLETO DE JERARQUÍA DE ÁREAS ===");
        Debug.Log($"📍 Analizando {areaObjects.Count} áreas registradas");
        Debug.Log("");

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            Debug.Log($"🏭 ===== {areaObj.name} (Key: {areaKey}) =====");

            // Transform principal
            Debug.Log($"   📍 Transform.position: {areaObj.transform.position}");
            Debug.Log($"   📍 Transform.localPosition: {areaObj.transform.localPosition}");
            Debug.Log($"   🔄 Transform.rotation: {areaObj.transform.rotation.eulerAngles}");
            Debug.Log($"   📏 Transform.scale: {areaObj.transform.localScale}");

            // Información del parent
            if (areaObj.transform.parent != null)
            {
                Debug.Log($"   👨‍👩‍👧‍👦 Parent: {areaObj.transform.parent.name}");
                Debug.Log($"   👨‍👩‍👧‍👦 Parent Position: {areaObj.transform.parent.position}");
            }
            else
            {
                Debug.Log($"   👨‍👩‍👧‍👦 Parent: NINGUNO (Root object)");
            }

            // Información de children
            int childCount = areaObj.transform.childCount;
            Debug.Log($"   👶 Children count: {childCount}");

            if (childCount > 0)
            {
                Debug.Log($"   👶 Children:");
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = areaObj.transform.GetChild(i);
                    Debug.Log($"      {i + 1}. {child.name} - Pos: {child.position}");
                }
            }

            // Información de componentes relevantes
            Collider col = areaObj.GetComponent<Collider>();
            if (col != null)
            {
                Debug.Log($"   📦 Collider: {col.GetType().Name}");
                if (col is BoxCollider box)
                {
                    Debug.Log($"       Center: {box.center}, Size: {box.size}");
                }
            }

            Renderer rend = areaObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Debug.Log($"   🎨 Renderer bounds: {rend.bounds.center}");
            }
            else
            {
                Debug.Log($"   🎨 No renderer directo - buscando en children...");
                Renderer[] childRenderers = areaObj.GetComponentsInChildren<Renderer>();
                if (childRenderers.Length > 0)
                {
                    Debug.Log($"   🎨 Found {childRenderers.Length} child renderers:");
                    for (int i = 0; i < Mathf.Min(3, childRenderers.Length); i++)
                    {
                        Debug.Log($"       {i + 1}. {childRenderers[i].name}: {childRenderers[i].bounds.center}");
                    }
                }
            }

            // Posición registrada vs actual
            Vector3 registeredPos = realAreaPositions.ContainsKey(areaKey) ?
                                   realAreaPositions[areaKey] : Vector3.zero;
            Debug.Log($"   ⚖️  Posición registrada: {registeredPos}");
            Debug.Log($"   ⚖️  ¿Coincide con actual? {Vector3.Distance(areaObj.transform.position, registeredPos) < 0.1f}");

            Debug.Log("");
        }

        Debug.Log("========================================");
    }

    // METODO SIMPLIFICADO: Click directo en el área sin algoritmos complejos
    void HandleAreaClickSimplified()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        if (hits.Length == 0)
        {
            Debug.Log("Click detectado pero no golpeó ningún collider");
            return;
        }

        // Ordenar hits por distancia (más cercano primero)
        System.Array.Sort(hits, (hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

        Debug.Log($"🎯 Click detectado - {hits.Length} objetos golpeados:");

        // Buscar la primera área válida en los hits
        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;
            Debug.Log($"  - Hit: {hitObject.name} a distancia {hit.distance}");

            // Buscar directamente en nuestras áreas registradas
            foreach (GameObject areaObj in areaObjects)
            {
                if (areaObj == hitObject || IsChildOfArea(hitObject, areaObj))
                {
                    Debug.Log($"🎯 ÁREA IDENTIFICADA: {areaObj.name}");
                    OnAreaClicked(areaObj);
                    return; // Salir inmediatamente después del primer match
                }
            }
        }

        Debug.LogWarning("No se encontró ningún área válida en el click");
    }

    // Debug simplificado para posiciones reales
    void DebugRealAreaPositions()
    {
        Debug.Log("=== DEBUG: POSICIONES REALES DE AREAS ===");

        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            string areaKey = GetAreaKey(areaObj.name);
            Vector3 currentPos = areaObj.transform.position;
            Vector3 registeredPos = realAreaPositions.ContainsKey(areaKey) ? realAreaPositions[areaKey] : Vector3.zero;

            Debug.Log($"🏭 {areaObj.name} (Key: {areaKey})");
            Debug.Log($"   Posición actual: {currentPos}");
            Debug.Log($"   Posición registrada: {registeredPos}");
            Debug.Log($"   ¿Coinciden? {Vector3.Distance(currentPos, registeredPos) < 0.1f}");
        }
        Debug.Log("========================================");
    }

    void FindAreasAutomatically()
    {
        areaObjects.Clear();

        string[] exactAreaNames = {
            "Area_ATHONDA", "Area_VCTL4", "Area_BUZZERL2", "Area_VBL1"
        };

        foreach (string exactName in exactAreaNames)
        {
            GameObject found = GameObject.Find(exactName);
            if (found != null && !areaObjects.Contains(found))
            {
                areaObjects.Add(found);
                Debug.Log($"✓ Área encontrada: {found.name} - Posición: {found.transform.position}");
            }
        }

        Debug.Log($"Total de áreas encontradas: {areaObjects.Count}");
    }

    void InitializeAreaData()
    {
        areaDataDict["ATHONDA"] = new AreaData
        {
            areaName = "ATHONDA",
            displayName = "AT Honda",
            delivery = 100f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 100f,
            trainingDNA = 100f,
            mtto = 100f,
            overallResult = 95f,
            status = "Optimus",
            statusColor = new Color(0.0f, 0.4f, 0.0f, 1.0f)
        };

        areaDataDict["VCTL4"] = new AreaData
        {
            areaName = "VCTL4",
            displayName = "VCT L4",
            delivery = 77f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 100f,
            trainingDNA = 81f,
            mtto = 100f,
            overallResult = 92f,
            status = "Optimus",
            statusColor = new Color(0.0f, 0.4f, 0.0f, 1.0f)
        };

        areaDataDict["BUZZERL2"] = new AreaData
        {
            areaName = "BUZZERL2",
            displayName = "BUZZER L2",
            delivery = 91f,
            quality = 83f,
            parts = 81f,
            processManufacturing = 89f,
            trainingDNA = 62f,
            mtto = 100f,
            overallResult = 73f,
            status = "Sick",
            statusColor = Color.yellow
        };

        areaDataDict["VBL1"] = new AreaData
        {
            areaName = "VBL1",
            displayName = "VB L1",
            delivery = 29f,
            quality = 83f,
            parts = 100f,
            processManufacturing = 32f,
            trainingDNA = 100f,
            mtto = 47f,
            overallResult = 49f,
            status = "High risk",
            statusColor = Color.red
        };
    }

    void CreateAreaCards()
    {
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj == null) continue;

            AreaCard card = areaObj.GetComponent<AreaCard>();
            if (card == null)
            {
                card = areaObj.AddComponent<AreaCard>();
            }

            string areaKey = GetAreaKey(areaObj.name);
            if (areaDataDict.ContainsKey(areaKey))
            {
                AreaData data = areaDataDict[areaKey];
                card.areaName = data.displayName;
                Debug.Log($"✓ Tarjeta configurada para {areaObj.name} -> {data.displayName}");
            }
            else
            {
                card.areaName = areaObj.name;
                Debug.LogWarning($"❌ No se encontraron datos para el área: {areaObj.name}");
            }

            SetupAreaCollider(areaObj);
        }
    }

    bool IsChildOfArea(GameObject clickedObj, GameObject areaObj)
    {
        Transform current = clickedObj.transform;
        while (current != null)
        {
            if (current.gameObject == areaObj)
                return true;
            current = current.parent;
        }
        return false;
    }

    string GetAreaKey(string objectName)
    {
        string upperName = objectName.ToUpper();

        if (upperName == "AREA_ATHONDA" || upperName.Contains("ATHONDA") || upperName.Contains("AT HONDA"))
        {
            return "ATHONDA";
        }
        else if (upperName == "AREA_VCTL4" || upperName.Contains("VCTL4") || upperName.Contains("VCT L4"))
        {
            return "VCTL4";
        }
        else if (upperName == "AREA_BUZZERL2" || upperName.Contains("BUZZERL2") || upperName.Contains("BUZZER L2"))
        {
            return "BUZZERL2";
        }
        else if (upperName == "AREA_VBL1" || upperName.Contains("VBL1") || upperName.Contains("VB L1"))
        {
            return "VBL1";
        }

        return objectName.ToUpper().Replace("AREA_", "");
    }

    public void OnAreaClicked(GameObject areaObject)
    {
        if (dashboard == null) return;

        string areaKey = GetAreaKey(areaObject.name);

        if (areaDataDict.ContainsKey(areaKey))
        {
            AreaData data = areaDataDict[areaKey];

            List<KPIData> kpis = new List<KPIData>
            {
                new KPIData("Delivery", data.delivery, "%"),
                new KPIData("Quality", data.quality, "%"),
                new KPIData("Parts", data.parts, "%"),
                new KPIData("Process Manufacturing", data.processManufacturing, "%"),
                new KPIData("Training DNA", data.trainingDNA, "%"),
                new KPIData("Mantenimiento", data.mtto, "%"),
                new KPIData("Overall Result", data.overallResult, "%")
            };

            List<string> predicciones = GeneratePredictions(data);

            dashboard.UpdateWithAreaData(data.displayName, kpis, predicciones);

            // SOLUCION: Usar la posición REAL del objeto, no una inventada
            Vector3 focusPosition = realAreaPositions.ContainsKey(areaKey) ?
                                  realAreaPositions[areaKey] :
                                  areaObject.transform.position;

            Debug.Log($"🎯 ÁREA CLICKEADA: {areaObject.name} (Key: {areaKey})");
            Debug.Log($"🎯 POSICIÓN REAL PARA FOCUS: {focusPosition}");

            FreeCameraController cameraController = Camera.main?.GetComponent<FreeCameraController>();
            if (cameraController != null)
            {
                cameraController.FocusOnArea(areaObject.transform, 25f);
            }

            Debug.Log($"✅ Área seleccionada: {data.displayName}");
        }
    }

    public void OnAreaClicked(AreaCard areaCard)
    {
        if (areaCard != null && areaCard.gameObject != null)
        {
            OnAreaClicked(areaCard.gameObject);
        }
    }

    List<string> GeneratePredictions(AreaData data)
    {
        List<string> predictions = new List<string>();

        if (data.delivery < 50)
            predictions.Add("🚨 CRÍTICO: Problemas severos de entrega detectados");
        else if (data.delivery < 80)
            predictions.Add("⚠️ Delivery bajo riesgo - Optimización recomendada");

        if (data.quality < 70)
            predictions.Add("🔧 Control de calidad requiere intervención");

        if (data.trainingDNA < 70)
            predictions.Add("📚 Personal requiere capacitación urgente");

        if (data.overallResult < 50)
        {
            predictions.Add("🚨 ZONA ROJA: Intervención ejecutiva inmediata");
        }
        else if (data.overallResult >= 90)
        {
            predictions.Add("🏆 ZONA OPTIMUS: Benchmark para otras áreas");
        }

        return predictions;
    }

    void SetupAreaCollider(GameObject areaObj)
    {
        Collider existingCollider = areaObj.GetComponent<Collider>();

        if (existingCollider == null)
        {
            BoxCollider boxCollider = areaObj.AddComponent<BoxCollider>();

            Renderer[] renderers = areaObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                boxCollider.center = areaObj.transform.InverseTransformPoint(bounds.center);
                boxCollider.size = bounds.size;
            }
            else
            {
                boxCollider.size = Vector3.one * 10f;
                boxCollider.center = Vector3.up * 2.5f;
            }

            Debug.Log($"✓ BoxCollider agregado a: {areaObj.name}");
        }
    }

    void ShowAreaDebugInfo()
    {
        Debug.Log("=== INFORMACIÓN DE DEBUG DE ÁREAS ===");
        foreach (GameObject areaObj in areaObjects)
        {
            if (areaObj != null)
            {
                string areaKey = GetAreaKey(areaObj.name);
                Debug.Log($"Área: {areaObj.name} (Key: {areaKey}) - Posición: {areaObj.transform.position}");
            }
        }
        Debug.Log("=====================================");
    }

    public void CloseDashboard()
    {
        if (dashboard != null)
        {
            dashboard.HideInterface();
        }
    }

    public AreaData GetAreaData(string areaKey)
    {
        return areaDataDict.ContainsKey(areaKey) ? areaDataDict[areaKey] : null;
    }

    public List<GameObject> GetAreaObjects()
    {
        return areaObjects;
    }
}