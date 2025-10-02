using UnityEngine;
using TMPro;

/// <summary>
/// Utilidad para crear tarjetas de región rápidamente
/// USO: Agregar este script a un GameObject vacío en la escena
/// Asignar el parent (Cards_Root) y presionar los botones en el Inspector
/// </summary>
public class CardFactory : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Transform cardsParent; // Cards_Root
    [SerializeField] private GameObject planet;
    
    [Header("Crear Tarjeta Manual")]
    [SerializeField] private string cardName = "Nueva Región";
    [SerializeField] private Vector3 positionFromCenter = new Vector3(0, 2, 5);
    [SerializeField] private RegionCard.RegionType cardType = RegionCard.RegionType.Continent;
    
    void Start()
    {
        if (planet == null)
            planet = GameObject.Find("Planet");
            
        if (cardsParent == null)
            cardsParent = GameObject.Find("Cards_Root")?.transform;
    }
    
    /// <summary>
    /// Crear una tarjeta nueva (llamar desde botón personalizado o script)
    /// </summary>
    [ContextMenu("Crear Tarjeta")]
    public GameObject CreateCard()
    {
        if (cardsParent == null)
        {
            Debug.LogError("❌ No se asignó Cards_Root!");
            return null;
        }
        
        // Crear GameObject principal
        GameObject cardObj = new GameObject($"Card_{cardName}");
        cardObj.transform.SetParent(cardsParent);
        
        // Posicionar en el espacio mundial
        if (planet != null)
        {
            float planetRadius = planet.transform.localScale.x / 2f;
            Vector3 direction = positionFromCenter.normalized;
            cardObj.transform.position = planet.transform.position + direction * (planetRadius + 2f);
        }
        else
        {
            cardObj.transform.position = positionFromCenter;
        }
        
        // Agregar componente RegionCard
        RegionCard regionCard = cardObj.AddComponent<RegionCard>();
        regionCard.regionName = cardName;
        regionCard.regionType = cardType;
        
        // Crear el fondo de la tarjeta (Quad)
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "CardBackground";
        background.transform.SetParent(cardObj.transform);
        background.transform.localPosition = Vector3.zero;
        background.transform.localRotation = Quaternion.identity;
        background.transform.localScale = new Vector3(2f, 0.5f, 1f);
        
        // Configurar material del fondo
        MeshRenderer bgRenderer = background.GetComponent<MeshRenderer>();
        Material bgMaterial = new Material(Shader.Find("Unlit/Color"));
        bgMaterial.color = new Color(1f, 1f, 1f, 0.9f);
        bgRenderer.material = bgMaterial;
        
        // Eliminar el collider del quad (usaremos uno en el parent)
        Destroy(background.GetComponent<Collider>());
        
        // Crear el texto
        GameObject textObj = new GameObject("CardText");
        textObj.transform.SetParent(cardObj.transform);
        textObj.transform.localPosition = Vector3.zero;
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;
        
        TextMeshPro textMesh = textObj.AddComponent<TextMeshPro>();
        textMesh.text = cardName;
        textMesh.fontSize = regionCard.fontSize;
        textMesh.color = Color.black;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        
        // Agregar collider para detectar clicks
        BoxCollider collider = cardObj.AddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 0.5f, 0.1f);
        
        // Asignar referencias al RegionCard
        regionCard.labelText = textMesh;
        regionCard.cardBackground = background;
        
        Debug.Log($"✅ Tarjeta creada: {cardName}");
        
        return cardObj;
    }
    
    /// <summary>
    /// Crear set completo de continentes con posiciones predefinidas
    /// </summary>
    [ContextMenu("Crear Set de Continentes")]
    public void CreateContinentSet()
    {
        // Definiciones de continentes con posiciones aproximadas
        var continents = new[]
        {
            new { name = "África", pos = new Vector3(0.3f, 0f, 1f) },
            new { name = "Europa", pos = new Vector3(0.5f, 0.2f, 0.8f) },
            new { name = "Asia", pos = new Vector3(1f, 0.1f, 0.5f) },
            new { name = "América del Norte", pos = new Vector3(-0.8f, 0.3f, 0.5f) },
            new { name = "América del Sur", pos = new Vector3(-0.5f, -0.3f, 0.8f) },
            new { name = "Oceanía", pos = new Vector3(1f, -0.4f, -0.3f) }
        };
        
        foreach (var continent in continents)
        {
            cardName = continent.name;
            positionFromCenter = continent.pos;
            cardType = RegionCard.RegionType.Continent;
            CreateCard();
        }
        
        Debug.Log("✅ Set completo de continentes creado!");
    }
}