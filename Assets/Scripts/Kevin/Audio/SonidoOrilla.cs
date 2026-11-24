using UnityEngine;

// Olas en la orilla (Zona Roja): UNA sola fuente 3D que sigue al punto de la orilla mas cercano al oyente.
// Asi el mar se escucha en toda la costa (fuerte junto al agua y apagandose tierra adentro)
// sin tener decenas de fuentes sonando a la vez. La arma Tools > Zona Roja > Rehacer orilla (ZonaRojaOrillaSetup).
[RequireComponent(typeof(AudioSource))]
public class SonidoOrilla : MonoBehaviour
{
    [Tooltip("Contorno de la orilla: puntos locales al PADRE de este objeto, en orden (se cierra solo)")]
    public Vector3[] orilla;
    [Tooltip("Que tan rapido se acomoda la fuente cuando el punto mas cercano salta (ej: al cruzar una esquina)")]
    public float suavizado = 4f;

    AudioListener oyente;
    float proximaBusqueda;
    bool colocada;

    void OnEnable() => colocada = false;

    void LateUpdate()
    {
        if (orilla == null || orilla.Length < 2) return;
        Transform o = Oyente();
        if (o == null) return;

        Transform padre = transform.parent;
        Vector3 local = padre ? padre.InverseTransformPoint(o.position) : o.position;
        Vector3 punto = PuntoMasCercano(local);
        Vector3 mundo = padre ? padre.TransformPoint(punto) : punto;

        if (!colocada) { transform.position = mundo; colocada = true; }
        else transform.position = Vector3.Lerp(transform.position, mundo, 1f - Mathf.Exp(-suavizado * Time.unscaledDeltaTime));
    }

    Transform Oyente()
    {
        if (oyente && oyente.isActiveAndEnabled) return oyente.transform;
        if (Time.unscaledTime < proximaBusqueda) return null;
        proximaBusqueda = Time.unscaledTime + 1f;
        oyente = FindFirstObjectByType<AudioListener>();
        return oyente ? oyente.transform : null;
    }

    // Punto del contorno mas cercano en planta (XZ); la altura sale del propio contorno.
    Vector3 PuntoMasCercano(Vector3 p)
    {
        Vector3 mejor = orilla[0];
        float dMejor = float.MaxValue;
        int n = orilla.Length;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = orilla[i], b = orilla[(i + 1) % n];
            float abx = b.x - a.x, abz = b.z - a.z;
            float largo = abx * abx + abz * abz;
            float t = largo > 1e-6f ? Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / largo) : 0f;
            Vector3 q = Vector3.LerpUnclamped(a, b, t);
            float d = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z);
            if (d < dMejor) { dMejor = d; mejor = q; }
        }
        return mejor;
    }
}
