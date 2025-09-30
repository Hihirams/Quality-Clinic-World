using UnityEngine;

public class GlobeSpinner : MonoBehaviour
{
    [Header("Spin")]
    public float idleSpinDegPerSec = 5f;   // velocidad de giro automática
    public float damp = 5f;                // qué tan rápido se frena el spin manual
    public float idleAfterSeconds = 2f;    // tiempo sin input antes de girar solo

    float lastUserInput = 0f;
    float manualSpin = 0f;

    void Update()
    {
        // detectar si el jugador mueve el mouse o presiona teclas
        bool anyInput = Mathf.Abs(Input.GetAxis("Mouse X")) > 0.001f ||
                        Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.001f ||
                        Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
                        Input.anyKey;

        if (anyInput)
        {
            lastUserInput = Time.time;
            manualSpin = 60f; // impulso breve
        }
        else
        {
            manualSpin = Mathf.MoveTowards(manualSpin, 0f, damp * Time.deltaTime);
        }

        float spin = (Time.time - lastUserInput > idleAfterSeconds)
                   ? idleSpinDegPerSec   // spin automático
                   : manualSpin;         // spin con inercia de usuario

        transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.Self);
    }
}
