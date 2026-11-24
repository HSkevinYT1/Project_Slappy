using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cinematica al darle a Jugar (antes de que el jugador tome el control):
// 1. La camara del menu se queda quieta mientras un carro llega por la calle (desde la derecha) y se estaciona.
// 2. Del carro baja el cliente y camina hasta la puerta de la tienda; la camara pasa cerca de el.
// 3. La camara va hacia la ventana derecha de la tienda y la atraviesa; mientras, sube del sotano un doble del
//    cliente con otros colores (el tendero) hasta detras del mostrador.
// 4. La camara se acopla a la vista del jugador y empieza el juego (MenuPrincipal.Empezar).
//    (la camara sigue con la mirada al tendero cuando sale por la puerta de la escalera)
// El cliente "de verdad" lo sigue manejando TutorialTienda: el doble de la puerta desaparece cuando el aparece.
public class IntroTienda : MonoBehaviour
{
    [Header("Carro")]
    public Transform carro;
    public Transform carroInicio;
    public Transform carroFin;
    public float duracionCarro = 3.4f;
    public AudioClip sonidoCarro;
    [Tooltip("Llantas del carro (el pivote en el centro de cada una; giran sobre su eje x local)")]
    public Transform[] llantas;
    public float radioLlanta = 0.33f;

    [Header("Cliente que baja del carro")]
    public Transform bajadaCliente;     // junto a la puerta del carro (lado de la tienda)
    public Transform puertaTienda;      // donde espera (el primer punto del camino del tutorial)

    [Header("Tendero que sube del sotano")]
    public Transform[] caminoSotano;    // desde el sotano, por la escalera, hasta detras del mostrador
    public Color[] coloresTendero = { new Color(0.12f, 0.24f, 0.2f), new Color(0.5f, 0.38f, 0.16f), new Color(0.22f, 0.13f, 0.08f), new Color(0.75f, 0.68f, 0.55f) };

    [Header("Camara")]
    [Tooltip("Puntos por los que pasa la camara despues de la espera (el ultimo debe quedar adentro, tras la ventana)")]
    public Transform[] caminoCamara;
    public float esperaInicial = 3.6f;
    public float duracionCamara = 6.2f;
    public float velocidadPersonajes = 1.35f;
    [Tooltip("Punto dentro de la tienda al que mira la camara mientras va hacia la ventana")]
    public Transform mirarAdentro;

    TutorialTienda tutorial;
    GameObject dobleCliente, dobleTendero;
    // Lo que la intro esconde del jugador mientras dura (Terminar lo devuelve si se corta a la mitad)
    GameObject brazosOcultos;
    readonly List<Renderer> ocultos = new List<Renderer>();

    public bool Reproduciendo { get; private set; }

    // Corta la intro ya (lo usa PanelDebug al saltar el menu): carro estacionado, sin dobles, y el cuerpo y los
    // brazos del jugador visibles otra vez. MenuPrincipal.Empezar corta su parte (la camara)
    public void Terminar()
    {
        StopAllCoroutines();
        Reproduciendo = false;
        if (carro != null && carroFin != null) carro.SetPositionAndRotation(carroFin.position, carroFin.rotation);
        if (dobleCliente != null) Destroy(dobleCliente);
        if (dobleTendero != null) Destroy(dobleTendero);
        foreach (Renderer r in ocultos) if (r != null) r.enabled = true;
        ocultos.Clear();
        if (brazosOcultos != null) brazosOcultos.SetActive(true);
        brazosOcultos = null;
    }

