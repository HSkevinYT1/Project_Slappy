using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sotano ("zotano"):
// - Bloques CAFES -> cajas de carton (textura generada: carton, cinta de embalar, etiqueta, manchas).
// - Bloques AZULES -> cajas ABIERTAS: paredes de carton, solapas abiertas, papel protector arrugado
//   adentro y un objeto encima ("Objeto en la caja", se puede cambiar por otro).
// - Props de sotano pegados a las paredes (estanterias metalicas, pilas de cajas, mesa vieja, maletas,
//   calentador con tuberias, focos colgando), dejando libre el paso desde la escalera hasta la cabina.
//
// Se aplica sola una vez. Rehacer: Tools > Sotano > Decorar.
[InitializeOnLoad]
public static class SotanoSetup
{
    const string Version = "_sotano v6";
    const string Carpeta = "Assets/Sotano";
    const string CarpetaTex = Carpeta + "/Texturas";
    const string CarpetaMat = Carpeta + "/Materiales";
    const string CarpetaMallas = Carpeta + "/Mallas";
    const string Props = "Assets/Models/Props/";

    static System.Random rnd;
    static Material matCarton, matInterior, matPapel, matMetal, matTuberia, matFoco, matCable;
    static readonly List<Bounds> libres = new List<Bounds>();
    static int puestos;

    static SotanoSetup()
    {
        // Un ciclo mas tarde que las otras herramientas (la tienda usa el catalogo de props).
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject z = GameObject.Find("zotano");
        if (!z) return;
        Transform d = z.transform.Find("Decoracion Sotano");
        if (d && d.Find(Version)) return;
        Crear();
    }

    [MenuItem("Tools/Sotano/Decorar")]
    static void Menu()
    {
        Crear();
        GameObject z = GameObject.Find("zotano");
        if (z) Selection.activeTransform = z.transform.Find("Decoracion Sotano");
    }

    // ------------------------------------------------------------------

    static void Crear()
    {
        GameObject zGO = GameObject.Find("zotano");
        if (!zGO) return;
        Transform zotano = zGO.transform;

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Decorar sotano");

        Transform vieja = zotano.Find("Decoracion Sotano");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        rnd = new System.Random(77);
        puestos = 0;
        CrearMateriales();

        var raiz = new GameObject("Decoracion Sotano");
        Undo.RegisterCreatedObjectUndo(raiz, "Decoracion sotano");
        new GameObject(Version).transform.SetParent(raiz.transform, false);

        // ---- Cajas existentes ----
        int cafes = 0, azules = 0;
        Transform cajasAbiertas = Hijo(raiz.transform, "Cajas abiertas");
        foreach (Renderer r in zotano.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLowerInvariant().StartsWith("caja")) continue;
            string mat = r.sharedMaterial ? r.sharedMaterial.name : "";
            if (mat.StartsWith("Material1")) { CajaAbierta(r, cajasAbiertas); azules++; }
            else if (mat.ToLowerInvariant().Contains("caja") || mat == "Carton")
            {
                Undo.RecordObject(r, "Textura de carton");
                r.sharedMaterial = matCarton;
                cafes++;
            }
        }

        // ---- Props del sotano ----
        DecorarCuarto(zotano, raiz.transform);

        // ---- Techo y acabados con mapa de normales ----
        string acabados = SotanoAcabados.Aplicar(zotano, raiz.transform);
        Debug.Log("[Sotano] Acabados: " + acabados);

