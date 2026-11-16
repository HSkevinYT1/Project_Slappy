using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Trigger de un area (Laberinto, Backrooms, Cloacas, Zona Roja).
// Cuando el jugador entra, el VolumenZonas pasa a usar el perfil de esta area; al salir vuelve al anterior.
[RequireComponent(typeof(BoxCollider))]
public class ZonaVolumen : MonoBehaviour
{
    // Perfil (Assets/Volumenes) que se usa dentro de esta area
    public VolumeProfile perfil;
    // Volume que cambia. Vacio = busca el VolumenZonas de la escena
    public VolumenZonas volumen;
    // Si esta area usa la niebla (FogVolume) mientras el jugador esta adentro
    public bool niebla = true;

    // Colliders del jugador que estan dentro (el jugador puede tener varios)
    private readonly HashSet<Collider> dentro = new HashSet<Collider>();

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (volumen == null) volumen = FindAnyObjectByType<VolumenZonas>();
        if (volumen == null) Debug.LogWarning(name + ": no hay ningun VolumenZonas en la escena.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!EsJugador(other)) return;
        dentro.Add(other);
        if (dentro.Count == 1 && volumen != null) volumen.Entrar(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!dentro.Remove(other)) return;
        if (dentro.Count == 0 && volumen != null) volumen.Salir(this);
    }

    void OnDisable()
    {
        dentro.Clear();
        if (volumen != null) volumen.Salir(this);
    }

    // Jugador (etiqueta Player) o lo que lleve la camara principal
    static bool EsJugador(Collider c)
    {
        for (Transform t = c.transform; t != null; t = t.parent)
            if (t.CompareTag("Player")) return true;
        Camera cam = Camera.main;
        return cam != null && cam.transform.IsChildOf(c.transform.root);
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