    public IEnumerator Reproducir(Camera cam, CanvasGroup panel, Camera camJugador, Transform cuerpoJugador)
    {
        Reproduciendo = true;
        tutorial = FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        Transform t = cam.transform;
        Vector3 posInicio = t.position;
        Quaternion rotInicio = t.rotation;
        float fovInicio = cam.fieldOfView;

        // Los brazos (objeto Player_Arms) se desactivan y se activan cuando la camara se acopla a la vista del jugador
        GameObject brazos = BuscarBrazos(camJugador, cuerpoJugador);
        if (brazos != null && brazos.activeSelf) { brazos.SetActive(false); brazosOcultos = brazos; }
        // El cuerpo del jugador tampoco se ve durante la escena (el tendero que sube es "el")
        ocultos.Clear();
        if (cuerpoJugador != null)
            foreach (Renderer r in cuerpoJugador.GetComponentsInChildren<Renderer>())
                if (r.enabled && (brazos == null || !r.transform.IsChildOf(brazos.transform)) &&
                    (camJugador == null || !r.transform.IsChildOf(camJugador.transform))) { r.enabled = false; ocultos.Add(r); }

        StartCoroutine(Carro());
        float tiempo = 0f;
        bool clienteSalio = false, tenderoSalio = false;
        Coroutine rutCliente = null;

        // 1. Espera con la camara quieta (se desvanece el menu)
        while (tiempo < esperaInicial)
        {
            tiempo += Time.deltaTime;
            if (panel != null) panel.alpha = 1f - Mathf.Clamp01(tiempo / 0.6f);
            if (!clienteSalio && tiempo > duracionCarro + 0.15f) { clienteSalio = true; rutCliente = StartCoroutine(Cliente()); }
            yield return null;
        }

        // 2-3. Recorrido de la camara
        var puntos = new List<Vector3> { posInicio };
        foreach (Transform p in caminoCamara) if (p != null) puntos.Add(p.position);
        Vector3 finPos = camJugador != null ? camJugador.transform.position : puntos[puntos.Count - 1];
        Quaternion finRot = cuerpoJugador != null ? Quaternion.Euler(0f, cuerpoJugador.eulerAngles.y, 0f) : rotInicio;
        puntos.Add(finPos);
        float fovFin = camJugador != null ? camJugador.fieldOfView : fovInicio;

        for (float k = 0f; k < 1f; k += Time.deltaTime / duracionCamara)
        {
            tiempo += Time.deltaTime;
            if (!clienteSalio && tiempo > duracionCarro + 0.15f) { clienteSalio = true; rutCliente = StartCoroutine(Cliente()); }
            // Sale de la escalera mas o menos cuando la camara llega a la ventana, y llega al mostrador antes del acople
            if (!tenderoSalio) { tenderoSalio = true; StartCoroutine(Tendero(duracionCamara * 0.66f, finRot)); }

            float s = k * k * (3f - 2f * k);
            Vector3 pos = Recorrido(puntos, s);
            t.position = pos;
            // Afuera la camara no gira: mira siempre de frente a la tienda (como en el menu). Solo cuando
            // atraviesa el vidrio de la ventana empieza a mirar adentro, al tendero y al final se alinea con el jugador.
            Quaternion mirar = rotInicio;
            if (mirarAdentro != null) mirar = Quaternion.LookRotation(mirarAdentro.position - pos);
            if (dobleTendero != null && dobleTendero.activeSelf)
            {
                Quaternion alTendero = Quaternion.LookRotation(dobleTendero.transform.position + Vector3.up * 1.2f - pos);
                float pesoTendero = 1f - Mathf.SmoothStep(0f, 1f, (s - 0.82f) / 0.1f);
                mirar = Quaternion.Slerp(mirar, alTendero, pesoTendero * 0.85f);
            }
            float adentro = Mathf.SmoothStep(0f, 1f, Adentro(pos, rotInicio) / 0.8f);
            Quaternion r0 = Quaternion.Slerp(rotInicio, mirar, adentro);
            t.rotation = Quaternion.Slerp(r0, finRot, Mathf.SmoothStep(0f, 1f, (s - 0.78f) / 0.22f) * adentro);
            cam.fieldOfView = Mathf.Lerp(fovInicio, fovFin, s);
            // Se acopla: el tendero desaparece justo antes de que la camara entre en su cabeza
            if (dobleTendero != null && s > 0.94f) dobleTendero.SetActive(false);
            yield return null;
        }
        t.SetPositionAndRotation(finPos, finRot);
        if (dobleTendero != null) Destroy(dobleTendero);
        // Ya acoplada: vuelven el cuerpo y se activan los brazos
        foreach (Renderer r in ocultos) if (r != null) r.enabled = true;
        ocultos.Clear();
        if (brazos != null) brazos.SetActive(true);
        brazosOcultos = null;
        Reproduciendo = false;
    }

    // Cuanto ha pasado la camara el vidrio de la ventana (metros, hacia adentro de la tienda; negativo = afuera).
    // El ultimo punto del camino esta 0.8 m detras del vidrio.
    float Adentro(Vector3 pos, Quaternion rotFrente)
    {
        if (caminoCamara == null || caminoCamara.Length == 0 || caminoCamara[caminoCamara.Length - 1] == null) return 1f;
        Vector3 haciaTienda = Plano(rotFrente * Vector3.forward).normalized;
        Vector3 vidrio = caminoCamara[caminoCamara.Length - 1].position - haciaTienda * 0.8f;
        return Vector3.Dot(pos - vidrio, haciaTienda);
    }

