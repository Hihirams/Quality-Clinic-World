using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject planet;
    public Camera mainCamera;
    public RegionCard[] continentCards;
    
    [Header("Configuración de Cámara")]
    public float zoomDuration = 1.5f;
    public float continentZoomDistance = 8f;
    public float countryZoomDistance = 5f;
    public float stateZoomDistance = 3f;
    public float plantZoomDistance = 2f;
    
    [Header("Rotación del Planeta")]
    public float planetRotationSpeed = 10f;
    public bool autoRotate = true;
    
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;
    private Stack<NavigationLevel> navigationStack = new Stack<NavigationLevel>();
    private RegionCard[] currentVisibleCards;
    private bool isZooming = false;

    private class NavigationLevel
    {
        public RegionCard focusedRegion;
        public RegionCard[] visibleCards;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
    }

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        initialCameraPosition = mainCamera.transform.position;
        initialCameraRotation = mainCamera.transform.rotation;
        
        currentVisibleCards = continentCards;
        ShowCards(continentCards);
        
        Debug.Log("PlanetController iniciado. Continentes visibles: " + continentCards.Length);
    }

    void Update()
    {
        if (autoRotate && !isZooming)
        {
            planet.transform.Rotate(Vector3.up, planetRotationSpeed * Time.deltaTime, Space.World);
        }
        
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            GoBack();
        }
    }

    public void FocusOnRegion(RegionCard region)
    {
        if (isZooming) return;

        Debug.Log("═══════════════════════════════════════════════");
        Debug.Log($"🎯 FOCUS EN: {region.regionName} (Tipo: {region.regionType})");
        Debug.Log($"📊 Tarjetas hijas: {(region.childRegions != null ? region.childRegions.Length : 0)}");
        Debug.Log($"🌍 Rotación del planeta ANTES: {planet.transform.rotation.eulerAngles}");

        // CRÍTICO: Detener rotación ANTES de calcular posiciones
        autoRotate = false;
        Debug.Log("⏸️ Auto-rotación DETENIDA");

        // NUEVO: Bloquear posiciones de las tarjetas hijas INMEDIATAMENTE
        if (region.childRegions != null && region.childRegions.Length > 0)
        {
            Debug.Log($"🔒 Bloqueando {region.childRegions.Length} tarjetas hijas...");
            foreach (var childCard in region.childRegions)
            {
                if (childCard != null)
                {
                    childCard.LockPosition();
                }
            }
        }

        NavigationLevel currentLevel = new NavigationLevel
        {
            focusedRegion = region,
            visibleCards = currentVisibleCards,
            cameraPosition = mainCamera.transform.position,
            cameraRotation = mainCamera.transform.rotation
        };
        navigationStack.Push(currentLevel);
        
        HideCards(currentVisibleCards);
        
        float targetDistance = GetZoomDistanceForNextLevel(region.regionType);
        
        // Obtener la posición ACTUAL de la tarjeta (después de cualquier rotación)
        Vector3 cardWorldPosition = region.transform.position;
        
        // Calcular dirección desde el centro del planeta
        Vector3 directionFromPlanet = (cardWorldPosition - planet.transform.position).normalized;
        
        // Posición objetivo de la cámara
        Vector3 targetPosition = cardWorldPosition + directionFromPlanet * targetDistance;
        
        // Rotación de la cámara mirando hacia la tarjeta
        Vector3 lookDirection = cardWorldPosition - targetPosition;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        
        Debug.Log($"📍 Posición tarjeta: {cardWorldPosition}");
        Debug.Log($"📷 Target cámara: {targetPosition}");
        Debug.Log("═══════════════════════════════════════════════");
        
        StartCoroutine(ZoomToRegion(targetPosition, targetRotation, region.childRegions));
    }

    private IEnumerator ZoomToRegion(Vector3 targetPos, Quaternion targetRot, RegionCard[] newCards)
    {
        isZooming = true;
        
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float elapsed = 0;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / zoomDuration);
            
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        
        if (newCards != null && newCards.Length > 0)
        {
            Debug.Log("Mostrando " + newCards.Length + " tarjetas nuevas");
            foreach (var card in newCards)
            {
                if (card != null)
                {
                    card.gameObject.SetActive(true);
                    Debug.Log("Activando tarjeta: " + card.regionName);
                }
            }
        }
        else
        {
            Debug.LogWarning("No hay tarjetas hijas para mostrar!");
        }
        
        currentVisibleCards = newCards;
        ShowCards(newCards);
        
        isZooming = false;
    }

    private float GetZoomDistanceForNextLevel(RegionCard.RegionType currentType)
    {
        switch (currentType)
        {
            case RegionCard.RegionType.Continent:
                return countryZoomDistance;
            case RegionCard.RegionType.Country:
                return stateZoomDistance;
            case RegionCard.RegionType.State:
                return plantZoomDistance;
            default:
                return continentZoomDistance;
        }
    }

    public void GoBack()
    {
        if (isZooming) return;

        // NUEVO: Desbloquear tarjetas actuales antes de ocultarlas
        if (currentVisibleCards != null)
        {
            foreach (var card in currentVisibleCards)
            {
                if (card != null)
                {
                    card.UnlockPosition();
                    card.gameObject.SetActive(false);
                    card.SetVisibility(false);
                }
            }
        }

        if (navigationStack.Count > 0)
        {
            navigationStack.Pop();

            if (navigationStack.Count > 0)
            {
                NavigationLevel previousLevel = navigationStack.Pop();
                
                StartCoroutine(ZoomToPosition(
                    previousLevel.cameraPosition,
                    previousLevel.cameraRotation,
                    previousLevel.visibleCards
                ));
            }
            else
            {
                StartCoroutine(ZoomToInitialView());
            }
        }
        else
        {
            StartCoroutine(ZoomToInitialView());
        }
    }

    private IEnumerator ZoomToPosition(Vector3 targetPos, Quaternion targetRot, RegionCard[] cardsToShow)
    {
        isZooming = true;
        
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float elapsed = 0;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / zoomDuration);
            
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        
        if (cardsToShow != null)
        {
            foreach (var card in cardsToShow)
            {
                if (card != null)
                {
                    card.gameObject.SetActive(true);
                }
            }
        }
        
        currentVisibleCards = cardsToShow;
        ShowCards(cardsToShow);
        
        isZooming = false;
    }

    private IEnumerator ZoomToInitialView()
    {
        isZooming = true;
        
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float elapsed = 0;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / zoomDuration);
            
            mainCamera.transform.position = Vector3.Lerp(startPos, initialCameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, initialCameraRotation, t);
            
            yield return null;
        }

        mainCamera.transform.position = initialCameraPosition;
        mainCamera.transform.rotation = initialCameraRotation;
        
        navigationStack.Clear();
        
        currentVisibleCards = continentCards;
        foreach (var card in continentCards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
            }
        }
        ShowCards(continentCards);
        
        autoRotate = true; // Reactivar rotación solo al volver a la vista inicial
        isZooming = false;
    }

    private void ShowCards(RegionCard[] cards)
    {
        if (cards == null || cards.Length == 0)
        {
            Debug.LogWarning("No hay tarjetas para mostrar");
            return;
        }
        
        foreach (var card in cards)
        {
            if (card != null)
            {
                card.SetVisibility(true);
                Debug.Log("Mostrando tarjeta: " + card.regionName);
            }
        }
    }

    private void HideCards(RegionCard[] cards)
    {
        if (cards == null) return;
        
        foreach (var card in cards)
        {
            if (card != null)
            {
                card.SetVisibility(false);
            }
        }
    }
}