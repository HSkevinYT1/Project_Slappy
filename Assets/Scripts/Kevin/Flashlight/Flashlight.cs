using System.Collections;
using UnityEngine;

// Linterna. Jugo: al prenderse parpadea un instante (como una bombita que agarra), con poca pila falla de vez en
// cuando, y al quedarse sin pila se apaga a los tirones en vez de cortarse en seco.
public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    public float battery = 100f;
    public float maxBattery = 100f;
    // Energia que gasta por segundo encendida
    public float drainPerSecond = 2f;

    [Header("Parpadeos")]
    [Tooltip("Por debajo de esta carga (0..1) la luz falla de vez en cuando")]
    [Range(0f, 1f)] public float cargaBaja = 0.15f;

    public float BatteryPercent => maxBattery > 0f ? Mathf.Clamp01(battery / maxBattery) : 0f;

    private Grabbable grab;
    private PlayerInteractor interactor;
    private Light[] luces;
    private float[] intensidades;
    private Coroutine parpadeo;
    private bool apagandose;
    private float proximaFalla;

    void Start()
    {
        source = GetComponent<AudioSource>();
        grab = GetComponentInParent<Grabbable>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
        if (lightObject != null)
        {
            luces = lightObject.GetComponentsInChildren<Light>(true);
            intensidades = new float[luces.Length];
            for (int i = 0; i < luces.Length; i++) intensidades[i] = luces[i].intensity;
        }
    }

    void Update()
    {
        // Solo se puede prender/apagar si el jugador la tiene en la mano
        if (GameInput.Flashlight && IsHeld() && !apagandose)
        {
            LightManager();
        }

        // Descargar bateria
        if (lightObject.activeSelf && !apagandose)
        {
            battery -= drainPerSecond * Time.deltaTime;

            if (battery <= 0)
            {
                battery = 0;
                Parpadear(Apagarse());
            }
            else if (BatteryPercent < cargaBaja && Time.time > proximaFalla && parpadeo == null)
            {
                // Con poca pila: un fallo corto cada tanto (mas seguido cuanto menos carga queda)
                proximaFalla = Time.time + Random.Range(1.5f, 5f) * Mathf.Lerp(0.4f, 1f, BatteryPercent / Mathf.Max(0.01f, cargaBaja));
                Parpadear(Falla());
            }
        }
    }

    // La llaman las pilas (Bateria.Recoger)
    public void Recharge(float amount)
    {
        battery = Mathf.Min(maxBattery, battery + amount);
        proximaFalla = Time.time + 2f;
    }

    bool IsHeld()
    {
        // Si la linterna no se puede agarrar, se asume que siempre esta en la mano
        if (grab == null) return true;
        return interactor != null && interactor.Held == grab;
    }

    void LightManager()
    {
        if (lightObject.activeSelf)
        {
            Detener();
            lightObject.SetActive(false);
        }
        else
        {
            if (battery > 0)
            {
                lightObject.SetActive(true);
                Parpadear(Encenderse());
            }
        }

        if (source != null)
        {
            if (lightSound != null)
            {
                source.PlayOneShot(lightSound);
            }
        }
    }

    // ------------------------------------------------------------------ Parpadeos

    void Parpadear(IEnumerator rutina)
    {
        if (luces == null || luces.Length == 0 || !isActiveAndEnabled) { if (rutina != null && battery <= 0f) lightObject.SetActive(false); return; }
        Detener();
        parpadeo = StartCoroutine(rutina);
    }

    void Detener()
    {
        if (parpadeo != null) StopCoroutine(parpadeo);
        parpadeo = null;
        apagandose = false;
        Nivel(1f);
    }

    void Nivel(float k)
    {
        if (luces == null) return;
        for (int i = 0; i < luces.Length; i++) if (luces[i] != null) luces[i].intensity = intensidades[i] * k;
    }

    // Prende con un par de titileos rapidos
    IEnumerator Encenderse()
    {
        float[] niveles = { 0.15f, 1f, 0.35f, 1.1f, 1f };
        float[] tiempos = { 0.03f, 0.04f, 0.035f, 0.05f, 0f };
        for (int i = 0; i < niveles.Length; i++)
        {
            Nivel(niveles[i]);
            if (tiempos[i] > 0f) yield return new WaitForSeconds(tiempos[i]);
        }
        Nivel(1f);
        parpadeo = null;
    }

    // Fallo corto con poca pila
    IEnumerator Falla()
    {
        int veces = Random.Range(1, 4);
        for (int i = 0; i < veces; i++)
        {
            Nivel(Random.Range(0f, 0.25f));
            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            Nivel(Random.Range(0.6f, 1f));
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }
        Nivel(1f);
        parpadeo = null;
    }

    // Sin pila: unos tirones cada vez mas debiles y se apaga
    IEnumerator Apagarse()
    {
        apagandose = true;
        float[] niveles = { 0.3f, 0.8f, 0.1f, 0.5f, 0.05f, 0.25f, 0f };
        foreach (float n in niveles)
        {
            Nivel(n);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }
        lightObject.SetActive(false);
        apagandose = false;
        Nivel(1f);
        parpadeo = null;
    }

    // Al guardarla en el inventario (se desactiva) se cortan los parpadeos y vuelve a su brillo normal
    void OnDisable()
    {
        bool sinPila = apagandose;
        parpadeo = null;
        apagandose = false;
        Nivel(1f);
        if (sinPila && lightObject != null) lightObject.SetActive(false);
    }
}