    // El objeto Player_Arms del jugador (primero dentro del jugador; si no, en toda la escena)
    static GameObject BuscarBrazos(Camera camJugador, Transform cuerpoJugador)
    {
        foreach (Transform raiz in new[] { camJugador != null ? camJugador.transform : null, cuerpoJugador })
            if (raiz != null)
                foreach (Transform x in raiz.GetComponentsInChildren<Transform>(true))
                    if (x.name == "Player_Arms") return x.gameObject;
        foreach (Transform x in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (x.name == "Player_Arms" && x.gameObject.scene.IsValid()) return x.gameObject;
        return null;
    }

    void Update()
    {
        // El cliente de la puerta es solo un doble: se quita cuando aparece el cliente del tutorial
        if (dobleCliente != null && tutorial != null && tutorial.cliente != null && tutorial.cliente.gameObject.activeInHierarchy)
            Destroy(dobleCliente);
    }

    // ------------------------------------------------------------------ Carro

    IEnumerator Carro()
    {
        if (carro == null || carroInicio == null || carroFin == null) yield break;
        carro.gameObject.SetActive(true);
        if (sonidoCarro != null)
        {
            var a = carro.gameObject.AddComponent<AudioSource>();
            a.clip = sonidoCarro; a.spatialBlend = 0.6f; a.minDistance = 6f; a.maxDistance = 40f; a.Play();
        }
        Vector3 antes = carroInicio.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / duracionCarro)
        {
            float s = 1f - (1f - k) * (1f - k) * (1f - k);  // frena al llegar
            carro.SetPositionAndRotation(Vector3.Lerp(carroInicio.position, carroFin.position, s), carroFin.rotation);
            GirarLlantas(Vector3.Distance(antes, carro.position));
            antes = carro.position;
            yield return null;
        }
        carro.SetPositionAndRotation(carroFin.position, carroFin.rotation);
        GirarLlantas(Vector3.Distance(antes, carro.position));
    }

    // Las llantas ruedan lo mismo que avanza el carro
    void GirarLlantas(float distancia)
    {
        if (llantas == null || radioLlanta <= 0f) return;
        float grados = distancia / radioLlanta * Mathf.Rad2Deg;
        foreach (Transform l in llantas) if (l != null) l.Rotate(Vector3.right, grados, Space.Self);
    }

    // ------------------------------------------------------------------ Personajes

