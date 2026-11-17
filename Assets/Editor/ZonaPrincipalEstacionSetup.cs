using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Estetica de la Zona Principal: estacion de metro / central abandonada, OSCURA.
// No toca nada de lo que ya existe (piso, marcas de colores, paredes, cabina): todo se agrega encima, en
// "Zona_Principal/Estacion (estetica)".
// - Paredes de la sala y de los pasillos con azulejos de metro sucios (textura + mapa de normales, en metros: no se estiran).
// - Techo de concreto en la sala, tubos fluorescentes (pocos encendidos, uno parpadea, uno muerto), tuberias.
// - Columnas de azulejo, bancas, botes de basura, taquilla cerrada, maquina expendedora, cabinas fuera de servicio,
//   reloj de estacion colgante, letreros de cada pasillo con su color, placas "ESTACION CENTRAL",
//   mapa de lineas con los colores del piso, carteles rotos y papeles en el piso (fuera de las marcas).
// Se crea sola; rehacer: Tools > Zona Principal > Estacion de metro.
[InitializeOnLoad]
public static class ZonaPrincipalEstacionSetup
{
    const string Nombre = "Estacion (estetica)";
    const string Version = "_estacion v2";
    const string Carpeta = "Assets/ZonaPrincipal/Estacion";

    // Sala (caras interiores de las paredes) y centro (cabina)
    const float X0 = 87.8f, X1 = 112.35f, Z0 = -85.35f, Z1 = -60.55f, Alto = 4.25f, CX = 100f, CZ = -73f;
    // Bandas de las marcas de colores del piso (no se pone nada encima)
    const float BandaMedia = 3.8f;

    static readonly Color Indigo = new Color(0.38f, 0.12f, 1f), Amarillo = new Color(1f, 0.82f, 0.1f),
                          GrisCloaca = new Color(0.72f, 0.78f, 0.82f), Rojo = new Color(1f, 0.1f, 0.08f);

    static ZonaPrincipalEstacionSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var zona = GameObject.Find("Zona_Principal");
            if (!zona) return;
            // Solo se arma si no existe: si ya esta (aunque se haya editado a mano) no se toca
            var ya = zona.transform.Find(Nombre);
            if (!ya) { Crear(); return; }
            if (!ya.Find(MarcaLuces)) LucesComoSotano(ya.gameObject);
        };
    }

    // ================================================================== Luces con el efecto del sotano
    // Todas las luces de la estacion fallan y suenan como el foco del sotano (LuzParpadeante, con sus mismos ajustes
    // y sonidos). El brillo de su pieza (tubo, letrero, vidrio) sigue a la luz.
    const string MarcaLuces = "_luces como sotano";

    [MenuItem("Tools/Zona Principal/Luces como el sotano")]
    static void LucesComoSotanoMenu()
    {
        var zona = GameObject.Find("Zona_Principal");
        var raiz = zona ? zona.transform.Find(Nombre) : null;
        if (raiz) LucesComoSotano(raiz.gameObject);
    }

    static void LucesComoSotano(GameObject raiz)
    {
        // Modelo: el foco del sotano (cualquier LuzParpadeante que no sea de la estacion)
        LuzParpadeante modelo = null;
        foreach (var lp in Object.FindObjectsByType<LuzParpadeante>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!lp.transform.IsChildOf(raiz.transform)) { modelo = lp; break; }
        int n = 0;
        foreach (Light l in raiz.GetComponentsInChildren<Light>(true))
        {
            var lp = l.GetComponent<LuzParpadeante>();
            if (!lp) lp = Undo.AddComponent<LuzParpadeante>(l.gameObject);
            else Undo.RecordObject(lp, "Luces como el sotano");
            if (modelo) EditorUtility.CopySerialized(modelo, lp);
            // Pieza que brilla con la luz: la mas cercana con emision dentro del mismo grupo
            Renderer foco = null; float mejor = float.MaxValue;
            Transform grupo = l.transform.parent ? l.transform.parent : l.transform;
            foreach (Renderer r in grupo.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (!m || !m.IsKeywordEnabled("_EMISSION") || r.GetComponent<TextMeshPro>()) continue;
                float d = (r.bounds.center - l.transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; foco = r; }
            }
            lp.foco = foco;
            if (foco) lp.brilloFoco = foco.sharedMaterial.GetColor("_EmissionColor");
            EditorUtility.SetDirty(lp);
            n++;
        }
        var marca = new GameObject(MarcaLuces);
        Undo.RegisterCreatedObjectUndo(marca, "Luces como el sotano");
        marca.transform.SetParent(raiz.transform, false);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Estacion] {n} luces con el efecto y el sonido del foco del sotano{(modelo ? " (copiado de " + modelo.name + ")" : " (no se encontro el del sotano: valores por defecto)")}. Guarda la escena (Ctrl+S).", raiz);
    }

    [MenuItem("Tools/Zona Principal/Estacion de metro")]
    static void Crear()
    {
        var zona = GameObject.Find("Zona_Principal");
        if (!zona) { Debug.LogWarning("[Estacion] No hay Zona_Principal."); return; }
        var vieja = zona.transform.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        System.IO.Directory.CreateDirectory(Carpeta + "/Texturas");
        System.IO.Directory.CreateDirectory(Carpeta + "/Mallas");
        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, Nombre);
        raiz.transform.SetParent(zona.transform, true);
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        Transform r = raiz.transform;

        Physics.SyncTransforms();
        int paneles = Paredes(zona, Grupo(r, "Paredes (azulejo)"));
        Techo(Grupo(r, "Techo"));
        Columnas(Grupo(r, "Columnas"));
        Mobiliario(Grupo(r, "Mobiliario"));
        Letreros(Grupo(r, "Letreros"));
        Physics.SyncTransforms();
        Basura(Grupo(r, "Basura"));

        foreach (Renderer x in raiz.GetComponentsInChildren<Renderer>(true))
            if (!x.GetComponentInParent<Light>() && !x.GetComponent<TextMeshPro>())
                GameObjectUtility.SetStaticEditorFlags(x.gameObject, StaticEditorFlags.BatchingStatic);
        LucesComoSotano(raiz);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Estacion] Zona Principal como estacion de metro: {paneles} paredes con azulejo, techo, columnas, mobiliario y letreros. Guarda la escena (Ctrl+S).", raiz);
    }

    static Transform Grupo(Transform r, string n) { var g = new GameObject(n).transform; g.SetParent(r, false); return g; }

    // ================================================================== Paredes
    // Cada pared de la zona (Pasillos/*/Pared*) recibe un panel de azulejo en cada cara grande que da a un piso
    // (se comprueba con un rayo hacia abajo), apenas despegado de la pared.
    static int Paredes(GameObject zona, Transform g)
    {
        Material azulejo = MatAzulejo();
        int n = 0;
        var pasillos = zona.transform.Find("Pasillos");
        if (!pasillos) return 0;
        foreach (Renderer w in pasillos.GetComponentsInChildren<Renderer>())
        {
            if (!w.name.StartsWith("Pared")) continue;
            Bounds b = w.bounds;
            bool largoX = b.size.x >= b.size.z;
            Vector3 eje = largoX ? Vector3.forward : Vector3.right;
            float largo = largoX ? b.size.x : b.size.z, medio = largoX ? b.extents.z : b.extents.x;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 nrm = eje * s;
                Vector3 cara = new Vector3(b.center.x, 0f, b.center.z) + nrm * medio;
                if (!HayPiso(cara + nrm * 0.4f, w.transform)) continue;
                float top = Mathf.Min(b.max.y, Alto);
                var p = new GameObject("Azulejo " + w.name);
                p.transform.SetParent(g, false);
                p.transform.SetPositionAndRotation(cara + nrm * 0.012f, Quaternion.LookRotation(-nrm));
                p.AddComponent<MeshFilter>().sharedMesh = Quad(largo, top, 2f, 4.3f, true);
                var mr = p.AddComponent<MeshRenderer>(); mr.sharedMaterial = azulejo;
                n++;
            }
        }
        return n;
    }

    static bool HayPiso(Vector3 p, Transform ignorar)
    {
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(ignorar) && h.point.y < 0.4f && h.point.y > -0.6f) return true;
        return false;
    }

    // ================================================================== Techo y luces
    static void Techo(Transform g)
    {
        var t = new GameObject("Techo de concreto");
        t.transform.SetParent(g, false);
        // Quad horizontal mirando hacia abajo: largo en x, "alto" en z (cubre toda la sala y el grosor de las paredes)
        t.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        t.transform.position = new Vector3((X0 + X1) * 0.5f, Alto, Z0 - 0.8f);
        t.AddComponent<MeshFilter>().sharedMesh = Quad(X1 - X0 + 2f, Z1 - Z0 + 1.6f, 4f, 4f, true);
        t.AddComponent<MeshRenderer>().sharedMaterial = MatTecho();

        // Vigas de concreto cada 4 m
        Material conc = MatColor("Estacion_Concreto", new Color(0.13f, 0.13f, 0.14f), 0.1f);
        for (float z = Z0 + 2.3f; z < Z1 - 1f; z += 4.1f)
            Caja(g, "Viga", new Vector3((X0 + X1) * 0.5f, Alto - 0.18f, z), new Vector3(X1 - X0, 0.36f, 0.45f), conc);

        // Tuberias por el borde norte y sur
        Material tubo = MatColor("Estacion_Tuberia", new Color(0.16f, 0.17f, 0.16f), 0.45f, 0.6f);
        Material oxido = MatColor("Estacion_Oxido", new Color(0.24f, 0.12f, 0.06f), 0.15f, 0.2f);
        foreach (float z in new[] { Z1 - 0.3f, Z0 + 0.3f })
            foreach (var (dy, rad) in new[] { (0f, 0.09f), (-0.25f, 0.05f) })
            {
                var c = Cilindro(g, "Tuberia", new Vector3((X0 + X1) * 0.5f, Alto - 0.55f + dy, z), new Vector3(rad * 2f, (X1 - X0) * 0.5f, rad * 2f), tubo);
                c.transform.rotation = Quaternion.Euler(0, 0, 90);
                for (float x = X0 + 1.5f; x < X1; x += 3f)
                    Caja(g, "Abrazadera", new Vector3(x, Alto - 0.55f + dy, z), new Vector3(0.06f, rad * 2.6f, rad * 2.6f), oxido);
            }

        // Tubos fluorescentes: 3 x 2. Pocos encendidos (sala oscura), uno parpadea y uno esta muerto.
        Material carcasa = MatColor("Estacion_Lampara", new Color(0.2f, 0.21f, 0.22f), 0.3f, 0.5f);
        Material difusorOn = Emisivo("Estacion_Fluorescente", new Color(0.85f, 0.95f, 1f), 1.6f);
        Material difusorOff = MatColor("Estacion_FluorescenteApagado", new Color(0.32f, 0.33f, 0.34f), 0.4f);
        Color frio = new Color(0.72f, 0.86f, 1f);
        int i = 0;
        foreach (float x in new[] { CX - 7.5f, CX, CX + 7.5f })
            foreach (float z in new[] { CZ - 6.2f, CZ + 6.2f })
            {
                string estado = i == 2 ? "muerto" : i == 4 ? "parpadea" : i == 1 ? "muerto" : "on";
                i++;
                Vector3 p = new Vector3(x, Alto - 0.36f - 0.05f, z);
                var f = new GameObject("Fluorescente (" + estado + ")").transform; f.SetParent(g, false); f.position = p;
                Caja(f, "Carcasa", p, new Vector3(2.5f, 0.08f, 0.38f), carcasa);
                var dif = Caja(f, "Difusor", p + Vector3.down * 0.05f, new Vector3(2.35f, 0.03f, 0.28f), estado == "muerto" ? difusorOff : estado == "parpadea" ? Emisivo("Estacion_FluorescenteParpadea", new Color(0.85f, 0.95f, 1f), 1.6f) : difusorOn);
                foreach (float sx in new[] { -0.9f, 0.9f }) Caja(f, "Cadena", p + new Vector3(sx, 0.04f, 0), new Vector3(0.015f, 0.08f, 0.015f), carcasa);
                if (estado == "muerto") continue;
                var lgo = new GameObject("Luz"); lgo.transform.SetParent(f, false); lgo.transform.position = p + Vector3.down * 0.35f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.color = frio; l.intensity = estado == "parpadea" ? 2.6f : 2.2f; l.range = 7.5f;
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f;
                if (estado == "parpadea")
                {
                    var lp = lgo.AddComponent<LuzParpadeante>();
                    lp.foco = dif.GetComponent<Renderer>();
                    lp.brilloFoco = new Color(0.85f, 0.95f, 1f) * 1.6f;
                    lp.probApagon = 0.45f;
                }
            }
    }

    // ================================================================== Columnas
    static void Columnas(Transform g)
    {
        Material azulejo = MatAzulejo();
        foreach (var c in new[] { new Vector2(94.4f, -79.6f), new Vector2(105.6f, -79.6f), new Vector2(94.4f, -66.4f), new Vector2(105.6f, -66.4f) })
        {
            var col = new GameObject("Columna").transform; col.SetParent(g, false); col.position = new Vector3(c.x, 0, c.y);
            const float a = 0.8f;
            foreach (Vector3 nrm in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var p = new GameObject("Cara"); p.transform.SetParent(col, false);
                p.transform.SetPositionAndRotation(col.position + nrm * a * 0.5f, Quaternion.LookRotation(-nrm));
                p.AddComponent<MeshFilter>().sharedMesh = Quad(a, Alto, 2f, 4.3f, true);
                p.AddComponent<MeshRenderer>().sharedMaterial = azulejo;
            }
            var bc = col.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, Alto * 0.5f, 0); bc.size = new Vector3(a, Alto, a);
            // Cantoneras de metal en las esquinas
            Material metal = MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                Caja(col, "Cantonera", col.position + new Vector3(sx * a * 0.5f, 1.0f, sz * a * 0.5f), new Vector3(0.04f, 2f, 0.04f), metal);
        }
    }

    // ================================================================== Mobiliario
    static void Mobiliario(Transform g)
    {
        // Bancas: junto a las columnas y contra las paredes, mirando hacia la cabina
        Banca(g, new Vector3(94.4f, 0, -81.0f), 180f, 4);
        Banca(g, new Vector3(105.6f, 0, -81.0f), 180f, 4);
        Banca(g, new Vector3(94.4f, 0, -65.0f), 0f, 4);
        Banca(g, new Vector3(105.6f, 0, -65.0f), 0f, 4);
        Banca(g, new Vector3(108.8f, 0, -61.3f), 180f, 5);
        Banca(g, new Vector3(111.6f, 0, -81.2f), -90f, 4);
        Banca(g, new Vector3(91.2f, 0, -84.6f), 0f, 5);

        // Botes de basura
        foreach (var p in new[] { new Vector3(95.85f, 0, -81.0f), new Vector3(104.15f, 0, -65.0f), new Vector3(111.7f, 0, -78.4f), new Vector3(88.5f, 0, -78.2f) })
            Bote(g, p);

        // Cabinas fuera de servicio (copias de la real, sin scripts) contra la pared norte, en la esquina noroeste
        var cabina = GameObject.Find("Zona_Principal/Cabina_Telefonica (1)");
        if (cabina)
            foreach (float x in new[] { 89.0f, 90.5f, 92.0f })
                CabinaRota(g, cabina, new Vector3(x, 0, Z1 - 0.75f));

        Taquilla(g, new Vector3(108.8f, 0, Z0 + 1.15f));
        Expendedora(g, new Vector3(X1 - 0.5f, 0, -64.6f), -90f);
    }

    static void Banca(Transform g, Vector3 p, float giro, int asientos)
    {
        var b = new GameObject("Banca").transform; b.SetParent(g, false); b.SetPositionAndRotation(p, Quaternion.Euler(0, giro, 0));
        Material metal = MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f);
        Material madera = MatColor("Estacion_MaderaBanca", new Color(0.26f, 0.16f, 0.09f), 0.3f);
        float largo = asientos * 0.55f;
        foreach (float x in new[] { -largo * 0.5f + 0.1f, 0f, largo * 0.5f - 0.1f })
        {
            Caja(b, "Pata", b.TransformPoint(new Vector3(x, 0.22f, 0.05f)), new Vector3(0.05f, 0.44f, 0.4f), metal, b.rotation);
            Caja(b, "Soporte", b.TransformPoint(new Vector3(x, 0.66f, -0.2f)), new Vector3(0.05f, 0.5f, 0.05f), metal, b.rotation * Quaternion.Euler(-12, 0, 0));
        }
        for (int k = 0; k < 3; k++) Caja(b, "Tabla asiento", b.TransformPoint(new Vector3(0, 0.46f, -0.08f + k * 0.13f)), new Vector3(largo, 0.04f, 0.11f), madera, b.rotation);
        for (int k = 0; k < 2; k++) Caja(b, "Tabla respaldo", b.TransformPoint(new Vector3(0, 0.66f + k * 0.17f, -0.22f - k * 0.035f)), new Vector3(largo, 0.12f, 0.035f), madera, b.rotation * Quaternion.Euler(-12, 0, 0));
        var bc = b.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.45f, -0.05f); bc.size = new Vector3(largo, 0.9f, 0.55f);
    }

    static void Bote(Transform g, Vector3 p)
    {
        Material verde = MatColor("Estacion_Bote", new Color(0.07f, 0.13f, 0.09f), 0.35f, 0.5f);
        Material negro = MatColor("Estacion_Negro", new Color(0.03f, 0.03f, 0.03f), 0.2f);
        var b = new GameObject("Bote de basura").transform; b.SetParent(g, false); b.position = p;
        Cilindro(b, "Cuerpo", p + Vector3.up * 0.45f, new Vector3(0.5f, 0.45f, 0.5f), verde);
        Cilindro(b, "Tapa", p + Vector3.up * 0.93f, new Vector3(0.54f, 0.03f, 0.54f), negro);
        Cilindro(b, "Bolsa", p + Vector3.up * 0.95f, new Vector3(0.38f, 0.03f, 0.38f), negro);
        var bc = b.gameObject.AddComponent<CapsuleCollider>(); bc.center = Vector3.up * 0.48f; bc.radius = 0.26f; bc.height = 0.96f;
    }

    static void CabinaRota(Transform g, GameObject original, Vector3 pie)
    {
        var c = Object.Instantiate(original, g);
        c.name = "Cabina fuera de servicio";
        foreach (var mb in c.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        foreach (var col in c.GetComponentsInChildren<Collider>(true)) if (col.isTrigger) Object.DestroyImmediate(col);
        foreach (var l in c.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var a in c.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        c.transform.rotation = original.transform.rotation * Quaternion.Euler(0, 180f, 0);
        Bounds b = Limites(c);
        c.transform.position += new Vector3(pie.x - b.center.x, 0, pie.z - b.center.z);
        b = Limites(c);
        // Cinta en X sobre el frente (lado sur) + cartel
        Material cinta = MatColor("Estacion_Cinta", new Color(0.85f, 0.65f, 0.05f), 0.4f);
        Vector3 frente = new Vector3(b.center.x, 1.3f, b.min.z - 0.03f);
        foreach (float a in new[] { 35f, -35f })
            Caja(c.transform, "Cinta", frente, new Vector3(1.35f, 0.08f, 0.01f), cinta, Quaternion.Euler(0, 0, a));
        Texto(c.transform, "FUERA DE\nSERVICIO", frente + new Vector3(0, 0.55f, -0.01f), Quaternion.identity, 1.6f, new Color(0.9f, 0.85f, 0.75f), 1.0f, 0.45f, new Color(0.08f, 0.07f, 0.06f));
    }

    static void Taquilla(Transform g, Vector3 p)
    {
        var t = new GameObject("Taquilla").transform; t.SetParent(g, false); t.position = p;
        Material pared = MatAzulejo();
        Material marco = MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f);
        Material vidrio = MatColor("Estacion_VidrioOscuro", new Color(0.02f, 0.03f, 0.04f), 0.95f);
        Material cortina = MatColor("Estacion_Cortina", new Color(0.2f, 0.21f, 0.2f), 0.35f, 0.7f);
        const float ancho = 3.2f, fondo = 1.8f, alto = 2.7f;
        // Cuerpo (azulejo en el frente bajo y lados)
        Caja(t, "Mostrador", p + new Vector3(0, 0.55f, 0), new Vector3(ancho, 1.1f, fondo), MatColor("Estacion_TaquillaBase", new Color(0.09f, 0.1f, 0.11f), 0.3f));
        Caja(t, "Techo taquilla", p + new Vector3(0, alto - 0.15f, 0), new Vector3(ancho + 0.1f, 0.3f, fondo + 0.1f), MatColor("Estacion_TaquillaBase", new Color(0.09f, 0.1f, 0.11f), 0.3f));
        foreach (float sx in new[] { -1f, 1f })
            Caja(t, "Lado", p + new Vector3(sx * (ancho * 0.5f - 0.06f), alto * 0.5f, 0), new Vector3(0.12f, alto, fondo), marco);
        Caja(t, "Vidrio", p + new Vector3(0, 1.75f, fondo * 0.5f - 0.05f), new Vector3(ancho - 0.2f, 1.1f, 0.03f), vidrio);
        // Cortina metalica medio bajada
        for (int k = 0; k < 9; k++)
            Caja(t, "Cortina", p + new Vector3(0, 2.4f - k * 0.07f, fondo * 0.5f + 0.01f), new Vector3(ancho - 0.25f, 0.06f, 0.025f), cortina);
        Caja(t, "Repisa", p + new Vector3(0, 1.12f, fondo * 0.5f + 0.15f), new Vector3(ancho - 0.3f, 0.04f, 0.3f), marco);
        Texto(t, "TAQUILLA", p + new Vector3(0, alto + 0.25f, fondo * 0.5f + 0.06f), Quaternion.Euler(0, 180f, 0), 3.0f, new Color(0.92f, 0.9f, 0.82f), 2.6f, 0.42f, new Color(0.05f, 0.08f, 0.14f));
        Texto(t, "CERRADO", p + new Vector3(0, 1.5f, fondo * 0.5f - 0.08f), Quaternion.Euler(0, 180f, 0), 2.2f, Rojo * 1.4f, 1.2f, 0.3f, null);
        // Foco tenue adentro
        var lgo = new GameObject("Luz taquilla"); lgo.transform.SetParent(t, false); lgo.transform.position = p + new Vector3(0, 2.2f, -0.2f);
        var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.intensity = 0.9f; l.range = 3.2f;
        var bc = t.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, alto * 0.5f, 0); bc.size = new Vector3(ancho, alto, fondo);
    }

    static void Expendedora(Transform g, Vector3 p, float giro)
    {
        var e = new GameObject("Maquina expendedora").transform; e.SetParent(g, false); e.SetPositionAndRotation(p, Quaternion.Euler(0, giro, 0));
        Material cuerpo = MatColor("Estacion_Expendedora", new Color(0.3f, 0.03f, 0.04f), 0.45f, 0.3f);
        Material vidrio = Emisivo("Estacion_ExpendedoraVidrio", new Color(0.05f, 0.07f, 0.08f), 0f);
        vidrio.SetColor("_EmissionColor", new Color(0.35f, 0.42f, 0.5f) * 0.6f);
        Caja(e, "Cuerpo", e.TransformPoint(new Vector3(0, 0.95f, 0)), new Vector3(1.0f, 1.9f, 0.8f), cuerpo, e.rotation);
        Caja(e, "Vidrio", e.TransformPoint(new Vector3(-0.1f, 1.15f, 0.41f)), new Vector3(0.65f, 1.25f, 0.02f), vidrio, e.rotation);
        Material[] productos =
        {
            MatColor("Estacion_Producto1", new Color(0.7f, 0.15f, 0.1f), 0.5f), MatColor("Estacion_Producto2", new Color(0.1f, 0.35f, 0.7f), 0.5f),
            MatColor("Estacion_Producto3", new Color(0.8f, 0.7f, 0.1f), 0.5f), MatColor("Estacion_Producto4", new Color(0.2f, 0.6f, 0.2f), 0.5f),
        };
        var rnd = new System.Random(4);
        for (int fila = 0; fila < 5; fila++)
            for (int k = 0; k < 4; k++)
            {
                if (rnd.NextDouble() < 0.3) continue; // huecos: ya casi vacia
                Caja(e, "Producto", e.TransformPoint(new Vector3(-0.34f + k * 0.16f, 0.65f + fila * 0.23f, 0.3f)), new Vector3(0.1f, 0.15f, 0.08f), productos[rnd.Next(4)], e.rotation);
            }
        Caja(e, "Teclado", e.TransformPoint(new Vector3(0.36f, 1.25f, 0.41f)), new Vector3(0.16f, 0.35f, 0.02f), MatColor("Estacion_Negro", new Color(0.03f, 0.03f, 0.03f), 0.2f), e.rotation);
        Caja(e, "Bandeja", e.TransformPoint(new Vector3(-0.1f, 0.3f, 0.41f)), new Vector3(0.6f, 0.2f, 0.02f), MatColor("Estacion_Negro", new Color(0.03f, 0.03f, 0.03f), 0.2f), e.rotation);
        var lgo = new GameObject("Brillo"); lgo.transform.SetParent(e, false); lgo.transform.position = e.TransformPoint(new Vector3(0, 1.2f, 0.9f));
        var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.6f, 0.75f, 1f); l.intensity = 0.7f; l.range = 2.6f;
        var bc = e.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.95f, 0); bc.size = new Vector3(1f, 1.9f, 0.8f);
    }

    // ================================================================== Letreros, reloj, mapa y carteles
    static void Letreros(Transform g)
    {
        // Letrero colgante en la boca de cada pasillo, con su color (mirando hacia la sala)
        Colgante(g, "LABERINTO", Indigo, new Vector3(X1 - 0.6f, 3.35f, CZ), Vector3.left);
        Colgante(g, "BACKROOMS", Amarillo, new Vector3(CX, 3.35f, Z0 + 0.6f), Vector3.forward);
        Colgante(g, "CLOACAS", GrisCloaca, new Vector3(CX, 3.35f, Z1 - 0.6f), Vector3.back);
        Colgante(g, "? ? ?", Rojo, new Vector3(X0 + 0.6f, 3.35f, CZ), Vector3.right);

        // Placas "ESTACION CENTRAL" en las paredes (en los tramos entre esquina y pasillo)
        foreach (var (pos, nrm) in new[] {
            (new Vector3(108.9f, 2.55f, Z1), Vector3.back),
            (new Vector3(91.2f, 2.55f, Z0), Vector3.forward), (new Vector3(X0, 2.55f, -64.4f), Vector3.right), (new Vector3(X1, 2.55f, -81.6f), Vector3.left) })
            Placa(g, pos + nrm * 0.03f, nrm);

        // Reloj de estacion colgante de dos caras sobre el pasillo de los Backrooms
        Reloj(g, new Vector3(CX, 3.2f, CZ - 8.8f));

        // Mapa de lineas (los colores del piso) y carteles rotos
        Cuadro(g, "Mapa de lineas", MatMapa(), new Vector3(X0 + 0.03f, 1.75f, -81.0f), Vector3.right, 1.3f, 1.75f);
        Cuadro(g, "Mapa de lineas", MatMapa(), new Vector3(X1 - 0.03f, 1.75f, -66.5f), Vector3.left, 1.3f, 1.75f);
        Texto(g, "RED DE LINEAS", new Vector3(X0 + 0.06f, 2.82f, -81.0f), Quaternion.LookRotation(Vector3.left), 1.6f, new Color(0.85f, 0.83f, 0.75f), 1.3f, 0.2f, null);
        Texto(g, "RED DE LINEAS", new Vector3(X1 - 0.06f, 2.82f, -66.5f), Quaternion.LookRotation(Vector3.right), 1.6f, new Color(0.85f, 0.83f, 0.75f), 1.3f, 0.2f, null);
        int c = 0;
        foreach (var (pos, nrm) in new[] {
            (new Vector3(105.4f, 1.6f, Z0), Vector3.forward), (new Vector3(93.7f, 1.5f, Z1), Vector3.back), (new Vector3(X0, 1.55f, -67.6f), Vector3.right),
            (new Vector3(X1, 1.6f, -79.5f), Vector3.left), (new Vector3(111.0f, 1.45f, Z1), Vector3.back) })
            Cuadro(g, "Cartel roto", MatCartel(c++), pos + nrm * 0.03f, nrm, 0.95f, 1.35f);
    }

    static void Colgante(Transform g, string texto, Color col, Vector3 p, Vector3 haciaSala)
    {
        var l = new GameObject("Letrero " + texto).transform; l.SetParent(g, false);
        Vector3 lado = Vector3.Cross(Vector3.up, haciaSala);
        l.SetPositionAndRotation(p, Quaternion.LookRotation(-haciaSala));
        Material tabla = MatColor("Estacion_Letrero", new Color(0.035f, 0.035f, 0.045f), 0.4f, 0.4f);
        Material metal = MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f);
        Caja(l, "Tabla", p, new Vector3(4.0f, 0.62f, 0.14f), tabla, l.rotation);
        Material franja = Emisivo("Estacion_Franja " + texto.Replace("?", "X"), col, 1.6f);
        Caja(l, "Franja", p + Vector3.down * 0.27f + haciaSala * 0.075f, new Vector3(4.0f, 0.06f, 0.01f), franja, l.rotation);
        Caja(l, "Franja (atras)", p + Vector3.down * 0.27f - haciaSala * 0.075f, new Vector3(4.0f, 0.06f, 0.01f), franja, l.rotation);
        foreach (float s in new[] { -1.6f, 1.6f })
        {
            float y0 = p.y + 0.31f, hb = Alto - y0;
            Caja(l, "Barra", new Vector3(p.x, y0 + hb * 0.5f, p.z) + lado * s, new Vector3(0.03f, hb, 0.03f), metal, l.rotation);
        }
        Texto(l, texto + "  >", p + haciaSala * 0.075f + Vector3.up * 0.03f, Quaternion.LookRotation(-haciaSala), 4.0f, col * 1.25f, 3.8f, 0.5f, null);
        var lgo = new GameObject("Brillo"); lgo.transform.SetParent(l, false); lgo.transform.position = p + haciaSala * 0.7f + Vector3.down * 0.3f;
        var luz = lgo.AddComponent<Light>(); luz.type = LightType.Point; luz.color = col; luz.intensity = 0.6f; luz.range = 2.5f;
    }

    static void Placa(Transform g, Vector3 p, Vector3 nrm)
    {
        var t = new GameObject("Placa ESTACION CENTRAL").transform; t.SetParent(g, false);
        t.SetPositionAndRotation(p, Quaternion.LookRotation(-nrm));
        Material borde = MatColor("Estacion_PlacaBorde", new Color(0.45f, 0.38f, 0.2f), 0.6f, 0.7f);
        Material fondo = MatColor("Estacion_PlacaFondo", new Color(0.03f, 0.07f, 0.16f), 0.5f);
        Caja(t, "Borde", p + nrm * 0.015f, new Vector3(3.0f, 0.62f, 0.03f), borde, t.rotation);
        Caja(t, "Fondo", p + nrm * 0.032f, new Vector3(2.88f, 0.5f, 0.01f), fondo, t.rotation);
        Texto(t, "ESTACION CENTRAL", p + nrm * 0.04f, Quaternion.LookRotation(-nrm), 2.6f, new Color(0.88f, 0.86f, 0.78f), 2.8f, 0.45f, null);
    }

    static void Reloj(Transform g, Vector3 p)
    {
        var r = new GameObject("Reloj de estacion").transform; r.SetParent(g, false); r.position = p;
        Material marco = MatColor("Estacion_Negro", new Color(0.03f, 0.03f, 0.03f), 0.2f);
        Material cara = Emisivo("Estacion_RelojCara", new Color(0.8f, 0.78f, 0.7f), 0f);
        cara.SetColor("_EmissionColor", new Color(0.3f, 0.3f, 0.27f));
        Material metal = MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f);
        float yb = p.y + 0.5f, hr = Alto - yb;
        Caja(r, "Barra", new Vector3(p.x, yb + hr * 0.5f, p.z), new Vector3(0.04f, hr, 0.04f), metal);
        var aro = Cilindro(r, "Aro", p, new Vector3(1.0f, 0.09f, 1.0f), marco); aro.transform.rotation = Quaternion.Euler(90, 0, 0);
        foreach (float s in new[] { -1f, 1f })
        {
            var c = Cilindro(r, "Cara", p + Vector3.forward * s * 0.092f, new Vector3(0.88f, 0.005f, 0.88f), cara); c.transform.rotation = Quaternion.Euler(90, 0, 0);
            Vector3 f = Vector3.forward * s * 0.1f;
            for (int h = 0; h < 12; h++)
            {
                float a = h * 30f * Mathf.Deg2Rad;
                Caja(r, "Marca", p + f + new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * 0.37f, new Vector3(0.025f, h % 3 == 0 ? 0.09f : 0.05f, 0.005f), marco, Quaternion.Euler(0, 0, -h * 30f));
            }
            // Detenido a las 3:07 (las manecillas giran desde el centro)
            Caja(r, "Horario", p + f + new Vector3(0.11f, 0.01f, 0), new Vector3(0.22f, 0.04f, 0.006f), marco, Quaternion.Euler(0, 0, -4f));
            Caja(r, "Minutero", p + f + new Vector3(0.12f, 0.27f, 0) * 0.55f, new Vector3(0.03f, 0.34f, 0.006f), marco, Quaternion.Euler(0, 0, -42f));
        }
    }

    static void Cuadro(Transform g, string n, Material m, Vector3 p, Vector3 nrm, float ancho, float alto)
    {
        var c = new GameObject(n); c.transform.SetParent(g, false);
        c.transform.SetPositionAndRotation(p + nrm * 0.02f, Quaternion.LookRotation(-nrm));
        c.AddComponent<MeshFilter>().sharedMesh = Quad(ancho, alto, ancho, alto, false);
        c.AddComponent<MeshRenderer>().sharedMaterial = m;
        if (n.StartsWith("Mapa"))
            Caja(g, "Marco mapa", p + nrm * 0.012f, new Vector3(Mathf.Abs(nrm.x) > 0.5f ? 0.02f : ancho + 0.1f, alto + 0.1f, Mathf.Abs(nrm.x) > 0.5f ? ancho + 0.1f : 0.02f),
                MatColor("Estacion_Metal", new Color(0.3f, 0.31f, 0.32f), 0.5f, 0.8f));
    }

    // ================================================================== Basura (fuera de las marcas del piso)
    static void Basura(Transform g)
    {
        var rnd = new System.Random(9);
        Material papel = MatColor("Estacion_Papel", new Color(0.55f, 0.53f, 0.46f), 0.15f);
        Material lata = MatColor("Estacion_Lata", new Color(0.45f, 0.08f, 0.06f), 0.6f, 0.8f);
        int n = 0;
        for (int i = 0; i < 400 && n < 70; i++)
        {
            float x = X0 + 0.4f + (float)rnd.NextDouble() * (X1 - X0 - 0.8f), z = Z0 + 0.4f + (float)rnd.NextDouble() * (Z1 - Z0 - 0.8f);
            if (Mathf.Abs(x - CX) < BandaMedia + 0.3f || Mathf.Abs(z - CZ) < BandaMedia + 0.3f) continue; // marcas de colores
            if (Physics.CheckSphere(new Vector3(x, 0.3f, z), 0.25f, ~0, QueryTriggerInteraction.Ignore)) continue;
            n++;
            if (rnd.NextDouble() < 0.82)
                Caja(g, "Papel", new Vector3(x, 0.008f + n * 0.0002f, z), new Vector3(0.2f + (float)rnd.NextDouble() * 0.1f, 0.003f, 0.28f), papel, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0));
            else
                Cilindro(g, "Lata", new Vector3(x, 0.033f, z), new Vector3(0.066f, 0.06f, 0.066f), lata).transform.rotation = Quaternion.Euler(90, (float)rnd.NextDouble() * 360f, 0);
        }
    }

    // ================================================================== Texturas y materiales
    static Material MatAzulejo()
    {
        string rc = $"{Carpeta}/Texturas/Azulejo.png", rn = $"{Carpeta}/Texturas/Azulejo_Normal.png";
        if (!System.IO.File.Exists(rc) || !System.IO.File.Exists(rn)) GenerarAzulejo(rc, rn);
        Material m = Mat("Estacion_Azulejo");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rn)); m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_BumpScale", 1f);
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.5f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatTecho()
    {
        string rc = $"{Carpeta}/Texturas/Techo.png", rn = $"{Carpeta}/Texturas/Techo_Normal.png";
        if (!System.IO.File.Exists(rc) || !System.IO.File.Exists(rn)) GenerarTecho(rc, rn);
        Material m = Mat("Estacion_Techo");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rn)); m.EnableKeyword("_NORMALMAP");
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatMapa()
    {
        string rc = $"{Carpeta}/Texturas/MapaLineas.png";
        if (!System.IO.File.Exists(rc)) GenerarMapa(rc);
        Material m = Mat("Estacion_Mapa");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.12f));
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.7f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatCartel(int i)
    {
        string rc = $"{Carpeta}/Texturas/Cartel{i}.png";
        if (!System.IO.File.Exists(rc)) GenerarCartel(rc, i);
        Material m = Mat("Estacion_Cartel" + i);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rc));
        m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.2f);
        // Las partes arrancadas del cartel son transparentes (recorte)
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = 2450;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Azulejo de metro: 2 m de ancho (se repite) x 4.3 m de alto (la pared entera).
    // Zocalo de concreto, franja baja de azulejo verde oscuro, filete, azulejo blanco sucio en ladrillo y concreto arriba.
    static void GenerarAzulejo(string rc, string rn)
    {
        const int W = 512, Ht = 1100;
        const float AnchoM = 2f, AltoM = 4.3f;
        var col = new Color[W * Ht]; var alt = new float[W * Ht];
        Color verde = new Color(0.07f, 0.17f, 0.15f), blanco = new Color(0.44f, 0.44f, 0.4f), concreto = new Color(0.16f, 0.16f, 0.16f), junta = new Color(0.1f, 0.1f, 0.09f);
        for (int py = 0; py < Ht; py++)
            for (int px = 0; px < W; px++)
            {
                float u = (px + 0.5f) / W * AnchoM, y = (py + 0.5f) / Ht * AltoM;
                float sucio = Ruido(u, y, AnchoM, 1.3f, 3f) * 0.55f + Ruido(u, y, AnchoM, 6f, 7f) * 0.25f;
                Color c; float h;
                if (y < 0.18f) { c = concreto * (0.7f + sucio * 0.5f); h = 0.55f; }
                else if (y < 1.25f)
                {
                    // azulejo chico cuadrado (14 por 2 m)
                    float tw = AnchoM / 14f, tu = u / tw, tv = (y - 0.18f) / tw;
                    bool j = Frac(tu) < 0.07f || Frac(tv) < 0.07f;
                    float var = Hash(Mathf.Floor(tu) % 14f, Mathf.Floor(tv));
                    c = j ? junta : verde * (0.85f + var * 0.3f); h = j ? 0.2f : 0.75f + var * 0.05f;
                }
                else if (y < 1.38f) { bool j = Frac((y - 1.25f) / 0.13f) < 0.1f; c = j ? junta : new Color(0.03f, 0.05f, 0.08f); h = j ? 0.2f : 0.85f; }
                else if (y < 3.25f)
                {
                    // azulejo de metro (7 por 2 m, 2:1, en ladrillo)
                    float tw = AnchoM / 7f, th = tw * 0.5f;
                    float fila = Mathf.Floor((y - 1.38f) / th);
                    float tu = u / tw + (fila % 2f) * 0.5f, tv = (y - 1.38f) / th;
                    bool j = Frac(tu) < 0.035f || Frac(tv) < 0.07f;
                    float id = Mathf.Floor(tu) % 7f, var = Hash(id, fila);
                    bool falta = Hash(id + 31f, fila * 1.7f) > 0.965f;           // azulejos caidos
                    bool roto = !falta && Hash(id + 7f, fila * 2.3f) > 0.93f;     // con grieta
                    // borde biselado del azulejo
                    float bord = Mathf.Min(Mathf.Min(Frac(tu), 1f - Frac(tu)) * tw, Mathf.Min(Frac(tv), 1f - Frac(tv)) * th);
                    if (falta) { c = concreto * (0.6f + sucio * 0.4f); h = 0.15f; }
                    else if (j) { c = junta; h = 0.25f; }
                    else
                    {
                        c = blanco * (0.88f + var * 0.18f); h = 0.7f + Mathf.Clamp01(bord / 0.012f) * 0.15f;
                        if (roto && Mathf.Abs(Frac(tu) - 0.3f - Frac(tv) * 0.4f) < 0.02f) { c = junta; h = 0.4f; }
                    }
                }
                else { c = concreto * (0.75f + sucio * 0.6f); h = 0.5f + sucio * 0.2f; }

                // Mugre: manchas, chorreados desde arriba (columnas) y mas sucio abajo
                float chorro = Mathf.Pow(Ruido(u, 0f, AnchoM, 9f, 1f), 3f) * Mathf.Clamp01((y - 1.0f) / 3.3f) * 1.4f;
                float abajo = Mathf.Clamp01(1f - y / 1.6f) * 0.35f;
                float mugre = Mathf.Clamp01(sucio * 0.65f + chorro + abajo);
                c = Color.Lerp(c, new Color(0.06f, 0.055f, 0.04f), mugre * 0.85f);
                col[py * W + px] = new Color(c.r, c.g, c.b, 1f); alt[py * W + px] = h;
            }
        GuardarPng(rc, W, Ht, col, false, true);
        GuardarPng(rn, W, Ht, Normales(alt, W, Ht, 5f), true, true);
    }

    // Losas de concreto de 2 x 2 m (4 x 4 m por textura) con manchas de humedad
    static void GenerarTecho(string rc, string rn)
    {
        const int T = 512; const float M = 4f;
        var col = new Color[T * T]; var alt = new float[T * T];
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                float u = (px + 0.5f) / T * M, v = (py + 0.5f) / T * M;
                bool j = Frac(u / 2f) < 0.012f || Frac(v / 2f) < 0.012f;
                float n = Ruido(u, v, M, 1.2f, 1.2f) * 0.6f + Ruido(u, v, M, 8f, 8f) * 0.3f;
                float mancha = Mathf.Clamp01((Ruido(u + 3f, v, M, 0.8f, 0.8f) - 0.55f) * 4f);
                Color c = new Color(0.17f, 0.17f, 0.17f) * (0.75f + n * 0.5f);
                c = Color.Lerp(c, new Color(0.07f, 0.065f, 0.05f), mancha * 0.8f);
                if (j) c *= 0.4f;
                col[py * T + px] = c; alt[py * T + px] = j ? 0.2f : 0.6f + n * 0.15f;
            }
        GuardarPng(rc, T, T, col, false, true);
        GuardarPng(rn, T, T, Normales(alt, T, T, 3f), true, true);
    }

    // Mapa de la red: 4 lineas de los colores del piso que salen de la estacion central
    static void GenerarMapa(string rc)
    {
        const int W = 520, Ht = 700;
        var col = new Color[W * Ht];
        Vector2 centro = new Vector2(W * 0.5f, Ht * 0.47f);
        var lineas = new[] {
            (Indigo, new[] { centro, new Vector2(330, 330), new Vector2(430, 330), new Vector2(470, 220) }),
            (Amarillo, new[] { centro, new Vector2(260, 200), new Vector2(180, 120), new Vector2(120, 120) }),
            (new Color(0.55f, 0.6f, 0.65f), new[] { centro, new Vector2(260, 470), new Vector2(330, 560), new Vector2(330, 640) }),
            (Rojo, new[] { centro, new Vector2(170, 330), new Vector2(90, 400), new Vector2(60, 520) }),
        };
        for (int py = 0; py < Ht; py++)
            for (int px = 0; px < W; px++)
            {
                float n = Mathf.PerlinNoise(px * 0.02f, py * 0.02f);
                Color c = new Color(0.78f, 0.75f, 0.66f) * (0.82f + n * 0.2f);
                // Rio y calles tenues
                if (Mathf.Abs(py - (300 + Mathf.Sin(px * 0.018f) * 60f)) < 10f) c = Color.Lerp(c, new Color(0.45f, 0.55f, 0.6f), 0.5f);
                if (px % 64 < 2 || py % 64 < 2) c *= 0.93f;
                Vector2 p = new Vector2(px, py);
                for (int li = 0; li < lineas.Length; li++)
                {
                    var (lc, pts) = lineas[li];
                    for (int s = 0; s + 1 < pts.Length; s++)
                    {
                        float d = DistSeg(p, pts[s], pts[s + 1]);
                        bool cortada = li == 3 && s > 0 && ((int)((p - pts[s]).magnitude / 14f)) % 2 == 1; // linea roja: en construccion
                        if (d < 7f && !cortada) c = lc * 0.85f;
                    }
                    foreach (var e in pts)
                    {
                        if (e == centro) continue;
                        float d = (p - e).magnitude;
                        if (d < 11f) c = d < 6.5f ? new Color(0.95f, 0.93f, 0.88f) : new Color(0.1f, 0.1f, 0.1f);
                    }
                }
                float dc = (p - centro).magnitude;
                if (dc < 20f) c = dc < 13f ? new Color(0.95f, 0.93f, 0.88f) : new Color(0.1f, 0.1f, 0.1f);
                // Envejecido: manchas y esquina doblada
                float mancha = Mathf.Clamp01((Mathf.PerlinNoise(px * 0.008f + 5, py * 0.008f) - 0.6f) * 3f);
                c = Color.Lerp(c, new Color(0.35f, 0.3f, 0.2f), mancha * 0.6f);
                if (px + (Ht - py) < 60) c = new Color(0.3f, 0.28f, 0.24f);
                if (px < 8 || py < 8 || px > W - 9 || py > Ht - 9) c = new Color(0.12f, 0.12f, 0.12f);
                col[py * W + px] = new Color(c.r, c.g, c.b, 1f);
            }
        GuardarPng(rc, W, Ht, col, false, false);
    }

    // Cartel viejo de propaganda: papel, una figura grande, bandas de texto ilegible, partes arrancadas (alfa)
    static void GenerarCartel(string rc, int i)
    {
        const int W = 360, Ht = 512;
        var col = new Color[W * Ht];
        var rnd = new System.Random(20 + i);
        Color[] tintes = { new Color(0.55f, 0.12f, 0.1f), new Color(0.1f, 0.25f, 0.45f), new Color(0.6f, 0.45f, 0.1f), new Color(0.2f, 0.35f, 0.2f), new Color(0.35f, 0.15f, 0.4f) };
        Color tinte = tintes[i % tintes.Length];
        float cx = W * (0.3f + (float)rnd.NextDouble() * 0.4f), cy = Ht * (0.45f + (float)rnd.NextDouble() * 0.2f), rad = 70f + (float)rnd.NextDouble() * 50f;
        float corte = 0.3f + (float)rnd.NextDouble() * 0.4f;
        for (int py = 0; py < Ht; py++)
            for (int px = 0; px < W; px++)
            {
                float n = Mathf.PerlinNoise(px * 0.03f + i * 10, py * 0.03f);
                Color c = new Color(0.72f, 0.68f, 0.58f) * (0.8f + n * 0.25f);
                if (py > Ht * 0.78f) c = Color.Lerp(c, tinte, 0.85f);
                if (((px - cx) * (px - cx) + (py - cy) * (py - cy)) < rad * rad) c = Color.Lerp(c, tinte * 0.6f, 0.9f);
                if (py < Ht * 0.25f && py > Ht * 0.08f && (py / 14) % 2 == 0 && px > 30 && px < W - 30 - ((py / 28) % 3) * 40) c *= 0.35f;
                float mancha = Mathf.Clamp01((Mathf.PerlinNoise(px * 0.01f + i * 3, py * 0.01f + 7) - 0.55f) * 3f);
                c = Color.Lerp(c, new Color(0.25f, 0.22f, 0.15f), mancha * 0.7f);
                // Arrancado: borde irregular en diagonal desde una esquina
                float borde = (px / (float)W) + (py / (float)Ht) * 0.6f + (Mathf.PerlinNoise(px * 0.05f, py * 0.05f + i) - 0.5f) * 0.25f;
                float a = (i % 2 == 0 ? borde < corte + 0.9f : borde > corte - 0.2f) ? 1f : 0f;
                if (px < 4 || py < 4 || px > W - 5 || py > Ht - 5) a = 1f * a;
                col[py * W + px] = new Color(c.r, c.g, c.b, a);
            }
        GuardarPng(rc, W, Ht, col, false, false, true);
    }

    // ------------------------------------------------------------------ Utilidades
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(float a, float b) => Frac(Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f);
    // Ruido que se repite cada "periodo" metros en u (para que la textura no tenga costura horizontal)
    static float Ruido(float u, float v, float periodo, float fu, float fv)
    {
        float w = u / periodo;
        float a = Mathf.PerlinNoise(u * fu, v * fv), b = Mathf.PerlinNoise((u - periodo) * fu, v * fv);
        return Mathf.Lerp(a, b, w);
    }
    static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    static Color[] Normales(float[] alt, int W, int H, float fuerza)
    {
        var nor = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float hl = alt[y * W + (x + W - 1) % W], hr = alt[y * W + (x + 1) % W];
                float hd = alt[Mathf.Max(0, y - 1) * W + x], hu = alt[Mathf.Min(H - 1, y + 1) * W + x];
                Vector3 n = new Vector3((hl - hr) * fuerza, (hd - hu) * fuerza, 1f).normalized;
                nor[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        return nor;
    }

    static void GuardarPng(string ruta, int w, int h, Color[] px, bool normal, bool repetir, bool alfa = false)
    {
        var t = new Texture2D(w, h, alfa ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, normal);
        t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(ruta, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.wrapMode = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = alfa;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
    }

    // Quad de ancho x alto metros con el pivote abajo al centro, mirando a -Z (visible desde -Z),
    // con UV en metros (se repite cada repU x repV metros)
    static Mesh Quad(float ancho, float alto, float repU, float repV, bool pivoteAbajo)
    {
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Quad_{0:0.00}x{1:0.00}_{2:0.0}_{3:0.0}{4}", ancho, alto, repU, repV, pivoteAbajo ? "" : "_c");
        string ruta = $"{Carpeta}/Mallas/{clave}.asset";
        var ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (ya) return ya;
        float y0 = pivoteAbajo ? 0f : -alto * 0.5f, y1 = y0 + alto;
        var m = new Mesh { name = clave };
        m.vertices = new[] { new Vector3(-ancho * 0.5f, y0, 0), new Vector3(-ancho * 0.5f, y1, 0), new Vector3(ancho * 0.5f, y1, 0), new Vector3(ancho * 0.5f, y0, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, alto / repV), new Vector2(ancho / repU, alto / repV), new Vector2(ancho / repU, 0) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.RecalculateTangents(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material Mat(string nombre)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre }; AssetDatabase.CreateAsset(m, ruta); }
        return m;
    }

    static Material MatColor(string nombre, Color c, float brillo, float metal = 0f)
    {
        var m = Mat(nombre);
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", brillo); m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emisivo(string nombre, Color c, float intensidad)
    {
        var m = MatColor(nombre, c, 0.5f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensidad);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Caja(Transform r, string n, Vector3 p, Vector3 s, Material m, Quaternion? rot = null) => Prim(PrimitiveType.Cube, r, n, p, s, m, rot);
    static GameObject Cilindro(Transform r, string n, Vector3 p, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, r, n, p, s, m, null);

    static GameObject Prim(PrimitiveType t, Transform r, string n, Vector3 p, Vector3 s, Material m, Quaternion? rot)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = n; g.transform.SetParent(r, true);
        g.transform.SetPositionAndRotation(p, rot ?? Quaternion.identity);
        g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void Texto(Transform r, string texto, Vector3 p, Quaternion rot, float tam, Color c, float ancho, float alto, Color? fondo)
    {
        var go = new GameObject("Texto " + texto.Replace('\n', ' '));
        go.transform.SetParent(r, true);
        // TextMeshPro se lee desde -Z de su transform: se gira para que mire hacia afuera de la pared
        go.transform.SetPositionAndRotation(p, rot);
        if (fondo.HasValue)
            Caja(go.transform, "Fondo", p - (rot * Vector3.forward) * -0.005f, new Vector3(ancho + 0.1f, alto + 0.1f, 0.01f), MatColor("Estacion_TextoFondo", fondo.Value, 0.3f), go.transform.rotation);
        var t = go.AddComponent<TextMeshPro>();
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Frisky SDF.asset");
        if (fuente) t.font = fuente;
        t.text = texto; t.fontSize = tam; t.color = c; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(ancho, alto);
        t.characterSpacing = 6f;
    }

    static Bounds Limites(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero);
        foreach (var x in rs) b.Encapsulate(x.bounds);
        return b;
    }
}