        raiz.transform.SetParent(zotano, true);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Sotano] {cafes} cajas con textura de carton, {azules} cajas abiertas con papel y objeto, {puestos} props. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ Caja abierta

    static void CajaAbierta(Renderer caja, Transform padre)
    {
        Bounds b = caja.bounds;
        // El bloque azul deja de verse pero conserva su colision.
        Undo.RecordObject(caja, "Caja abierta");
        caja.enabled = false;

        var grupo = new GameObject("Caja abierta (" + caja.name + ")").transform;
        grupo.SetParent(padre, false);
        float t = 0.025f;
        float W = b.size.x, H = b.size.y, D = b.size.z;
        Vector3 c = b.center;

        Pieza(grupo, "Fondo", new Vector3(c.x, b.min.y + t * 0.5f, c.z), new Vector3(W, t, D), matInterior);
        Pieza(grupo, "Pared +X", new Vector3(b.max.x - t * 0.5f, c.y, c.z), new Vector3(t, H, D), matCarton);
        Pieza(grupo, "Pared -X", new Vector3(b.min.x + t * 0.5f, c.y, c.z), new Vector3(t, H, D), matCarton);
        Pieza(grupo, "Pared +Z", new Vector3(c.x, c.y, b.max.z - t * 0.5f), new Vector3(W - 2 * t, H, t), matCarton);
        Pieza(grupo, "Pared -Z", new Vector3(c.x, c.y, b.min.z + t * 0.5f), new Vector3(W - 2 * t, H, t), matCarton);
        // Caras internas mas oscuras (carton por dentro).
        Pieza(grupo, "Interior +X", new Vector3(b.max.x - t - 0.002f, c.y, c.z), new Vector3(0.004f, H - t, D - 2 * t), matInterior);
        Pieza(grupo, "Interior -X", new Vector3(b.min.x + t + 0.002f, c.y, c.z), new Vector3(0.004f, H - t, D - 2 * t), matInterior);
        Pieza(grupo, "Interior +Z", new Vector3(c.x, c.y, b.max.z - t - 0.002f), new Vector3(W - 2 * t, H - t, 0.004f), matInterior);
        Pieza(grupo, "Interior -Z", new Vector3(c.x, c.y, b.min.z + t + 0.002f), new Vector3(W - 2 * t, H - t, 0.004f), matInterior);

        // Solapas abiertas: giran sobre el borde de arriba hacia afuera y caen un poco.
        Solapa(grupo, new Vector3(b.max.x, b.max.y, c.z), Vector3.forward, Vector3.left, D, W * 0.5f, -1f);
        Solapa(grupo, new Vector3(b.min.x, b.max.y, c.z), Vector3.forward, Vector3.right, D, W * 0.5f, 1f);
        Solapa(grupo, new Vector3(c.x, b.max.y, b.max.z), Vector3.right, Vector3.back, W, D * 0.5f, 1f);
        Solapa(grupo, new Vector3(c.x, b.max.y, b.min.z), Vector3.right, Vector3.forward, W, D * 0.5f, -1f);

        // Papel protector arrugado adentro (con algunas puntas que sobresalen).
        float alto = Mathf.Min(0.2f, H * 0.3f);
        var papel = new GameObject("Papel protector");
        papel.transform.SetParent(grupo, false);
        papel.transform.position = new Vector3(c.x, b.max.y - alto * 0.75f, c.z);
        papel.transform.localScale = new Vector3(W - 2 * t - 0.01f, alto, D - 2 * t - 0.01f);
        papel.AddComponent<MeshFilter>().sharedMesh = MallaPapel(rnd.Next(3));
        papel.AddComponent<MeshRenderer>().sharedMaterial = matPapel;

        // Objeto encima del papel (como recien sacado de la caja), apoyado en lo mas alto del centro del papel.
        string[] opciones = { "farol", "radio", "reloj", "lampara" };
        string clave = opciones[rnd.Next(opciones.Length)];
        float yPapel = AlturaCentroPapel(papel);
        GameObject obj = TiendaPropsSetup.CrearProp(clave, grupo, new Vector3(c.x, yPapel, c.z), Mathf.Min(W, D) * 0.6f, R(0f, 360f), false, rnd);
        if (obj) obj.name = "Objeto en la caja (" + clave + ")";
    }

    // Altura (mundo) de la parte mas alta del papel en el centro de la caja.
    static float AlturaCentroPapel(GameObject papel)
    {
        Mesh m = papel.GetComponent<MeshFilter>().sharedMesh;
        float max = float.MinValue;
        foreach (Vector3 v in m.vertices)
            if (Mathf.Abs(v.x) < 0.22f && Mathf.Abs(v.z) < 0.22f)
                max = Mathf.Max(max, papel.transform.TransformPoint(v).y);
        return max > float.MinValue ? max - 0.01f : papel.transform.position.y;
    }

    // Solapa de carton con bisagra en el borde superior. Cerrada apuntaria "haciaAdentro";
    // se gira sobre el borde: primero hacia arriba, luego hacia afuera, y cae un poco (abierta).
    static void Solapa(Transform padre, Vector3 bisagra, Vector3 ejeBorde, Vector3 haciaAdentro, float largo, float ancho, float _)
    {
        var piv = new GameObject("Solapa").transform;
        piv.SetParent(padre, false);
        piv.position = bisagra;

        var hoja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hoja.name = "Hoja";
        Object.DestroyImmediate(hoja.GetComponent<Collider>());
        hoja.transform.SetParent(piv, false);
        hoja.transform.localPosition = haciaAdentro * (ancho * 0.5f);
        // Tamano en ejes del mundo: largo a lo largo del borde, ancho hacia adentro, 1.2 cm de grosor.
        Vector3 tam = Abs(ejeBorde) * largo + Abs(haciaAdentro) * ancho + Vector3.up * 0.012f;
        hoja.transform.localScale = tam;
        hoja.GetComponent<Renderer>().sharedMaterial = matCarton;

        // AngleAxis(+90, eje) lleva "haciaAdentro" a Cross(eje, haciaAdentro): se elige el signo que sube.
        float signo = Vector3.Dot(Vector3.Cross(ejeBorde, haciaAdentro), Vector3.up) > 0f ? 1f : -1f;
        piv.rotation = Quaternion.AngleAxis(signo * (180f + R(15f, 40f)), ejeBorde);
    }

    // Malla de papel arrugado normalizada (x,z de -0.5 a 0.5; y de 0 a ~1).
    static Mesh MallaPapel(int variante)
    {
        string ruta = $"{CarpetaMallas}/PapelArrugado_{variante}.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente) return existente;

        var azar = new System.Random(100 + variante);
        int n = 18;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float o1 = (float)azar.NextDouble() * 50f, o2 = (float)azar.NextDouble() * 50f;
        for (int iz = 0; iz <= n; iz++)
        for (int ix = 0; ix <= n; ix++)
        {
            float x = ix / (float)n - 0.5f, z = iz / (float)n - 0.5f;
            float borde = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) * 2f;           // 0 centro, 1 borde
            float arrugas = Mathf.PerlinNoise(o1 + x * 9f, o2 + z * 9f) * 0.45f
                          + Mathf.PerlinNoise(o2 + x * 23f, o1 + z * 23f) * 0.25f
                          + (float)azar.NextDouble() * 0.12f;
            float y = 0.35f + arrugas * 0.6f + Mathf.SmoothStep(0.55f, 1f, borde) * 0.45f; // sube contra las paredes
            v.Add(new Vector3(x, y, z));
            uv.Add(new Vector2(x + 0.5f, z + 0.5f) * 2f);
        }
        for (int iz = 0; iz < n; iz++)
        for (int ix = 0; ix < n; ix++)
        {
            int a = iz * (n + 1) + ix, b = a + 1, c = a + n + 1, d = c + 1;
            t.AddRange(new[] { a, c, b, b, c, d });
        }
        var m = new Mesh { name = "PapelArrugado_" + variante };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        Directory.CreateDirectory(CarpetaMallas);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Props del cuarto

    static void DecorarCuarto(Transform zotano, Transform raiz)
    {
        Renderer oeste = Rend(zotano, "zona/pared 2 (3)"), este = Rend(zotano, "zona/pared 2 (1)"), este2 = Rend(zotano, "zona/pared 2 (2)");
        Renderer sur = Rend(zotano, "zona/pared 2"), norte = Rend(zotano, "zona/pared1"), piso = Rend(zotano, "zona/piso");
        if (!oeste || !este || !sur || !norte || !piso) { Debug.LogWarning("[Sotano] No se encontraron las paredes del sotano."); return; }

        float xO = oeste.bounds.max.x, xE = este.bounds.min.x, zS = sur.bounds.max.z, zN = norte.bounds.min.z;
        float y0 = piso.bounds.max.y, techo = norte.bounds.max.y;
        float puertaZ0 = este.bounds.max.z, puertaZ1 = este2 ? este2.bounds.min.z : puertaZ0 + 1.4f;

        // Lugares libres: el paso desde la escalera hacia el oeste, la cabina con el muneco y las pilas existentes.
        libres.Clear();
        libres.Add(Caja2D(xO + 2.2f, xE + 0.5f, puertaZ0 - 0.75f, puertaZ1 - 0.6f));
        Transform props = zotano.Find("props");
        Transform cabina = props ? props.Find("Cabina_Telefonica") : null;
        if (cabina && Limites(cabina.gameObject, out Bounds bc))
        {
            bc.Expand(new Vector3(0.5f, 0f, 0.5f));
            bc.max = new Vector3(bc.max.x + 2.0f, bc.max.y, bc.max.z + 1.6f); // frente de la cabina y el muneco
            libres.Add(bc);
        }
        if (props)
            foreach (Renderer r in props.GetComponentsInChildren<Renderer>())
                if (r.name.ToLowerInvariant().StartsWith("caja")) { Bounds b = r.bounds; b.Expand(0.5f); libres.Add(b); }

        Transform estantes = Hijo(raiz, "Estanterias");
        Transform cajas = Hijo(raiz, "Cajas");
        Transform muebles = Hijo(raiz, "Muebles y objetos");
        Transform techoT = Hijo(raiz, "Tuberias y focos");

        // Estanterias metalicas contra la pared norte y la sur.
        foreach (float f in new[] { 0.22f, 0.40f, 0.58f })
            Estanteria(estantes, new Vector3(Mathf.Lerp(xO, xE, f), y0, zN - 0.27f), Vector3.right, Vector3.back);
        Estanteria(estantes, new Vector3(Mathf.Lerp(xO, xE, 0.6f), y0, zS + 0.27f), Vector3.left, Vector3.forward);
        // Una contra la pared oeste, entre la cabina y la pila del rincon.
        Estanteria(estantes, new Vector3(xO + 0.27f, y0, Mathf.Lerp(zS, zN, 0.48f)), Vector3.forward, Vector3.right);

        // Pilas de cajas en rincones y a lo largo de las paredes.
        Pila(cajas, new Vector3(xE - 0.45f, y0, zS + 0.45f), 3);
        Pila(cajas, new Vector3(xE - 0.4f, y0, Mathf.Lerp(zS, puertaZ0, 0.55f)), 2);
        Pila(cajas, new Vector3(Mathf.Lerp(xO, xE, 0.76f), y0, zS + 0.4f), 2);
        Pila(cajas, new Vector3(Mathf.Lerp(xO, xE, 0.75f), y0, zN - 0.4f), 3);
        Pila(cajas, new Vector3(xO + 0.4f, y0, Mathf.Lerp(zS, zN, 0.66f)), 2);

        // Mesa vieja con cosas encima, contra la pared este (al norte de la puerta), a lo largo de la pared.
        GameObject mesa = TiendaPropsSetup.CrearProp("mesa", muebles, new Vector3(xE - 0.6f, y0, puertaZ1 + 1.3f), 2.0f, R(-3f, 3f), false, rnd);
        if (mesa)
        {
            // Madera oscura y vieja para el sotano.
            var madera = AssetDatabase.LoadAssetAtPath<Material>(Props + "marron oscuro.mat");
            if (madera)
                foreach (Renderer r in mesa.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = madera;
                    r.sharedMaterials = mats;
                }
            Validar(mesa, true, muebles);
        }
        if (mesa && TiendaPropsSetup.LimitesDe(mesa, out Bounds bm))
        {
            float yMesa = bm.max.y;
            string[] sobre = { "farol", "radio", "libros", "taza", "reloj" };
            for (int i = 0; i < 4; i++)
            {
                float zz = Mathf.Lerp(bm.min.z + 0.25f, bm.max.z - 0.25f, (i + 0.5f) / 4f);
                var o = TiendaPropsSetup.CrearProp(sobre[rnd.Next(sobre.Length)], muebles, new Vector3(bm.center.x + R(-0.15f, 0.15f), yMesa, zz), 0.32f, R(0f, 360f), false, rnd);
                if (o) puestos++;
            }
        }

        // Maletas apiladas junto a la pared oeste.
        float zMal = Mathf.Lerp(zS, zN, 0.3f);
        GameObject m1 = TiendaPropsSetup.CrearProp("maletin", muebles, new Vector3(xO + 0.45f, y0, zMal), 0.8f, 0f, true, rnd);
        if (m1 && Validar(m1, true, muebles) && TiendaPropsSetup.LimitesDe(m1, out Bounds b1))
        {
            var m2 = TiendaPropsSetup.CrearProp("maletin", muebles, new Vector3(b1.center.x, b1.max.y, b1.center.z), 0.68f, 6f, true, rnd);
            if (m2) puestos++;
        }

        // Calentador de agua en el rincon noreste, con tuberias por el techo.
        Vector3 cal = new Vector3(xE - 0.55f, y0, zN - 0.55f);
        var huellaCal = new Bounds(cal + Vector3.up, new Vector3(0.9f, 1.6f, 0.9f));
        if (!EnLibre(huellaCal) && !Choca(huellaCal, null))
        {
            Cilindro(muebles, "Calentador", cal + Vector3.up * 0.85f, 0.42f, 1.7f, matTuberia, true);
            Cilindro(muebles, "Calentador tapa", cal + Vector3.up * 1.72f, 0.44f, 0.05f, matMetal, false);
            Cilindro(muebles, "Tubo del calentador", cal + Vector3.up * ((1.75f + techo - y0) * 0.5f), 0.05f, techo - y0 - 1.75f, matTuberia, false);
            puestos++;
        }
        // Por debajo de las vigas del techo (que bajan 0.22 m).
        float yTubo1 = techo - 0.34f, yTubo2 = techo - 0.48f;
        TuboX(techoT, xO, xE, yTubo1, zN - 0.12f, 0.06f);
        TuboX(techoT, xO, xE, yTubo2, zN - 0.25f, 0.035f);
        TuboX(techoT, xO, xE, yTubo1, zS + 0.12f, 0.05f);
        TuboZ(techoT, zS, zN, yTubo1 - 0.05f, xO + 0.15f, 0.045f);

        // Una sola luz para el cuarto: un foco colgando en el centro que parpadea (medio danado).
        {
            Vector3 p = new Vector3((xO + xE) * 0.5f, techo, (zS + zN) * 0.5f);
            float cable = 0.6f;
            Cilindro(techoT, "Cable", p - Vector3.up * cable * 0.5f, 0.006f, cable, matCable, false);
            Cilindro(techoT, "Portalampara", p - Vector3.up * (cable + 0.04f), 0.03f, 0.08f, matCable, false);
            var foco = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foco.name = "Foco";
            Object.DestroyImmediate(foco.GetComponent<Collider>());
            foco.transform.SetParent(techoT, false);
            foco.transform.position = p - Vector3.up * (cable + 0.13f);
            foco.transform.localScale = Vector3.one * 0.11f;
            var rFoco = foco.GetComponent<Renderer>();
            rFoco.sharedMaterial = matFoco;
            rFoco.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var luzGO = new GameObject("Luz del sotano (parpadea)");
            luzGO.transform.SetParent(techoT, false);
            luzGO.transform.position = foco.transform.position - Vector3.up * 0.08f;
            var luz = luzGO.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.8f, 0.55f);
            luz.range = 15f;
            luz.intensity = 12f;
            luz.shadows = LightShadows.Soft;
            luz.shadowStrength = 0.85f;
            // Sombras en resolucion baja: ya hay varias luces con sombra en la escena.
            var datos = luz.GetUniversalAdditionalLightData();
            var so = new SerializedObject(datos);
            var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tier != null) { tier.intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow; so.ApplyModifiedPropertiesWithoutUndo(); }

            var parpadeo = luzGO.AddComponent<LuzParpadeante>();
            parpadeo.foco = rFoco;
            puestos++;
        }
    }

    // Estanteria metalica (1.1 x 0.45 x 2.0 m) con cajas y objetos en sus repisas.
    static void Estanteria(Transform padre, Vector3 baseCentro, Vector3 largo, Vector3 haciaCuarto)
    {
        float L = 1.1f, P = 0.45f, A = 2.0f;
        Bounds huella = new Bounds(baseCentro + Vector3.up * A * 0.5f, Abs(largo * L + haciaCuarto * P) + Vector3.up * A);
        if (EnLibre(huella) || Choca(huella, null)) return;

        var e = new GameObject("Estanteria").transform;
        e.SetParent(padre, false);
        e.position = baseCentro;
        e.rotation = Quaternion.LookRotation(haciaCuarto, Vector3.up); // x local = a lo largo
        Vector3 lx = Vector3.right, lz = Vector3.forward;
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sz in new[] { -1f, 1f })
            PiezaLocal(e, "Poste", new Vector3(sx * (L * 0.5f - 0.02f), A * 0.5f, sz * (P * 0.5f - 0.02f)), new Vector3(0.04f, A, 0.04f), matMetal);
        float[] niveles = { 0.12f, 0.62f, 1.12f, 1.62f };
        for (int i = 0; i < niveles.Length; i++)
            PiezaLocal(e, "Repisa", new Vector3(0f, niveles[i], 0f), new Vector3(L, 0.025f, P), matMetal);
        var col = e.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, A * 0.5f, 0f); col.size = new Vector3(L, A, P);
        puestos++;

        // Contenido de cada repisa: cajas chicas y objetos.
        string[] objetos = { "radio", "farol", "reloj", "libros", "lampara", "taza", "maletin", "auto" };
        for (int i = 0; i < niveles.Length; i++)
        {
            float y = niveles[i] + 0.0125f;
            float alto = (i + 1 < niveles.Length ? niveles[i + 1] - niveles[i] : 0.5f) - 0.06f;
            float cursor = -L * 0.5f + 0.06f;
            while (cursor < L * 0.5f - 0.15f)
            {
                Vector3 local = new Vector3(0f, y, R(-0.04f, 0.04f));
                if (rnd.NextDouble() < 0.45)
                {
                    float w = R(0.2f, 0.36f), h = Mathf.Min(alto, R(0.18f, 0.34f)), d = R(0.22f, 0.36f);
                    if (cursor + w > L * 0.5f - 0.03f) break;
                    local.x = cursor + w * 0.5f;
                    CajaCarton(e, e.TransformPoint(local), new Vector3(w, h, d), e.eulerAngles.y + R(-6f, 6f), false);
                    cursor += w + R(0.03f, 0.1f);
                }
                else
                {
                    string clave = objetos[rnd.Next(objetos.Length)];
                    float tam = Mathf.Min(alto * 0.9f, R(0.22f, 0.34f));
                    local.x = cursor + tam * 0.5f;
                    if (cursor + tam > L * 0.5f - 0.03f) break;
                    var o = TiendaPropsSetup.CrearProp(clave, e, e.TransformPoint(local), tam, e.eulerAngles.y + R(-20f, 20f), false, rnd);
                    if (o && TiendaPropsSetup.LimitesDe(o, out Bounds bo))
                    {
                        float ancho = Mathf.Abs(Vector3.Dot(bo.size, Abs(e.right)));
                        o.transform.position += e.right * (cursor + ancho * 0.5f - local.x);
                        cursor += ancho + R(0.04f, 0.1f);
                        puestos++;
                    }
                    else cursor += 0.1f;
                }
            }
        }
    }

    // Pila de 1 a 3 cajas de carton de distintos tamanos.
    static void Pila(Transform padre, Vector3 baseCentro, int cantidad)
    {
        float y = baseCentro.y;
        float w0 = R(0.55f, 0.75f);
        for (int i = 0; i < cantidad; i++)
        {
            float w = Mathf.Max(0.3f, w0 - i * R(0.05f, 0.15f)), h = R(0.35f, 0.6f), d = Mathf.Max(0.3f, w * R(0.8f, 1.1f));
            Vector3 p = new Vector3(baseCentro.x + R(-0.06f, 0.06f), y, baseCentro.z + R(-0.06f, 0.06f));
            if (!CajaCarton(padre, p, new Vector3(w, h, d), R(-15f, 15f), i == 0)) break;
            y += h;
        }
    }

    static bool CajaCarton(Transform padre, Vector3 baseCentro, Vector3 tam, float yaw, bool comprobar)
    {
        Bounds b = new Bounds(baseCentro + Vector3.up * tam.y * 0.5f, tam * 1.15f);
        if (comprobar && (EnLibre(b) || Choca(b, null))) return false;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Caja de carton";
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(baseCentro + Vector3.up * tam.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = matCarton;
        puestos++;
        return true;
    }

    static bool Validar(GameObject go, bool colision, Transform padre)
    {
        if (!TiendaPropsSetup.LimitesDe(go, out Bounds b) || EnLibre(b) || Choca(b, go))
        {
            Object.DestroyImmediate(go);
            return false;
        }
        if (colision)
        {
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            col.size = new Vector3(b.size.x / Mathf.Abs(s.x), b.size.y / Mathf.Abs(s.y), b.size.z / Mathf.Abs(s.z));
        }
        puestos++;
        return true;
    }

    // ------------------------------------------------------------------ Materiales y texturas

    static void CrearMateriales()
    {
        Directory.CreateDirectory(CarpetaTex);
        Directory.CreateDirectory(CarpetaMat);
        Directory.CreateDirectory(CarpetaMallas);

        Texture2D tCarton = Textura("Carton", 512, PixelCarton);
        Texture2D tInterior = Textura("CartonInterior", 256, PixelInterior);
        Texture2D tPapel = Textura("PapelProtector", 512, PixelPapel);

        matCarton = Lit("Carton", Color.white, tCarton, 0.15f);
        matInterior = Lit("CartonInterior", Color.white, tInterior, 0.05f);
        matPapel = Lit("PapelProtector", Color.white, tPapel, 0.25f);
        matPapel.SetFloat("_Cull", 0f); // se ve de ambos lados
        matMetal = AssetDatabase.LoadAssetAtPath<Material>(Props + "metal.mat") ?? Lit("MetalEstante", new Color(0.35f, 0.36f, 0.38f), null, 0.4f, 0.7f);
        matTuberia = AssetDatabase.LoadAssetAtPath<Material>(Props + "cobre.mat") ?? Lit("Tuberia", new Color(0.5f, 0.3f, 0.2f), null, 0.5f, 0.8f);
        matCable = AssetDatabase.LoadAssetAtPath<Material>(Props + "negro.mat") ?? Lit("Cable", Color.black, null, 0.2f);
        matFoco = Lit("FocoApagado", new Color(0.95f, 0.9f, 0.75f), null, 0.8f, 0f, new Color(0.25f, 0.2f, 0.1f));
        AssetDatabase.SaveAssets();
    }

    delegate Color Pixel(float u, float v, System.Random azar);

    static Texture2D Textura(string nombre, int tam, Pixel f)
    {
        string ruta = $"{CarpetaTex}/{nombre}.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (existente) return existente; // si alguien la reemplazo, se respeta

        var azar = new System.Random(nombre.GetHashCode());
        var tex = new Texture2D(tam, tam, TextureFormat.RGB24, false);
        var px = new Color[tam * tam];
        for (int y = 0; y < tam; y++)
        for (int x = 0; x < tam; x++)
            px[y * tam + x] = f((x + 0.5f) / tam, (y + 0.5f) / tam, azar);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Carton: fibras, manchas, bordes gastados, cinta de embalar y una etiqueta.
    static Color PixelCarton(float u, float v, System.Random azar)
    {
        Color c = new Color(0.62f, 0.45f, 0.27f);
        float manchas = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.10f + Mathf.PerlinNoise(u * 21f + 3f, v * 21f) * 0.05f;
        float fibra = Mathf.PerlinNoise(u * 140f, v * 4f) * 0.06f;
        c *= 0.9f + manchas + fibra;
        float mancha = Mathf.PerlinNoise(u * 3.2f + 7f, v * 3.2f + 1f);
        if (mancha > 0.72f) c *= Mathf.Lerp(1f, 0.82f, (mancha - 0.72f) / 0.28f);
        float borde = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        if (borde < 0.035f) c *= Mathf.Lerp(0.7f, 1f, borde / 0.035f);

        // Cinta de embalar horizontal por el centro.
        if (Mathf.Abs(v - 0.5f) < 0.075f)
        {
            Color cinta = new Color(0.8f, 0.66f, 0.42f) * (0.95f + Mathf.PerlinNoise(u * 60f, v * 8f) * 0.1f);
            c = Color.Lerp(c, cinta, 0.75f);
            if (Mathf.Abs(Mathf.Abs(v - 0.5f) - 0.075f) < 0.004f) c *= 0.8f;
        }
        // Etiqueta con "texto" y codigo de barras.
        if (u > 0.6f && u < 0.9f && v > 0.12f && v < 0.32f)
        {
            c = new Color(0.9f, 0.88f, 0.82f);
            float lu = (u - 0.6f) / 0.3f, lv = (v - 0.12f) / 0.2f;
            if (lv > 0.55f && lv < 0.92f && Mathf.Repeat(lv * 6f, 1f) < 0.45f && Mathf.PerlinNoise(lu * 40f, lv * 3f) > 0.35f && lu > 0.08f && lu < 0.85f)
                c = new Color(0.15f, 0.15f, 0.15f);
            if (lv > 0.1f && lv < 0.42f && lu > 0.08f && lu < 0.92f && Mathf.PerlinNoise(lu * 90f, 0.5f) > 0.5f)
                c = new Color(0.1f, 0.1f, 0.1f);
        }
        return c;
    }

    // Carton por dentro: mas oscuro con el corrugado marcado.
    static Color PixelInterior(float u, float v, System.Random azar)
    {
        Color c = new Color(0.5f, 0.36f, 0.22f);
        c *= 0.9f + Mathf.PerlinNoise(u * 8f, v * 8f) * 0.15f + Mathf.Sin(u * 160f) * 0.025f;
        return c;
    }

    // Papel protector arrugado: blanco con pliegues claros y oscuros.
    static Color PixelPapel(float u, float v, System.Random azar)
    {
        float n1 = Mathf.PerlinNoise(u * 7f, v * 7f), n2 = Mathf.PerlinNoise(u * 19f + 5f, v * 19f + 2f);
        // Pliegues: lineas donde el ruido cruza ciertos valores.
        float pl1 = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Repeat(n1 * 6f, 1f) - 0.5f) * 14f);
        float pl2 = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Repeat(n2 * 5f, 1f) - 0.5f) * 16f);
        float sombra = 0.92f + n2 * 0.08f - pl1 * 0.13f - pl2 * 0.08f + Mathf.Clamp01(Mathf.Repeat(n1 * 6f, 1f) - 0.85f) * 0.4f;
        return new Color(0.93f, 0.91f, 0.86f) * sombra;
    }

    static Material Lit(string nombre, Color color, Texture2D tex, float suavidad, float metal = 0f, Color emision = default)
    {
        string ruta = $"{CarpetaMat}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        m.SetColor("_BaseColor", color);
        if (tex) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", suavidad);
        m.SetFloat("_Metallic", metal);
        if (emision != default)
        {
            m.SetColor("_EmissionColor", emision);
            m.EnableKeyword("_EMISSION");
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Utilidades

    static void Pieza(Transform padre, string nombre, Vector3 centro, Vector3 tam, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void PiezaLocal(Transform padre, string nombre, Vector3 local, Vector3 tam, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Cilindro(Transform padre, string nombre, Vector3 centro, float radio, float alto, Material mat, bool colision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        go.transform.localScale = new Vector3(radio * 2f, alto * 0.5f, radio * 2f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void TuboX(Transform padre, float x0, float x1, float y, float z, float r)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Tuberia";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, y, z), Quaternion.Euler(0f, 0f, 90f));
        go.transform.localScale = new Vector3(r * 2f, (x1 - x0) * 0.5f, r * 2f);
        go.GetComponent<Renderer>().sharedMaterial = matTuberia;
    }

    static void TuboZ(Transform padre, float z0, float z1, float y, float x, float r)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Tuberia";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(new Vector3(x, y, (z0 + z1) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(r * 2f, (z1 - z0) * 0.5f, r * 2f);
        go.GetComponent<Renderer>().sharedMaterial = matTuberia;
    }

    static bool EnLibre(Bounds b)
    {
        foreach (Bounds z in libres)
            if (b.min.x < z.max.x && b.max.x > z.min.x && b.min.z < z.max.z && b.max.z > z.min.z) return true;
        return false;
    }

    static bool Choca(Bounds b, GameObject propio)
    {
        Physics.SyncTransforms();
        Vector3 medio = b.extents * 0.85f;
        medio.y = Mathf.Max(0.02f, medio.y - 0.05f);
        foreach (Collider c in Physics.OverlapBox(b.center + Vector3.up * 0.05f, medio, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (propio && c.transform.IsChildOf(propio.transform)) continue;
            if (c.name == "piso") continue;
            return true;
        }
        return false;
    }

    static Bounds Caja2D(float x0, float x1, float z0, float z1)
    {
        var b = new Bounds(new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f), new Vector3(Mathf.Abs(x1 - x0), 100f, Mathf.Abs(z1 - z0)));
        return b;
    }

    static Renderer Rend(Transform raiz, string ruta)
    {
        Transform t = raiz.Find(ruta);
        return t ? t.GetComponent<Renderer>() : null;
    }

    static bool Limites(GameObject go, out Bounds b)
    {
        b = default; bool hay = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }
}
