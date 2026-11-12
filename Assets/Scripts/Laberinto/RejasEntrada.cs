using System.Collections;
using UnityEngine;

// Reja de la entrada del laberinto: cuando el jugador entra en la zona, la reja cae de golpe detras de el,
// suena un estruendo, la camara tiembla y el jugador se da vuelta para mirarla. Pasa una sola vez.
// Despues la reja queda cerrada (su collider no deja volver al pasillo).
public class RejasEntrada : MonoBehaviour
{
    public Transform reja;
    [Tooltip("Al entrar el jugador aqui cae la reja")]
    public BoxCollider zona;
    [Tooltip("Metros que baja la reja")]
    public float caida = 4.3f;
    public float duracionCaida = 0.38f;
    public AudioClip estruendo;
    [Range(0f, 1f)] public float volumen = 1f;

    [Header("Camara")]
    public float duracionTemblor = 0.9f;
    public float fuerzaTemblor = 0.12f;
    public float duracionGiro = 0.9f;
    [Tooltip("Segundos mirando la reja antes de devolver el control")]
    public float pausa = 0.8f;

    public bool Usada { get; private set; }

    Transform jugador;

    void Update()
    {
        if (Usada || !zona || !reja) return;
        if (!jugador)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (!pm) return;
            jugador = pm.transform;
        }
        if (zona.bounds.Contains(jugador.position)) StartCoroutine(Secuencia());
    }

    // Sube la reja otra vez (ej: al resolver el puzzle del centro), despacio y con el estruendo de fondo
    public void Abrir()
    {
        if (!reja) return;
        bool cayo = Usada;
        Usada = true;          // si todavia no habia caido, ya no cae
        if (cayo) StartCoroutine(Subir());
    }

    IEnumerator Subir()
    {
        if (abierta) yield break;
        abierta = true;
        if (alturaArriba == null) alturaArriba = reja.position.y + caida;
        Vector3 desde = reja.position, hasta = new Vector3(desde.x, alturaArriba.Value, desde.z);
        if (estruendo) Estruendo();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 3.5f)
        {
            // Sube a tirones, como con una cadena
            float k = Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * 40f) * 0.004f;
            reja.position = Vector3.Lerp(desde, hasta, k);
            yield return null;
        }
        reja.position = hasta;
    }

    bool abierta;
    float? alturaArriba;

    // Suena fuerte aunque el jugador ya este lejos de la reja: casi sin atenuacion por distancia,
    // apenas un poco ubicado hacia la reja, con maxima prioridad para que no lo tape otro sonido.
    void Estruendo()
    {
        var go = new GameObject("Estruendo Reja");
        go.transform.position = reja.position + Vector3.up * 1.5f;
        var a = go.AddComponent<AudioSource>();
        a.clip = estruendo;
        a.volume = volumen;
        a.priority = 0;
        a.spatialBlend = 0.25f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 30f;
        a.maxDistance = 120f;
        a.dopplerLevel = 0f;
        a.Play();
        Destroy(go, estruendo.length + 0.2f);
    }

    IEnumerator Secuencia()
    {
        Usada = true;
        alturaArriba = reja.position.y;
        var movimiento = jugador.GetComponent<PlayerMovement>();
        var rb = jugador.GetComponent<Rigidbody>();
        var mirada = FindFirstObjectByType<MouseLook>();
        var bob = FindFirstObjectByType<HeadBob>();
        var interactor = FindFirstObjectByType<PlayerInteractor>();
        Camera cam = Camera.main;

        // Cae la reja (acelerando, como si pesara)
        Vector3 arriba = reja.position, abajo = arriba + Vector3.down * caida;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracionCaida))
        {
            reja.position = Vector3.Lerp(arriba, abajo, t * t);
            yield return null;
        }
        reja.position = abajo;

        // Golpe: estruendo, el jugador se queda quieto y gira a mirarla mientras tiembla la camara
        if (estruendo) Estruendo();
        bool movia = movimiento && movimiento.enabled, miraba = mirada && mirada.enabled, bobeaba = bob && bob.enabled;
        bool interactuaba = interactor && interactor.enabled;
        if (movimiento) movimiento.enabled = false;
        if (mirada) mirada.enabled = false;
        if (bob) bob.enabled = false;
        if (interactor) interactor.enabled = false;
        if (rb) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        Transform camT = cam ? cam.transform : null;
        Vector3 camPos = camT ? camT.localPosition : Vector3.zero;
        Quaternion camRot = camT ? camT.localRotation : Quaternion.identity;

        Vector3 haciaReja = reja.position - jugador.position; haciaReja.y = 0f;
        float yawInicio = jugador.eulerAngles.y;
        float yawFin = haciaReja.sqrMagnitude > 0.01f ? Quaternion.LookRotation(haciaReja).eulerAngles.y : yawInicio + 180f;
        float total = Mathf.Max(duracionGiro, duracionTemblor) + pausa;

        // Rebote corto de la reja al pegar en el piso
        float rebote = 0f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            // Giro del cuerpo hacia la reja y la mirada a la altura de los ojos
            float k = Mathf.Clamp01(t / duracionGiro);
            k = k * k * (3f - 2f * k);
            Quaternion rot = Quaternion.Euler(0f, Mathf.LerpAngle(yawInicio, yawFin, k), 0f);
            if (rb) rb.MoveRotation(rot); else jugador.rotation = rot;

            // Temblor que se apaga
            float f = Mathf.Clamp01(1f - t / duracionTemblor);
            f *= f;
            if (camT)
            {
                Vector3 sacudida = new Vector3(Mathf.PerlinNoise(t * 28f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 31f) - 0.5f, 0f) * (2f * fuerzaTemblor * f);
                camT.localPosition = camPos + sacudida;
                camT.localRotation = Quaternion.Slerp(camRot, Quaternion.identity, k) * Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 22f, 5f) - 0.5f) * 6f * f);
            }

            rebote = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI) * 0.06f : 0f;
            reja.position = abajo + Vector3.up * rebote;
            yield return null;
        }
        reja.position = abajo;

        // Devolver el control
        Quaternion final = Quaternion.Euler(0f, yawFin, 0f);
        if (rb) { rb.MoveRotation(final); rb.rotation = final; }
        jugador.rotation = final;
        if (camT) { camT.localPosition = camPos; camT.localRotation = Quaternion.identity; }
        yield return new WaitForFixedUpdate();
        if (mirada && miraba) mirada.enabled = true;   // al activarse toma el nuevo giro del cuerpo
        if (bob && bobeaba) bob.enabled = true;
        if (interactor && interactuaba) interactor.enabled = true;
        if (movimiento && movia) movimiento.enabled = true;
    }
}
