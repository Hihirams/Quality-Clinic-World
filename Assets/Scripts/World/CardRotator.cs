using UnityEngine;

public class CardRotator : MonoBehaviour
{
    [Header("Referencias")]
    public Transform planet;
    
    [Header("Configuración")]
    public Vector3 offsetFromPlanet = new Vector3(0, 1, 3);
    
    private Vector3 localPosition;
    private Quaternion localRotation;
    private RegionCard regionCard;
    private bool shouldRotate = false;

    void Start()
    {
        if (planet == null)
        {
            planet = GameObject.Find("Planet").transform;
        }

        // Auto-detectar si esta tarjeta debe rotar con el planeta
        regionCard = GetComponent<RegionCard>();
        if (regionCard != null)
        {
            // SOLO los continentes rotan con el planeta
            shouldRotate = (regionCard.regionType == RegionCard.RegionType.Continent);
            
            if (!shouldRotate)
            {
                Debug.Log($"[CardRotator] {regionCard.regionName} ({regionCard.regionType}) - Rotación DESACTIVADA (no es continente)");
                // Desactivar este componente para tarjetas no-continente
                this.enabled = false;
                return;
            }
            
            Debug.Log($"[CardRotator] {regionCard.regionName} - Rotación ACTIVADA (es continente)");
        }

        // Guardar posición local solo para continentes
        if (shouldRotate && planet != null)
        {
            localPosition = planet.InverseTransformPoint(transform.position);
            localRotation = Quaternion.Inverse(planet.rotation) * transform.rotation;
        }
    }

    void LateUpdate()
    {
        // Solo ejecutar si debe rotar (es continente)
        if (shouldRotate && planet != null)
        {
            // Mantener la posición relativa al planeta mientras rota
            transform.position = planet.TransformPoint(localPosition);
        }
    }

    public void UpdateLocalPosition()
    {
        if (planet != null && shouldRotate)
        {
            localPosition = planet.InverseTransformPoint(transform.position);
        }
    }
}