    GameObject Doble(string nombre, Color[] colores)
    {
        if (tutorial == null || tutorial.cliente == null) return null;
        GameObject d = Instantiate(tutorial.cliente.gameObject);
        d.name = nombre;
        // Sin scripts (no es el cliente de verdad), salvo el de las animaciones: asi camina al moverse
        foreach (MonoBehaviour m in d.GetComponentsInChildren<MonoBehaviour>(true)) if (!(m is AnimacionesModelo)) Destroy(m);
        foreach (Collider c in d.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true)) r.allowOcclusionWhenDynamic = false;
        // Cada pose (quieto, caminando) es su propio modelo con sus materiales: todas con los mismos (si no, se
        // "cambiaba de ropa" al empezar o dejar de caminar)
        foreach (AnimacionesModelo am in d.GetComponentsInChildren<AnimacionesModelo>(true)) am.UnificarMateriales();
        if (colores != null && colores.Length > 0)
        {
            // Un color por MATERIAL (no por malla): la misma prenda queda del mismo color en todas las poses.
            // Copias: no cambia al cliente original
            var copias = new Dictionary<Material, Material>();
            foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int j = 0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (m == null) continue;
                    if (!copias.TryGetValue(m, out Material copia))
                    {
                        copia = new Material(m) { name = m.name + " (" + nombre + ")" };
                        Color c = colores[copias.Count % colores.Length];
                        if (copia.HasProperty("_BaseColor")) copia.SetColor("_BaseColor", c);
                        if (copia.HasProperty("_Color")) copia.SetColor("_Color", c);
                        copias[m] = copia;
                    }
                    mats[j] = copia;
                }
                r.sharedMaterials = mats;
            }
        }
        d.SetActive(true);
        return d;
    }

    static void Animar(GameObject g, bool caminando)
    {
        foreach (Animator a in g.GetComponentsInChildren<Animator>())
        {
            if (a.runtimeAnimatorController == null) continue;
            foreach (AnimatorControllerParameter p in a.parameters)
                if (p.name == "Caminando" && p.type == AnimatorControllerParameterType.Bool) a.SetBool("Caminando", caminando);
        }
    }

    IEnumerator Cliente()
    {
        if (bajadaCliente == null || puertaTienda == null) yield break;
        dobleCliente = Doble("Cliente (intro)", null);
        if (dobleCliente == null) yield break;
        Transform c = dobleCliente.transform;
        // "Se baja": sale de debajo de la acera pegado al carro (el carro lo tapa) y sube hasta pararse
        Vector3 pie = Suelo(bajadaCliente.position);
        Vector3 haciaAfuera = carro != null ? Plano(pie - carro.position).normalized : Vector3.back;
        haciaAfuera = Vector3.Project(haciaAfuera, carroFin != null ? carroFin.right : Vector3.forward).normalized;
        if (haciaAfuera.sqrMagnitude < 0.5f) haciaAfuera = Vector3.back;
        c.rotation = Quaternion.LookRotation(haciaAfuera);
        Vector3 abajo = pie - haciaAfuera * 0.35f + Vector3.down * 1.35f;
        c.position = abajo;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 1.1f)
        {
            float e = 1f - (1f - k) * (1f - k);
            c.position = Vector3.Lerp(abajo, pie, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.08f;
            yield return null;
        }
        c.position = pie;
        yield return new WaitForSeconds(0.15f);
        Animar(dobleCliente, true);
        yield return Caminar(c, puertaTienda.position, velocidadPersonajes);
        Animar(dobleCliente, false);
        // Espera frente a la puerta mirando la tienda
        Vector3 haciaTienda = tutorial != null && tutorial.caminoEntrada != null && tutorial.caminoEntrada.Length > 1
            ? tutorial.caminoEntrada[1].position - c.position : -Vector3.forward;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.5f)
        {
            c.rotation = Quaternion.Slerp(c.rotation, Quaternion.LookRotation(Plano(haciaTienda)), k);
            yield return null;
        }
    }

    IEnumerator Tendero(float duracion, Quaternion rotFinal)
    {
        if (caminoSotano == null || caminoSotano.Length < 2) yield break;
        dobleTendero = Doble("Tendero (intro)", coloresTendero);
        if (dobleTendero == null) yield break;
        Transform tr = dobleTendero.transform;
        tr.position = Suelo(caminoSotano[0].position);
        // Velocidad para llegar justo cuando llega la camara
        float largo = 0f;
        for (int i = 1; i < caminoSotano.Length; i++) largo += Plano(caminoSotano[i].position - caminoSotano[i - 1].position).magnitude;
        float vel = Mathf.Max(0.8f, largo / Mathf.Max(1f, duracion - 0.6f));
        Animar(dobleTendero, true);
        for (int i = 1; i < caminoSotano.Length; i++)
            if (dobleTendero != null) yield return Caminar(tr, caminoSotano[i].position, vel);
        if (dobleTendero == null) yield break;
        Animar(dobleTendero, false);
        for (float k = 0f; k < 1f && dobleTendero != null; k += Time.deltaTime / 0.5f)
        {
            tr.rotation = Quaternion.Slerp(tr.rotation, rotFinal, k);
            yield return null;
        }
    }

    IEnumerator Caminar(Transform npc, Vector3 destino, float vel)
    {
        while (npc != null && Plano(destino - npc.position).sqrMagnitude > 0.003f)
        {
            Vector3 dir = Plano(destino - npc.position);
            npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(dir.normalized), 10f * Time.deltaTime);
            Vector3 p = Vector3.MoveTowards(new Vector3(npc.position.x, 0f, npc.position.z), new Vector3(destino.x, 0f, destino.z), vel * Time.deltaTime);
            // (busca el piso a la altura de los pies: sube escalones, pero no se trepa a lo que esta encima)
            npc.position = Suelo(new Vector3(p.x, npc.position.y + 0.3f, p.z), npc);
            yield return null;
        }
    }

    // Altura del piso bajo un punto (escalera incluida), ignorando al propio personaje
    static Vector3 Suelo(Vector3 p, Transform ignorar = null)
    {
        float mejor = float.MinValue; Vector3 punto = p;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 0.9f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ignorar != null && h.collider.transform.IsChildOf(ignorar)) continue;
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            // el piso mas alto que no este por encima de la cabeza (techos, vigas)
            if (h.point.y > mejor && h.point.y <= p.y + 0.15f) { mejor = h.point.y; punto = h.point; }
        }
        return mejor > float.MinValue ? punto : p;
    }

    static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v; }

    // Catmull-Rom por los puntos (pasa por todos)
    static Vector3 Recorrido(List<Vector3> p, float k)
    {
        if (p.Count == 1) return p[0];
        float f = Mathf.Clamp01(k) * (p.Count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), p.Count - 2);
        float u = f - i;
        Vector3 a = p[Mathf.Max(i - 1, 0)], b = p[i], c = p[i + 1], d = p[Mathf.Min(i + 2, p.Count - 1)];
        return 0.5f * (2f * b + (-a + c) * u + (2f * a - 5f * b + 4f * c - d) * u * u + (-a + 3f * b - 3f * c + d) * u * u * u);
    }
}
