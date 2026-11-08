using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Escalera de la tienda al sotano ("escaleras") convertida en escalera de MADERA:
// - La rampa original se oculta pero conserva su colision (se sigue caminando igual de suave).
// - Encima: escalones (huella + contrahuella) de tabla, dos zancas laterales y un pasamanos en la pared.
// - El techo inclinado del pasillo usa los tablones de madera del sotano.
// La madera de los escalones es una textura generada con veta y mapa de normales.
//
// Se aplica sola una vez. Rehacer: Tools > Sotano > Escalera de madera.
[InitializeOnLoad]
public static class EscalerasSetup
{
    const string Version = "_escalera v2";
    const float AlturaEscalon = 0.19f;   // altura ideal de cada escalon
    const float Vuelo = 0.03f;           // cuanto sobresale la huella sobre la contrahuella

    static EscalerasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject e = GameObject.Find("escaleras");
        if (!e) return;
        Transform hecho = e.transform.Find("Escalera de madera");
        if (hecho && hecho.Find(Version)) return;
        Crear();
    }

    [MenuItem("Tools/Sotano/Escalera de madera")]
    static void Menu()
    {
        Crear();
        GameObject e = GameObject.Find("escaleras");
        if (e) Selection.activeTransform = e.transform.Find("Escalera de madera");
    }

    static void Crear()
    {
        GameObject eGO = GameObject.Find("escaleras");
        if (!eGO) return;
        Transform esc = eGO.transform;
        Transform rampaT = esc.Find("escalera"), techoT = esc.Find("escalera (1)");
        Transform paredA = esc.Find("Cube"), paredB = esc.Find("Cube (1)");
        if (!rampaT) { Debug.LogWarning("[Escalera] No se encontro la rampa \"escaleras/escalera\"."); return; }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Escalera de madera");

        Transform vieja = esc.Find("Escalera de madera");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        var raiz = new GameObject("Escalera de madera");
        Undo.RegisterCreatedObjectUndo(raiz, "Escalera de madera");
        new GameObject(Version).transform.SetParent(raiz.transform, false);

        // ---- Geometria de la rampa ----
        Vector3 subida = rampaT.right; if (subida.y < 0f) subida = -subida;    // a lo largo de la rampa, hacia arriba
        Vector3 normal = rampaT.up; if (normal.y < 0f) normal = -normal;
        Vector3 ancho = rampaT.forward;
        Vector3 s = rampaT.lossyScale;
        Vector3 sup = rampaT.position + normal * (s.y * 0.5f);                  // centro de la cara de arriba
        Vector3 inicio = sup - subida * (s.x * 0.5f);                           // punto mas bajo de la rampa
        Vector3 h = new Vector3(subida.x, 0f, subida.z).normalized;              // direccion horizontal de subida
        float pendiente = subida.y / new Vector2(subida.x, subida.z).magnitude;  // tan(angulo)

        // Pisos de abajo (sotano) y de arriba (tienda).
        float yAbajo = Piso("zotano/zona/piso", inicio.y);
        float yArriba = Piso("Tienda/rt (6)", (sup + subida * (s.x * 0.5f)).y);
        float s0 = (yAbajo - inicio.y) / pendiente, s1 = (yArriba - inicio.y) / pendiente; // distancia horizontal
        int n = Mathf.Max(3, Mathf.RoundToInt((yArriba - yAbajo) / AlturaEscalon));
        float alto = (yArriba - yAbajo) / n, huella = (s1 - s0) / n;

        // Ancho util: entre las dos paredes laterales.
        float centroW = Vector3.Dot(sup, ancho), anchoUtil = s.z;
        if (paredA && paredB && paredA.TryGetComponent(out Renderer ra) && paredB.TryGetComponent(out Renderer rb))
        {
            float a0 = Proy(ra.bounds, ancho, true), a1 = Proy(ra.bounds, ancho, false);
            float b0 = Proy(rb.bounds, ancho, true), b1 = Proy(rb.bounds, ancho, false);
            float desde = a1 < b0 ? a1 : b1, hasta = a1 < b0 ? b0 : a0; // cara interior de cada pared
            centroW = (desde + hasta) * 0.5f;
            anchoUtil = Mathf.Abs(hasta - desde);
        }
        float anchoEsc = anchoUtil - 0.02f;
        Vector3 Punto(float sh, float y) // punto en el eje de la escalera
        {
            Vector3 p = inicio + h * sh;
            p += ancho * (centroW - Vector3.Dot(p, ancho));
            p.y = y;
            return p;
        }

        // ---- Materiales ----
        var madera = SotanoAcabados.Generar("MaderaEscalera", Veta, 0.8f, 1.0f, 0.22f);
        var maderaOscura = SotanoAcabados.Generar("MaderaEscaleraOscura", VetaOscura, 0.8f, 1.0f, 0.15f);
        var tablones = SotanoAcabados.Generar("Techo", SotanoAcabados.Tablones, 1.2f, 1.6f, 0.1f);

        Quaternion rotH = Quaternion.LookRotation(h, Vector3.up); // x local = a lo ancho
        Transform escalones = Hijo(raiz.transform, "Escalones");
        for (int i = 0; i < n; i++)
        {
            float yTop = yAbajo + (i + 1) * alto;
            float sFrente = s0 + i * huella;
            // Huella (tabla de pisar), con un poco de vuelo hacia adelante.
            Caja(escalones, "Huella " + (i + 1), Punto(sFrente + huella * 0.5f - Vuelo * 0.5f, yTop - 0.0225f), rotH,
                 new Vector3(anchoEsc, 0.045f, huella + Vuelo), SotanoAcabados.Material("Escalon", madera, anchoEsc, 0.3f));
            // Contrahuella (tabla vertical del frente).
            Caja(escalones, "Contrahuella " + (i + 1), Punto(sFrente + 0.0125f, yTop - 0.045f - (alto - 0.045f) * 0.5f), rotH,
                 new Vector3(anchoEsc - 0.01f, alto - 0.045f, 0.025f), SotanoAcabados.Material("Contrahuella", maderaOscura, anchoEsc, 0.3f));
        }

        // ---- Zancas (vigas laterales que siguen la pendiente) ----
        Vector3 dirPend = (h + Vector3.up * pendiente).normalized;
        Vector3 nPend = (Vector3.up - dirPend * Vector3.Dot(Vector3.up, dirPend)).normalized;
        Quaternion rotPend = Quaternion.LookRotation(Vector3.Cross(dirPend, nPend), nPend); // x local = a lo largo
        float largoPend = (s1 - s0 + 0.15f) / Mathf.Cos(Mathf.Atan(pendiente));
        float sMedio = (s0 + s1) * 0.5f - 0.05f;
        float yLineaNarices = yAbajo + alto + (sMedio - s0) * pendiente; // linea por el borde de las huellas
        Transform zancas = Hijo(raiz.transform, "Zancas");
        foreach (float lado in new[] { -1f, 1f })
        {
            Vector3 p = Punto(sMedio, yLineaNarices - 0.06f) + ancho * lado * (anchoEsc * 0.5f - 0.02f);
            Caja(zancas, "Zanca", p, rotPend, new Vector3(largoPend, 0.3f, 0.05f),
                 SotanoAcabados.Material("Zanca", maderaOscura, largoPend, 0.3f));
        }

        // ---- Pasamanos en la pared (lado +ancho), con soportes ----
        Transform pas = Hijo(raiz.transform, "Pasamanos");
        float altoPas = 0.9f, separacion = 0.07f;
        Vector3 lateral = ancho * (anchoUtil * 0.5f - separacion);
        float largoPas = (s1 - s0 - 0.3f) / Mathf.Cos(Mathf.Atan(pendiente));
        Caja(pas, "Baranda", Punto(sMedio, yLineaNarices + altoPas) + lateral, rotPend, new Vector3(largoPas, 0.05f, 0.06f),
             SotanoAcabados.Material("Baranda", madera, largoPas, 0.1f));
        int soportes = Mathf.Max(2, Mathf.RoundToInt((s1 - s0) / 1.3f));
        for (int i = 0; i <= soportes; i++)
        {
            float sh = Mathf.Lerp(s0 + 0.25f, s1 - 0.25f, i / (float)soportes);
            float y = yAbajo + alto + (sh - s0) * pendiente + altoPas - 0.05f;
            Caja(pas, "Soporte", Punto(sh, y) + ancho * (anchoUtil * 0.5f - separacion * 0.5f), rotH,
                 new Vector3(separacion, 0.03f, 0.03f), SotanoAcabados.Material("Soporte", maderaOscura, 0.1f, 0.1f));
        }

        // ---- Ocultar la rampa (queda su colision) y techo de tablones ----
        if (rampaT.TryGetComponent(out Renderer rRampa)) { Undo.RecordObject(rRampa, "Ocultar rampa"); rRampa.enabled = false; }
        if (techoT && techoT.TryGetComponent(out Renderer rTecho))
        {
            Vector3 st = techoT.lossyScale;
            Undo.RecordObject(rTecho, "Techo de tablones");
            rTecho.sharedMaterial = SotanoAcabados.Material("TechoEscalera", tablones, st.x, st.z);
        }

        // ---- Paredes del pasillo: revestimiento de tablas verticales de madera ----
        var panel = SotanoAcabados.Generar("PanelMadera", Panel, 1.0f, 1.2f, 0.18f);
        foreach (Transform pared in new[] { paredA, paredB })
        {
            if (!pared || !pared.TryGetComponent(out Renderer rp)) continue;
            Vector3 sp = pared.lossyScale;
            Undo.RecordObject(rp, "Paredes de madera");
            // Cara grande del cubo: U a lo largo (x), V hacia arriba (y).
            rp.sharedMaterial = SotanoAcabados.Material("PanelPasillo", panel, Mathf.Abs(sp.x), Mathf.Abs(sp.y));
        }

        raiz.transform.SetParent(esc, true);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Escalera] Escalera de madera: {n} escalones de {alto * 100f:0} cm x {huella * 100f:0} cm, ancho {anchoEsc:0.00} m. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------

    static float Piso(string ruta, float porDefecto)
    {
        string[] partes = ruta.Split(new[] { '/' }, 2);
        GameObject raiz = GameObject.Find(partes[0]);
        Transform t = raiz && partes.Length > 1 ? raiz.transform.Find(partes[1]) : null;
        return t && t.TryGetComponent(out Renderer r) ? r.bounds.max.y : porDefecto;
    }

    static float Proy(Bounds b, Vector3 eje, bool minimo)
    {
        float c = Vector3.Dot(b.center, eje);
        float e = Mathf.Abs(b.extents.x * eje.x) + Mathf.Abs(b.extents.y * eje.y) + Mathf.Abs(b.extents.z * eje.z);
        return minimo ? c - e : c + e;
    }

    static void Caja(Transform padre, string nombre, Vector3 centro, Quaternion rot, Vector3 tam, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // se camina sobre la rampa original
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(centro, rot);
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    // ------------------------------------------------------------------ Textura de madera (veta a lo largo de U)

    static float Ruido(float u, float v, float esc, float sem)
    {
        // Repetible sin costuras.
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static void Veta(float u, float v, out Color c, out float h) => VetaBase(u, v, new Color(0.46f, 0.3f, 0.17f), out c, out h);
    static void VetaOscura(float u, float v, out Color c, out float h) => VetaBase(u, v, new Color(0.3f, 0.19f, 0.11f), out c, out h);

    // Tablas verticales (6 por repeticion) con la veta hacia arriba y una ranura entre tablas.
    static void Panel(float u, float v, out Color c, out float h)
    {
        const int tablas = 6;
        float fx = u * tablas;
        int tabla = Mathf.FloorToInt(fx);
        float tx = Mathf.Repeat(fx, 1f);
        float borde = Mathf.Min(tx, 1f - tx);
        float ranura = Mathf.Clamp01((borde - 0.015f) / 0.04f);
        ranura = ranura * ranura * (3f - 2f * ranura);
        float tono = 0.88f + Mathf.Repeat(Mathf.Sin(tabla * 91.7f) * 4375.3f, 1f) * 0.2f;
        // Veta corriendo a lo largo de V (vertical): se usa la misma funcion con U y V cambiados.
        VetaBase(Mathf.Repeat(v + tabla * 0.37f, 1f), u, new Color(0.4f, 0.26f, 0.15f) * tono, out Color cv, out float hv);
        c = Color.Lerp(cv * 0.35f, cv, ranura);
        h = hv * ranura;
    }

    static void VetaBase(float u, float v, Color baseC, out Color c, out float h)
    {
        float deform = Ruido(u, v, 3f, 2f) * 3f + Ruido(u, v, 9f, 5f) * 0.6f;
        float anillos = Mathf.Sin((v * 22f + deform) * Mathf.PI);
        float veta = anillos * anillos;                               // lineas a lo largo de U
        float fibra = Ruido(u, v, 60f, 8f) * 0.5f + Ruido(u, v, 150f, 3f) * 0.5f;
        float nudo = Ruido(u, v, 4f, 13f);
        float desgaste = Ruido(u, v, 2f, 21f);                         // zonas mas gastadas / claras

        c = baseC * (0.82f + veta * 0.22f + fibra * 0.08f);
        if (nudo > 0.78f) c *= Mathf.Lerp(1f, 0.6f, (nudo - 0.78f) / 0.22f);
        if (desgaste > 0.62f) c = Color.Lerp(c, c * 1.18f, (desgaste - 0.62f) / 0.38f);
        h = 0.55f + veta * 0.25f + fibra * 0.1f - (nudo > 0.78f ? 0.15f : 0f);
    }
}
