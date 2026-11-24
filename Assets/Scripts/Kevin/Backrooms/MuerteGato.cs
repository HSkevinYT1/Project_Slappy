using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Muerte del gato de sombra de los Backrooms (la usa PantallaMuerte en vez de su giro normal):
// el jugador voltea a ver hacia el gato, el gato se agazapa y SALTA hacia su cara; suena "sonido kill" (solo lo que
// dura la accion: se corta con un fundido mientras la pantalla se va a negro), la camara tiembla y la imagen se llena
// de estatica y cortes (glitch). Despues sigue la pantalla de muerte de siempre. Va en el gato (lo agrega BackroomsGuion).
public class MuerteGato : MonoBehaviour
{
    [Tooltip("Sonido del salto (sonido kill enemigo 2 y 4): empieza al saltar y se corta al irse a negro")]
    public AudioClip sonidoKill;
    [Tooltip("Segundo del audio desde el que empieza (se salta el silencio del principio)")]
    public float inicioKill;
    [Range(0f, 1f)] public float volumenKill = 1f;
    [Tooltip("Audio de estatica (opcional)")]
    public AudioClip estatica;
    [Range(0f, 1f)] public float volumenEstatica = 0.8f;
    public float duracionGiro = 0.42f, duracionAgazapado = 0.22f, duracionSalto = 0.32f, duracionImpacto = 0.5f;
    [Tooltip("Segundos que sigue sonando el kill despues de saltar encima (mientras la pantalla se va a negro)")]
    public float colaKill = 0.9f;

    // Con sonido propio la pantalla de muerte no pone el suyo
    public bool TieneSonido => sonidoKill != null;

    AudioSource fuenteKill;

