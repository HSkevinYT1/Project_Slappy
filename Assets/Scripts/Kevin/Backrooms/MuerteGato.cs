using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Muerte del gato de sombra de los Backrooms (la usa PantallaMuerte en vez de su giro normal):
// el gato empuja al protagonista al suelo (la camara cae y queda mirando para arriba), se le sube al pecho y lo
// mira a los ojos; la camara tiembla, hace zoom y la imagen se llena de estatica y cortes (glitch).
// Despues sigue la pantalla de muerte de siempre. Va en el gato (lo agrega BackroomsGuion).
public class MuerteGato : MonoBehaviour
{
    [Tooltip("Audio de estatica (opcional)")]
    public AudioClip estatica;
    [Range(0f, 1f)] public float volumenEstatica = 0.8f;
    public float duracionCaida = 0.38f, duracionSalto = 0.45f, duracionMirada = 1.5f;

    public IEnumerator Animar(Transform cam, Transform cuerpo)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) { agent.isStopped = true; agent.updatePosition = false; agent.updateRotation = false; }
        Camera c = cam.GetComponent<Camera>();
        float fov = c != null ? c.fieldOfView : 60f;

        // ---- 1. Cae al suelo de espaldas: la camara baja y queda mirando hacia arriba, hacia el gato
        Vector3 haciaGato = transform.position - cam.position; haciaGato.y = 0f;
        if (haciaGato.sqrMagnitude < 0.01f) haciaGato = cam.forward;
        haciaGato.Normalize();
        float suelo = cuerpo != null ? cuerpo.position.y : cam.position.y - 1.6f;
        if (Physics.Raycast(cam.position, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)) suelo = h.point.y;
        Vector3 posSuelo = new Vector3(cam.position.x, suelo + 0.28f, cam.position.z) - haciaGato * 0.3f;
        Quaternion rotSuelo = Quaternion.LookRotation(haciaGato, Vector3.up) * Quaternion.Euler(-62f, 0f, 14f);
        Vector3 posIni = cam.position;
        Quaternion rotIni = cam.rotation;
        for (float t = 0f; t < duracionCaida; t += Time.deltaTime)
        {
            float k = Jugo.EntradaCubica(t / duracionCaida);
            cam.SetPositionAndRotation(Vector3.Lerp(posIni, posSuelo, k), Quaternion.Slerp(rotIni, rotSuelo, k));
            yield return null;
        }
        cam.SetPositionAndRotation(posSuelo, rotSuelo);
        Jugo.Sacudir(0.7f);
        Glitch glitch = new Glitch();
        if (estatica != null) AudioSource.PlayClipAtPoint(estatica, cam.position, volumenEstatica);

        // ---- 2. El gato salta y se le sube al pecho, de frente
        Vector3 desde = transform.position;
        Vector3 pecho = new Vector3(posSuelo.x, suelo, posSuelo.z) + haciaGato * 0.55f;
        Quaternion rotGato = Quaternion.LookRotation(-haciaGato, Vector3.up);
        Quaternion rotDesde = transform.rotation;
        for (float t = 0f; t < duracionSalto; t += Time.deltaTime)
        {
            float k = Jugo.SalidaCubica(t / duracionSalto);
            Vector3 p = Vector3.Lerp(desde, pecho, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.45f + k * 0.12f);
            transform.SetPositionAndRotation(p, Quaternion.Slerp(rotDesde, rotGato, k));
            cam.rotation = Quaternion.Slerp(rotSuelo, Quaternion.LookRotation(Cabeza() - cam.position), k);
            glitch.Nivel(0.25f * k);
            yield return null;
        }
        Jugo.Sacudir(0.5f);

        // ---- 3. Lo mira a los ojos: zoom lento, temblor y estatica cada vez mas fuerte
        for (float t = 0f; t < duracionMirada; t += Time.deltaTime)
        {
            float k = t / duracionMirada;
            Vector3 temblor = new Vector3(Mathf.PerlinNoise(t * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 33f) - 0.5f, 0f) * (4f + 10f * k);
            cam.rotation = Quaternion.LookRotation(Cabeza() - cam.position) * Quaternion.Euler(temblor.y, temblor.x, (Mathf.PerlinNoise(t * 20f, 5f) - 0.5f) * 10f * k);
            if (c != null) c.fieldOfView = Mathf.Lerp(fov, fov - 18f, Jugo.SalidaCubica(k));
            glitch.Nivel(Mathf.Lerp(0.25f, 0.8f, k));
            yield return null;
        }

        // La pantalla de muerte sigue (a negro); cuando ya no se ve, todo vuelve a su sitio
        StartCoroutine(Limpiar(agent, c, fov, glitch));
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
