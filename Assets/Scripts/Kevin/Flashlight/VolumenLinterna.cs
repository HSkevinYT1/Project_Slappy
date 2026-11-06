using UnityEngine;
using UnityEngine.Rendering;

// Volume que se activa mientras la linterna esta encendida. Su perfil (LinternaVolume) quita el
// oscurecimiento de sombras y medios tonos de los Volumes de cada zona, para que la luz de la linterna
// se vea; lo que no ilumina sigue igual de oscuro. Va por encima de los demas (prioridad alta).
[RequireComponent(typeof(Volume))]
public class VolumenLinterna : MonoBehaviour
{
    // Vacio = busca la linterna de la escena
    public Flashlight linterna;
    // Segundos que tarda en aparecer o desaparecer
    public float transicion = 0.25f;

    private Volume volume;

    void Awake()
    {
        volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 0f;
    }

    void Update()
    {
        if (linterna == null) linterna = FindFirstObjectByType<Flashlight>();
        bool encendida = linterna != null && linterna.lightObject != null
                         && linterna.lightObject.activeInHierarchy;
        float paso = transicion > 0f ? Time.deltaTime / transicion : 1f;
        volume.weight = Mathf.MoveTowards(volume.weight, encendida ? 1f : 0f, paso);
    }
}