    public IEnumerator Animar(Transform cam, Transform cuerpo)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) { agent.isStopped = true; agent.updatePosition = false; agent.updateRotation = false; }
        Camera c = cam.GetComponent<Camera>();
        float fov = c != null ? c.fieldOfView : 60f;

        // Donde esta el cuerpo del gato respecto a su raiz (el modelo cuelga del agente): se mantiene al moverlo
        Bounds b0 = Limites();
        Vector3 offsetLocal = Quaternion.Inverse(transform.rotation) * (b0.center - transform.position);
        Vector3 Centro() => transform.position + transform.rotation * offsetLocal;
        void PonerCentro(Vector3 centro, Quaternion rot) => transform.SetPositionAndRotation(centro - rot * offsetLocal, rot);

        // Si esta pegado, se lo aparta un poco hacia atras (asi el salto se ve), sin meterlo en una pared
        Vector3 haciaGato = Centro() - cam.position; haciaGato.y = 0f;
        if (haciaGato.sqrMagnitude < 0.01f) haciaGato = cam.forward;
        haciaGato.y = 0f; haciaGato.Normalize();
        float dist = new Vector2(Centro().x - cam.position.x, Centro().z - cam.position.z).magnitude;
        if (dist < 1.5f)
        {
            float atras = 1.5f - dist;
            Vector3 suelo = new Vector3(Centro().x, b0.min.y + 0.1f, Centro().z);
            if (Physics.Raycast(suelo, haciaGato, out RaycastHit hp, atras + 0.3f, ~0, QueryTriggerInteraction.Ignore)) atras = Mathf.Max(0f, hp.distance - 0.3f);
            PonerCentro(Centro() + haciaGato * atras, Quaternion.LookRotation(-haciaGato, Vector3.up));
        }
        else transform.rotation = Quaternion.LookRotation(-haciaGato, Vector3.up);

        // ---- 1. Voltea a ver hacia el gato
        Quaternion rotIni = cam.rotation;
        Glitch glitch = new Glitch();
        for (float t = 0f; t < duracionGiro; t += Time.deltaTime)
        {
            float k = Jugo.SalidaCubica(t / duracionGiro);
            cam.rotation = Quaternion.Slerp(rotIni, Quaternion.LookRotation(Cabeza() - cam.position), k);
            yield return null;
        }

        // ---- 2. Se agazapa (baja un poco y se echa atras)
        Vector3 inicio = Centro();
        Quaternion rotGato = transform.rotation;
        for (float t = 0f; t < duracionAgazapado; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / duracionAgazapado) * Mathf.PI * 0.5f);
            PonerCentro(inicio - Vector3.up * 0.06f * k - transform.forward * 0.05f * k, rotGato);
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(Cabeza() - cam.position), 1f - Mathf.Exp(-14f * Time.deltaTime));
            yield return null;
        }

        // ---- 3. Salta a la cara: suena el kill
        Sonar();
        if (estatica != null) AudioSource.PlayClipAtPoint(estatica, cam.position, volumenEstatica);
        Vector3 desde = Centro();
        Vector3 cara = cam.position + cam.forward * 0.55f - Vector3.up * 0.05f;
        Quaternion rotCara = Quaternion.LookRotation(cam.position - cara, Vector3.up);
        for (float t = 0f; t < duracionSalto; t += Time.deltaTime)
        {
            float k = Jugo.EntradaCubica(t / duracionSalto);
            Vector3 p = Vector3.Lerp(desde, cara, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.3f);
            PonerCentro(p, Quaternion.Slerp(rotGato * Quaternion.Euler(-25f, 0f, 0f), rotCara, k));
            if (c != null) c.fieldOfView = Mathf.Lerp(fov, fov - 8f, k);
            glitch.Nivel(0.35f * k);
            yield return null;
        }
        PonerCentro(cara, rotCara);
        Jugo.Sacudir(0.8f);

        // ---- 4. Encima: temblor fuerte, estatica y cortes
        for (float t = 0f; t < duracionImpacto; t += Time.deltaTime)
        {
            float k = t / duracionImpacto;
            Vector3 temblor = new Vector3(Mathf.PerlinNoise(t * 38f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 41f) - 0.5f, 0f) * (16f * (1f - k * 0.5f));
            cam.rotation = Quaternion.LookRotation(Centro() - cam.position) * Quaternion.Euler(temblor.y, temblor.x, (Mathf.PerlinNoise(t * 22f, 5f) - 0.5f) * 14f);
            PonerCentro(cara + Random.insideUnitSphere * 0.015f, rotCara);
            glitch.Nivel(Mathf.Lerp(0.6f, 0.9f, k));
            yield return null;
        }

        // La pantalla de muerte sigue (a negro); el kill se apaga y todo vuelve a su sitio cuando ya no se ve
        StartCoroutine(Limpiar(agent, c, fov, glitch));
    }

    // El kill empieza en el salto y solo dura la accion (salto + impacto + un poco mientras se va a negro)
    void Sonar()
    {
        if (sonidoKill == null) return;
        var go = new GameObject("Sonido Kill Gato");
        fuenteKill = go.AddComponent<AudioSource>();
        fuenteKill.clip = sonidoKill;
        fuenteKill.volume = volumenKill;
        fuenteKill.spatialBlend = 0f;
        fuenteKill.priority = 0;
        fuenteKill.time = Mathf.Clamp(inicioKill, 0f, Mathf.Max(0f, sonidoKill.length - 0.05f));
        fuenteKill.Play();
        StartCoroutine(CortarKill(fuenteKill, duracionSalto + duracionImpacto + colaKill));
    }

    static IEnumerator CortarKill(AudioSource a, float duracion)
    {
        const float fundido = 0.4f;
        yield return new WaitForSeconds(Mathf.Max(0f, duracion - fundido));
        float v0 = a != null ? a.volume : 0f;
        for (float t = 0f; t < fundido && a != null; t += Time.deltaTime) { a.volume = v0 * (1f - t / fundido); yield return null; }
        if (a != null) Destroy(a.gameObject);
    }

    Bounds Limites()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.3f);
        bool hay = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Donde mirar: la cabeza del gato (la parte de adelante y arriba de su modelo)
    Vector3 Cabeza()
    {
        Bounds b = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one * 0.5f);
        bool hay = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return b.center + transform.forward * b.extents.z * 0.6f + Vector3.up * b.extents.y * 0.45f;
    }

    IEnumerator Limpiar(NavMeshAgent agent, Camera c, float fov, Glitch glitch)
    {
        for (float t = 0f; t < 1.3f; t += Time.deltaTime) { glitch.Nivel(Mathf.Lerp(0.8f, 0f, t / 1.3f)); yield return null; }
        glitch.Destruir();
        if (c != null) c.fieldOfView = fov;
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (agent.isOnNavMesh) agent.Warp(agent.nextPosition);
        }
    }

    // Estatica y cortes a pantalla completa
    class Glitch
    {
        readonly Canvas canvas;
        readonly RawImage ruido;
        readonly Image[] barras = new Image[6];
        readonly Image tinte;

        public Glitch()
        {
            canvas = HUD.CrearCanvas("Glitch Gato", 90);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            ruido = HUD.Llenar(canvas.transform, "Estatica").gameObject.AddComponent<RawImage>();
            ruido.texture = HUD.Ruido();
            ruido.raycastTarget = false;
            ruido.color = new Color(1f, 1f, 1f, 0f);
            tinte = HUD.Imagen(HUD.Llenar(canvas.transform, "Tinte"), new Color(0.4f, 0f, 0f, 0f));
            for (int i = 0; i < barras.Length; i++)
                barras[i] = HUD.Imagen(HUD.Rect(canvas.transform, "Corte " + i, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 10f)), new Color(0f, 0f, 0f, 0f));
        }

        public void Nivel(float k)
        {
            if (canvas == null) return;
            float parpadeo = Random.value < 0.25f ? 1.4f : 1f;
            ruido.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 0.55f * parpadeo));
            ruido.uvRect = new Rect(Random.value, Random.value, 2.5f, 1.6f);
            tinte.color = new Color(0.45f, 0f, 0.02f, k * 0.25f);
            foreach (Image b in barras)
            {
                bool ver = Random.value < k * 0.6f;
                b.color = ver ? (Random.value < 0.5f ? new Color(0f, 0f, 0f, 0.85f) : new Color(0.85f, 0.9f, 0.9f, 0.35f)) : Color.clear;
                RectTransform r = b.rectTransform;
                r.anchoredPosition = new Vector2(Random.Range(-60f, 60f), Random.Range(-340f, 340f));
                r.sizeDelta = new Vector2(Random.Range(-100f, 40f), Random.Range(3f, 26f));
            }
        }

        public void Destruir()
        {
            if (canvas != null) Object.Destroy(canvas.gameObject);
        }
    }
}
