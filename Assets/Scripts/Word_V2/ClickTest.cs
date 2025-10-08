using UnityEngine;

public class ClickTest : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("🖱️ CLICK DETECTADO GLOBALMENTE");
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            Debug.Log($"📍 Mouse Position: {Input.mousePosition}");
            
            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log($"✅ RAYCAST GOLPEÓ: {hit.collider.gameObject.name}");
                Debug.Log($"📍 UV Coordinates: {hit.textureCoord}");
            }
            else
            {
                Debug.Log("❌ RAYCAST NO GOLPEÓ NADA");
            }
        }
    }
}