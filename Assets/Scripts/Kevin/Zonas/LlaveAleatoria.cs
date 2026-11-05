using UnityEngine;

// Llave de las Cloacas: al empezar aparece en la mano de una estatua elegida al azar
// y hace un pequeno sonido cada cierto tiempo para que el jugador la pueda encontrar.
// Se agarra como cualquier objeto (Interactable > Grabbable > PickUp). Cuando sale de la mano deja de sonar.
[RequireComponent(typeof(AudioSource))]
public class LlaveAleatoria : MonoBehaviour
{
    // Manos posibles (un hijo "Mano" de cada estatua)
    public Transform[] manos;

    // Sonido de la llave. Vacio = un tintineo metalico generado por codigo
    public AudioClip sonido;
    [Range(0f, 1f)] public float volumenSonido = 0.8f;
    // Segundos entre sonidos (mas o menos la variacion, para que no sea tan mecanico)
    public float intervalo = 4f;
    public float variacion = 1f;

    public Transform ManoActual { get; private set; }

    private AudioSource audioSource;
    private float timer;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (sonido == null) sonido = CrearTintineo();

        ElegirMano();
        timer = Random.Range(0.5f, intervalo);
    }

    // Pone la llave en la mano de una estatua al azar
    public void ElegirMano()
    {
        int validas = 0;
        foreach (Transform m in manos) if (m != null) validas++;
        if (validas == 0)
        {
            Debug.LogWarning(name + ": no tiene manos asignadas, se queda donde esta.", this);
            return;
        }

        int elegida = Random.Range(0, validas);
        foreach (Transform m in manos)
        {
            if (m == null) continue;
            if (elegida-- == 0) { ManoActual = m; break; }
        }

        transform.SetParent(ManoActual, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // En la mano no tiene fisicas, sigue a la estatua
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        Debug.Log(name + ": la tiene " + ManoActual.root.name + " / " + ManoActual.parent.name, this);
    }

    void Update()
    {
        // Solo suena mientras la estatua la tiene
        if (ManoActual == null || !transform.IsChildOf(ManoActual)) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Mathf.Max(0.5f, intervalo + Random.Range(-variacion, variacion));
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(sonido, volumenSonido);
    }

    // Dos golpecitos metalicos cortos (como llaves chocando)
    static AudioClip CrearTintineo()
    {
        const int rate = 44100;
        const float largo = 0.45f;
        int n = Mathf.CeilToInt(rate * largo);
        float[] data = new float[n];
        float[] golpes = { 0f, 0.13f };
        float[] frecuencias = { 2093f, 3136f, 4186f, 5274f };
        float[] pesos = { 1f, 0.6f, 0.35f, 0.2f };

        for (int g = 0; g < golpes.Length; g++)
        {
            int inicio = Mathf.RoundToInt(golpes[g] * rate);
            float tono = g == 0 ? 1f : 1.12f;
            for (int i = inicio; i < n; i++)
            {
                float t = (i - inicio) / (float)rate;
                float s = 0f;
                for (int k = 0; k < frecuencias.Length; k++)
                    s += pesos[k] * Mathf.Sin(2f * Mathf.PI * frecuencias[k] * tono * t) * Mathf.Exp(-t * (18f + k * 8f));
                // Ataque muy corto para que no haga "click"
                data[i] += s * Mathf.Clamp01(t * 800f) * 0.3f;
            }
        }

        AudioClip clip = AudioClip.Create("Tintineo llave", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
