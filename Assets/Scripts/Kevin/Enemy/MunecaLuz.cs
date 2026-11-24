using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Las muñecas de las Cloacas se deshacen con la luz: si la linterna (encendida, en la mano) la alumbra un rato,
// tiembla, se pone al rojo, cruje (se va rajando) y se rompe como porcelana: su malla se parte en trozos con fisica
// (PedazoMuneca). Si deja de alumbrarla se va recuperando poco a poco.
// Si llevaba la llave (LlaveAleatoria), la suelta en el piso.
// Lo agregan AparicionMunecas (a las que aparecen) y CloacasLuzSetup (a la muñeca de practica de la entrada).
public class MunecaLuz : MonoBehaviour
{
    [Tooltip("Segundos de luz seguidos para destruirla")]
    public float segundosParaDestruir = 2.5f;
    [Tooltip("Cuanto mas rapido se recupera que lo que se dana (0.5 = tarda el doble en recuperarse)")]
    public float recuperacion = 0.5f;
    [Tooltip("Metros maximos a los que la linterna le hace dano")]
    public float alcance = 18f;

    public static event System.Action<MunecaLuz> AlDestruirse;
    public bool Destruida { get; private set; }
    public float Dano { get; private set; }

    [Tooltip("Daño (0 a 1) en el que cruje: se va rajando")]
    public float[] crujidos = { 0.3f, 0.55f, 0.78f, 0.92f };

    Renderer[] partes;
    Transform modelo;
    Vector3 modeloPos;
    MaterialPropertyBlock bloque;
    Collider[] propios;
    AudioSource voz;

    void Start()
    {
        // Sin la llave (la que la trae la lleva de hija): no se pone al rojo ni se rompe con ella
        var lista = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r.GetComponentInParent<LlaveAleatoria>() == null) lista.Add(r);
        partes = lista.ToArray();
        voz = gameObject.AddComponent<AudioSource>();
        voz.playOnAwake = false;
        voz.spatialBlend = 1f;
        voz.rolloffMode = AudioRolloffMode.Linear;
        voz.minDistance = 1.5f;
        voz.maxDistance = 22f;
        voz.dopplerLevel = 0f;
        propios = GetComponentsInChildren<Collider>();
        bloque = new MaterialPropertyBlock();
        // Lo que tiembla: el primer hijo con mallas (o la muñeca entera si no tiene)
        foreach (Transform t in transform)
            if (t.GetComponentInChildren<Renderer>() != null && t.GetComponent<LlaveAleatoria>() == null) { modelo = t; break; }
        if (modelo != null) modeloPos = modelo.localPosition;
    }

    void Update()
    {
        if (Destruida) return;
        bool alumbrada = Alumbrada();
        float antes = Dano;
        Dano = alumbrada ? Dano + Time.deltaTime / Mathf.Max(0.1f, segundosParaDestruir)
                         : Mathf.Max(0f, Dano - Time.deltaTime * recuperacion / Mathf.Max(0.1f, segundosParaDestruir));
        if (Dano > 0f || antes > 0f) Aspecto(Dano);
        // Cruje al pasar cada umbral (solo subiendo)
        if (crujidos != null && voz != null)
            foreach (float u in crujidos)
                if (antes < u && Dano >= u) { voz.pitch = Random.Range(0.9f, 1.15f); voz.PlayOneShot(PedazoMuneca.Crujido(), 0.5f + 0.5f * u); Jugo.SacudirEn(Centro(), 0.05f * u, 8f); }
        if (Dano >= 1f) StartCoroutine(Destruirse());
    }

    // Romperla sin luz (AgarreMuneca: el jugador se la quito de encima)
    public void Romper()
    {
        if (!Destruida) StartCoroutine(Destruirse());
    }

    bool Alumbrada()
    {
        // La que tiene encima no se deshace con la linterna: hay que quitarsela forcejeando
        if (AgarreMuneca.Montada != null && AgarreMuneca.Montada.gameObject == gameObject) return false;
        Flashlight f = Flashlight.EnMano;
        if (f == null || !f.Encendida || PantallaMuerte.Muerto) return false;
        Light haz = f.Haz;
        if (haz == null) return false;
        Vector3 c = Centro();
        Vector3 d = c - haz.transform.position;
        float dist = d.magnitude;
        if (dist > Mathf.Min(alcance, haz.range) || dist < 0.01f) return false;
        float medio = (haz.type == LightType.Spot ? haz.spotAngle : 60f) * 0.5f;
        if (Vector3.Angle(haz.transform.forward, d) > medio * 0.85f) return false;
        // Que no haya una pared en medio (lo de la muñeca y el jugador no cuenta)
        Transform jugador = HUD.Jugador();
        foreach (RaycastHit h in Physics.RaycastAll(haz.transform.position, d / dist, dist - 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (jugador != null && h.collider.transform.IsChildOf(jugador.root)) continue;
            return false;
        }
        return true;
    }

    Vector3 Centro()
    {
        if (partes == null || partes.Length == 0) return transform.position;
        Bounds b = partes[0].bounds;
        foreach (Renderer r in partes) if (r != null && r.enabled) b.Encapsulate(r.bounds);
        return b.center;
    }

    // Tiembla cada vez mas y se pone al rojo
    void Aspecto(float k)
    {
        if (modelo != null)
            modelo.localPosition = modeloPos + (k > 0f ? Random.insideUnitSphere * 0.03f * k * k : Vector3.zero);
        // Por material: el modelo tiene varios (cuerpo, ropa, ojos...) y cada uno parte de su propio color
        foreach (Renderer r in partes)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (k <= 0f) { r.SetPropertyBlock(null, i); continue; }
                Material m = mats[i];
                Color baseC = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                bloque.Clear();
                bloque.SetColor("_BaseColor", Color.Lerp(baseC, new Color(0.75f, 0.12f, 0.05f), k * 0.75f));
                bloque.SetColor("_EmissionColor", new Color(2.2f, 0.5f, 0.15f) * (k * k));
                r.SetPropertyBlock(bloque, i);
            }
        }
    }

    IEnumerator Destruirse()
    {
        Destruida = true;
        if (TryGetComponent(out EstatuaAI ai)) ai.enabled = false;
        if (TryGetComponent(out NavMeshAgent ag) && ag.enabled && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
        foreach (Collider c in propios) if (c != null) c.enabled = false;

        // La llave cae al piso
        LlaveAleatoria llave = GetComponentInChildren<LlaveAleatoria>(true);
        if (llave != null) llave.Soltar();

        // Un ultimo temblor fuerte
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            if (modelo != null) modelo.localPosition = modeloPos + Random.insideUnitSphere * 0.06f;
            yield return null;
        }
        if (modelo != null) modelo.localPosition = modeloPos;
        Vector3 c0 = Centro();
        Jugo.SacudirEn(c0, 0.3f, 12f);
        // Se rompe como porcelana: su propia malla en trozos (salen desde un poco mas abajo del centro, hacia afuera)
        PedazoMuneca.Romper(partes, c0 - Vector3.up * 0.15f);
        AlDestruirse?.Invoke(this);
        Destroy(gameObject);
    }
}
