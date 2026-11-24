using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Interior del faro de la Zona Roja: el refugio de los chicos. El faro esta abandonado, asi que todo quedo tirado
// como lo dejaron despues de los 60 dias que vivieron ahi:
//  - Piso 1: la puerta medio atrancada (barricada desarmada), los dias contados con rayitas en la pared, ropa
//    tendida, cajones de provisiones vacios bajo la escalera, bolsas de basura, el rincon donde cocinaban, latas.
//  - Piso 2: donde dormian los tres (el colchon de Diego apartado bajo la escalera), dibujos de la criatura en el
//    muro, mochilas, ropa, una lampara colgando y la brujula en su pedestal (BrujulaSetup.LugarPiso2).
//  - Piso 3: el ultimo durmio pegado a la luz (colchon, velas, latas, linterna).
// Deja libres la escalera de caracol, el paso de la puerta del piso 1, la mesa de la bitacora, el hueco de la escalera,
// la brujula, la luz y la entrada del piso 3 (Libre). Todo en la capa y el rendering layer del faro (el foco no lo
// ilumina por dentro). Se aplica sola una vez y saca una captura a Temp/Capturas/interior_faro.png (con luz de apoyo).
// Rehacer: Tools > Zona Roja > Interior faro > Rehacer. Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class InteriorFaroSetup
{
    const string Marca = "_interior faro v3";
    const string Nombre = "Interior (refugio de los chicos)";
    static readonly string[] Viejos =
    {
        "_interior faro v1", "_interior faro v2",
        "Interior - Idea 1 (refugio de los chicos)", "Interior - Idea 2 (casa del farero)", "Interior - Idea 3 (asediado)",
    };
    const string CarpetaMat = "Assets/ZonaRoja/Materiales/InteriorFaro";
    const string Capturas = "Temp/Capturas";
    const string RutaLampara = "Assets/Models/Props/lampara vieja.obj";
    // Alturas de los suelos (locales al faro: el piso 3 esta en 0) y techo de los pisos 1 y 2.
    const float Y1 = -20f, Y2 = -10f, Y3 = 0f, Techo = 9.7f;
    const int BitRenderingFaro = 7;

    static InteriorFaroSetup()
    {
        // Despues de los setups de la Zona Roja y de la brujula (que la mueve al piso 2).
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static Transform Faro()
    {
        GameObject zr = GameObject.Find("Zona Roja");
        return zr ? zr.transform.Find("Faro") : null;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform faro = Faro();
        if (!faro) return;
        string pedido = Capturas + "/interior_pedido.txt"; // pedir otra captura sin rehacer nada
        if (faro.Find(Marca))
        {
            if (File.Exists(pedido)) { File.Delete(pedido); Capturar(faro); }
            return;
        }
        Construir(faro);
        Capturar(faro);
    }

    [MenuItem("Tools/Zona Roja/Interior faro/Rehacer")]
    static void Rehacer()
    {
        Transform faro = Faro();
        if (faro) Construir(faro);
    }

    [MenuItem("Tools/Zona Roja/Interior faro/Captura")]
    static void MenuCaptura()
    {
        Transform faro = Faro();
        if (faro) Capturar(faro);
    }

    // ------------------------------------------------------------------ Construir

    static Transform raiz;      // el piso que se esta armando
    static int piso;
    static bool hayMesa;
    static Vector3 mesaLocal;
    static int saltados;
    static System.Random rnd;

    static void Construir(Transform faro)
    {
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Interior del faro");

        foreach (string n in Viejos)
        {
            Transform viejo = faro.Find(n);
            if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        }
        Transform anterior = faro.Find(Nombre);
        if (anterior) Undo.DestroyObjectImmediate(anterior.gameObject);
        if (!faro.Find(Marca))
        {
            var marca = new GameObject(Marca);
            Undo.RegisterCreatedObjectUndo(marca, "Interior del faro");
            marca.transform.SetParent(faro, false);
        }

        Transform mesa = faro.parent ? faro.parent.Find("Mesa del diario") : null;
        hayMesa = mesa;
        if (mesa) mesaLocal = faro.InverseTransformPoint(mesa.position);
        CargarMateriales();
        saltados = 0;
        rnd = new System.Random(17);

        var g = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(g, "Interior del faro");
        g.transform.SetParent(faro, false);
        Piso1(Piso(g.transform, 1));
        Piso2(Piso(g.transform, 2));
        Piso3(Piso(g.transform, 3));

        // Capa y rendering layer del faro: el foco de la luz no lo ilumina por dentro (como el resto del faro).
        int capa = LayerMask.NameToLayer("Faro");
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
            if (capa >= 0) t.gameObject.layer = capa;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            r.renderingLayerMask = 1u << BitRenderingFaro;

        EditorSceneManager.MarkSceneDirty(faro.gameObject.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Interior faro] Refugio de los chicos armado ({saltados} piezas no se pusieron por estorbar el paso). " +
                  "Guarda la escena (Ctrl+S).", g);
    }

    static Transform Piso(Transform interior, int n)
    {
        var go = new GameObject("Piso " + n);
        go.transform.SetParent(interior, false);
        piso = n;
        raiz = go.transform;
        return raiz;
    }

    static float Suelo => piso == 1 ? Y1 : piso == 2 ? Y2 : Y3;

    // Lo que no puede tapar nada (posicion local al faro, piso actual).
    static bool Libre(Vector3 p)
    {
        float r = new Vector2(p.x, p.z).magnitude;
        float a = Mathf.Repeat(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg, 360f);
        float h = p.y - Suelo;
        bool En(float a0, float a1)
        {
            a0 = Mathf.Repeat(a0, 360f);
            a1 = Mathf.Repeat(a1, 360f);
            return a0 <= a1 ? (a >= a0 && a <= a1) : (a >= a0 || a <= a1);
        }
        // En el camino que va de una puerta al centro
        bool EnPaso(float angPuerta, float ancho)
        {
            Vector3 d = Dir(angPuerta);
            float lat = Mathf.Abs(d.x * p.z - d.z * p.x), avance = d.x * p.x + d.z * p.z;
            return avance > 1.2f && lat < ancho;
        }

        switch (piso)
        {
            case 1:
                if (r > 5.6f && En(245f, 345f) && h < 4f) return false;                     // tramo bajo de la escalera 1->2
                if (EnPaso(180f, 1.3f) && h < 2.6f) return false;                             // de la puerta al centro
                if (hayMesa && new Vector2(p.x - mesaLocal.x, p.z - mesaLocal.z).magnitude < 1.3f && h < 2f) return false;
                break;
            case 2:
                if (r > 5.6f && r < 8.2f && En(84f, 188f) && h < 2.6f) return false;        // hueco por donde llega la escalera
                if (r > 3.1f && r < 6.1f && En(22f, 125f) && h < 3.2f) return false;        // tramo bajo de la escalera 2->3
                if (r > 2.4f && r < 7.4f && En(312f, 48f) && h < 2.6f) return false;        // paso hacia la escalera
                if (r > 5.7f && r < 7.4f && En(186f, 320f) && h < 2.6f) return false;       // pasillo junto al muro
                Vector3 b = BrujulaSetup.LugarPiso2;
                if (new Vector2(p.x - b.x, p.z - b.z).magnitude < 0.75f && h < 1.5f) return false; // la brujula
                break;
            case 3:
                if (r < 2.7f && h < 3.5f) return false;                                      // la luz
                if (r > 3.0f && r < 6.2f && En(220f, 326f)) return false;                   // hueco de la escalera
                if (EnPaso(0f, 1.6f) && h < 2.6f) return false;                               // entrada desde el pasillo
                break;
        }
        return true;
    }

    // ------------------------------------------------------------------ Los tres pisos

    static void Piso1(Transform r)
    {
        // La puerta medio atrancada: la barricada que armaron y despues desarmaron para poder salir
        MesaVolcada(r, P(207f, 6.8f, Y1), TanX(207f));
        for (int i = 0; i < 4; i++) TablaApoyada(r, 215f, -0.5f + i * 0.3f);
        for (int i = 0; i < 7; i++)
            Caja(r, "Tabla suelta", P(R(150f, 230f), R(5.0f, 7.4f), Y1 + 0.02f), new Vector3(0.18f, 0.03f, R(1.2f, 2.2f)), mMaderaVieja,
                 Quaternion.Euler(0f, R(0f, 180f), R(-3f, 3f)));
        SillaTirada(r, P(224f, 6.2f, Y1), 40f, mMadera);
        Silla(r, P(232f, 7.5f, Y1), Mira(232f), mMadera);
        Cajon(r, P(160f, 7.4f, Y1), 15f, 0.55f, false);
        SillaTirada(r, P(166f, 6.3f, Y1), 120f, mMadera);

        // Los dias contados en la pared y la ropa tendida (bajo la escalera, que ahi pasa alta)
        Rayitas(r, 235f, 1.75f, 60);
        Tendedero(r, 120f, 160f, 2.4f, new[] { mRopaA, mMantaB, mRopaB, mMantaA });

        // Bajo la escalera: cajones de provisiones vacios (algunos apilados) y bolsas de basura
        Cajon(r, P(48f, 7.5f, Y1), 10f, 0.6f, true);
        Cajon(r, P(48f, 7.5f, Y1 + 0.6f), 22f, 0.5f, true);
        Cajon(r, P(60f, 7.2f, Y1), 35f, 0.55f, true);
        Cajon(r, P(74f, 7.6f, Y1), 5f, 0.6f, true);
        Cajon(r, P(74f, 7.6f, Y1 + 0.6f), -8f, 0.55f, false);
        Cajon(r, P(88f, 6.8f, Y1), 50f, 0.45f, true);
        CajonTirado(r, P(102f, 6.4f, Y1), 30f, 0.55f);
        CajonTirado(r, P(140f, 7.0f, Y1), -40f, 0.5f);
        Bolsa(r, P(112f, 7.6f, Y1));
        Bolsa(r, P(118f, 7.0f, Y1));
        Bolsa(r, P(125f, 7.7f, Y1));
        Bolsa(r, P(95f, 7.8f, Y1));

        // El rincon donde cocinaban
        Cocina(r, P(300f, 3.4f, Y1));
        Botellas(r, P(300f, 3.4f, Y1), 1.5f, 6);

        // Lo que quedo tirado por todas partes
        Latas(r, new Vector3(0f, Y1, 0f), 5f, 26);
        Velas(r, P(20f, 2.2f, Y1), 5);
        Velas(r, P(60f, 2.0f, Y1), 3);
        Colchon(r, P(70f, 3.8f, Y1), TanZ(70f), mMantaA, true);
        Ropa(r, P(70f, 3.8f, Y1), 1.4f, 5);
        Papeles(r, new Vector3(0f, Y1, 0f), 5f, 22);
        for (int i = 0; i < 3; i++)
            Caja(r, "Manta tirada", P(R(0f, 360f), R(1.5f, 4.5f), Y1 + 0.03f), new Vector3(R(0.8f, 1.2f), 0.05f, R(0.7f, 1.1f)),
                 i % 2 == 0 ? mMantaC : mMantaB, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)));
    }

    static void Piso2(Transform r)
    {
        // Donde dormian los tres: dos colchones juntos y el de Diego apartado bajo la escalera
        Colchon(r, new Vector3(-1.4f, Y2, 0.2f), 8f, mMantaA, false);
        Colchon(r, new Vector3(-0.3f, Y2, 0.4f), -6f, mMantaB, true);
        Colchon(r, P(255f, 4.6f, Y2), TanZ(255f), mMantaC, true);
        Velas(r, P(255f, 3.6f, Y2), 4);

        // Dibujos de la criatura pegados en el muro, junto al rincon de Diego
        for (int i = 0; i < 10; i++) Dibujo(r, 245f + (i / 2) * 10f, i % 2 == 0 ? -0.35f : 0.3f, R(1.35f, 1.95f), R(-7f, 7f));

        // Sus cosas
        Caja(r, "Mochila", new Vector3(-2.3f, Y2 + 0.21f, -0.7f), new Vector3(0.32f, 0.42f, 0.2f), mCuero, Giro(R(0f, 360f)));
        Caja(r, "Mochila tirada", new Vector3(0.9f, Y2 + 0.1f, -1.4f), new Vector3(0.32f, 0.2f, 0.42f), mCuero, Giro(R(0f, 360f)));
        Caja(r, "Mochila", P(250f, 3.7f, Y2 + 0.21f), new Vector3(0.32f, 0.42f, 0.2f), mCuero, Giro(R(0f, 360f)));
        Ropa(r, new Vector3(-0.6f, Y2, -0.6f), 1.8f, 9);
        PilaDeLatas(r, new Vector3(-2.2f, Y2, 1.3f));
        Latas(r, new Vector3(-0.5f, Y2, 0f), 2.6f, 16);
        Botellas(r, new Vector3(-1.2f, Y2, -1.5f), 1.0f, 4);
        Papeles(r, new Vector3(0f, Y2, 0f), 2.9f, 12);
        Soga(r, new Vector3(0.6f, 0f, -1.2f), 3.6f);
        Lampara(r, new Vector3(0.6f, Y2 + Techo - 3.6f - 0.4f, -1.2f), 0.4f);

        // Provisiones vacias contra el muro
        Cajon(r, P(205f, 7.95f, Y2), 0f, 0.55f, true);
        Cajon(r, P(214f, 7.95f, Y2), 10f, 0.55f, true);
        Cajon(r, P(209f, 7.95f, Y2 + 0.55f), 25f, 0.5f, true);
        Cajon(r, P(296f, 7.9f, Y2), -15f, 0.55f, true);
        Cajon(r, P(305f, 7.95f, Y2), 5f, 0.5f, true);
        Bolsa(r, P(222f, 7.8f, Y2));
        Bolsa(r, P(288f, 7.8f, Y2));
    }

    static void Piso3(Transform r)
    {
        // El ultimo durmio pegado a la luz
        Colchon(r, P(120f, 3.9f, Y3), TanZ(120f), mMantaB, true);
        Caja(r, "Manta tirada", P(150f, 4.3f, Y3 + 0.03f), new Vector3(1.1f, 0.05f, 0.9f), mMantaC, Giro(35f));
        Cajon(r, P(150f, 4.6f, Y3), 20f, 0.45f, false);
        Velas(r, P(150f, 4.6f, Y3 + 0.45f), 2);
        Velas(r, P(95f, 3.4f, Y3), 5);
        Latas(r, P(120f, 3.9f, Y3), 1.6f, 12);
        Botellas(r, P(100f, 4.4f, Y3), 0.8f, 3);
        Ropa(r, P(120f, 3.9f, Y3), 1.2f, 4);
        Caja(r, "Mochila", P(145f, 3.6f, Y3 + 0.21f), new Vector3(0.32f, 0.42f, 0.2f), mCuero, Giro(30f));
        Papeles(r, P(120f, 4f, Y3), 1.8f, 10);
        Linterna(r, P(105f, 3.3f, Y3), P(105f, 8f, Y3));
    }

    // ------------------------------------------------------------------ Piezas

    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    static Vector3 Dir(float a) { float r = a * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r)); }
    static Vector3 P(float a, float r, float y) => Dir(a) * r + Vector3.up * y;
    static Vector3 Tangente(float a) => Vector3.Cross(Vector3.up, Dir(a));
    static Quaternion Mira(float a) => Quaternion.LookRotation(-Dir(a));   // +Z hacia el centro
    static Quaternion Giro(float y) => Quaternion.Euler(0f, y, 0f);
    static float TanX(float a) => -a - 90f;  // giro que deja el eje X a lo largo del muro
    static float TanZ(float a) => -a;        // giro que deja el eje Z a lo largo del muro
    static float Bloque(float a) => Mathf.Round((a - 5f) / 10f) * 10f + 5f; // centro del bloque de muro (36 bloques)

    static GameObject Prim(PrimitiveType tipo, Transform padre, string n, Vector3 pos, Quaternion rot, Vector3 esc, Material m, bool col)
    {
        if (padre == raiz && !Libre(pos)) { saltados++; return null; }
        var go = GameObject.CreatePrimitive(tipo);
        go.name = n;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = esc;
        if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Caja(Transform padre, string n, Vector3 pos, Vector3 esc, Material m, Quaternion rot, bool col = false) =>
        Prim(PrimitiveType.Cube, padre, n, pos, rot, esc, m, col);
    static GameObject Caja(Transform padre, string n, Vector3 pos, Vector3 esc, Material m) =>
        Prim(PrimitiveType.Cube, padre, n, pos, Quaternion.identity, esc, m, false);
    static GameObject Cil(Transform padre, string n, Vector3 pos, float radio, float alto, Material m, Quaternion rot, bool col = false) =>
        Prim(PrimitiveType.Cylinder, padre, n, pos, rot, new Vector3(radio * 2f, alto * 0.5f, radio * 2f), m, col);

    static Transform Grupo(Transform padre, string n, Vector3 pos, Quaternion rot)
    {
        if (padre == raiz && !Libre(pos)) { saltados++; return null; }
        var go = new GameObject(n);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        return go.transform;
    }

    // Algo plano pegado al muro, centrado en el bloque de muro mas cercano a "a" y corrido "lado" metros a lo largo
    // del muro; "alto" es la altura del centro sobre el suelo del piso. sep = separacion extra del muro.
    static GameObject EnMuro(Transform padre, string n, float a, float lado, float alto, Vector2 tam, float grosor, Material m,
                             float giroZ = 0f, float sep = 0f)
    {
        float ab = Bloque(a);
        Vector3 pos = Dir(ab) * (8.4f - grosor * 0.5f - 0.01f - sep) + Tangente(ab) * lado + Vector3.up * (Suelo + alto);
        return Caja(padre, n, pos, new Vector3(tam.x, tam.y, grosor), m, Mira(ab) * Quaternion.Euler(0f, 0f, giroZ));
    }

    static void Silla(Transform padre, Vector3 pos, Quaternion rot, Material m)
    {
        Transform s = Grupo(padre, "Silla", pos, rot);
        if (!s) return;
        Caja(s, "Asiento", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.04f, 0.45f), m);
        foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
                Caja(s, "Pata", new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), m);
        Caja(s, "Respaldo", new Vector3(0f, 0.75f, -0.21f), new Vector3(0.45f, 0.55f, 0.04f), m);
    }

    // Silla tirada de costado
    static void SillaTirada(Transform padre, Vector3 suelo, float giro, Material m) =>
        Silla(padre, suelo + Vector3.up * 0.23f, Giro(giro) * Quaternion.Euler(0f, 0f, 90f), m);

    // Mesa tirada: el tablero queda de pie y las patas hacia un lado
    static void MesaVolcada(Transform padre, Vector3 suelo, float giro)
    {
        Transform t = Grupo(padre, "Mesa volcada", suelo + Vector3.up * 0.37f, Giro(giro) * Quaternion.Euler(90f, 0f, 0f));
        if (!t) return;
        Caja(t, "Tablero", new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.05f, 0.7f), mMadera, Quaternion.identity, true);
        foreach (float x in new[] { -0.52f, 0.52f })
            foreach (float z in new[] { -0.28f, 0.28f })
                Caja(t, "Pata", new Vector3(x, 0.36f, z), new Vector3(0.06f, 0.72f, 0.06f), mMadera);
    }

    static void TablaApoyada(Transform padre, float a, float lado, float largo = 2.2f)
    {
        float ab = Bloque(a), inc = R(10f, 18f);
        Vector3 pos = Dir(ab) * (8.38f - Mathf.Sin(inc * Mathf.Deg2Rad) * largo * 0.5f - 0.02f) + Tangente(ab) * lado +
                      Vector3.up * (Suelo + Mathf.Cos(inc * Mathf.Deg2Rad) * largo * 0.5f);
        Caja(padre, "Tabla apoyada", pos, new Vector3(R(0.16f, 0.24f), largo, 0.03f), mMaderaVieja, Mira(ab) * Quaternion.Euler(-inc, 0f, R(-4f, 4f)));
    }

    static void Cajon(Transform padre, Vector3 suelo, float giro, float lado, bool abierto)
    {
        Transform t = Grupo(padre, "Cajon vacio", suelo, Giro(giro));
        if (!t) return;
        ArmarCajon(t, lado, abierto);
    }

    // Cajon tumbado de costado, con la boca hacia un lado
    static void CajonTirado(Transform padre, Vector3 suelo, float giro, float lado)
    {
        Transform t = Grupo(padre, "Cajon tirado", suelo + Vector3.up * lado * 0.4f, Giro(giro) * Quaternion.Euler(90f, 0f, 0f));
        if (!t) return;
        t.localPosition += t.localRotation * (Vector3.down * lado * 0.5f);
        ArmarCajon(t, lado, true);
    }

    static void ArmarCajon(Transform t, float lado, bool abierto)
    {
        Caja(t, "Cuerpo", new Vector3(0f, lado * 0.5f, 0f), new Vector3(lado, lado, lado * 0.8f), mMaderaVieja, Quaternion.identity, true);
        if (abierto) Caja(t, "Vacio", new Vector3(0f, lado + 0.002f, 0f), new Vector3(lado * 0.88f, 0.004f, lado * 0.68f), mHueco);
        Caja(t, "Liston", new Vector3(0f, lado * 0.5f, lado * 0.4f + 0.01f), new Vector3(lado * 1.02f, 0.06f, 0.02f), mMadera);
        Caja(t, "Liston", new Vector3(0f, lado * 0.5f, -lado * 0.4f - 0.01f), new Vector3(lado * 1.02f, 0.06f, 0.02f), mMadera);
    }

    static void Colchon(Transform padre, Vector3 suelo, float giro, Material manta, bool revuelto)
    {
        Transform t = Grupo(padre, "Colchon", suelo, Giro(giro));
        if (!t) return;
        Caja(t, "Colchon", new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.16f, 1.9f), mColchon);
        Caja(t, "Almohada", new Vector3(0f, 0.2f, 0.72f), new Vector3(0.55f, 0.1f, 0.3f), mSabana, Giro(R(-12f, 12f)));
        if (revuelto)
        {
            Caja(t, "Manta", new Vector3(R(-0.3f, 0.3f), 0.19f, -0.2f), new Vector3(1.0f, 0.05f, 0.9f), manta, Quaternion.Euler(R(-6f, 6f), R(-35f, 35f), R(-8f, 8f)));
            Caja(t, "Manta caida", new Vector3(0.65f, 0.05f, -0.6f), new Vector3(0.7f, 0.08f, 0.6f), manta, Quaternion.Euler(0f, R(0f, 90f), R(5f, 15f)));
        }
        else Caja(t, "Manta", new Vector3(0f, 0.18f, -0.25f), new Vector3(0.95f, 0.04f, 1.2f), manta, Giro(R(-5f, 5f)));
    }

    static void Latas(Transform padre, Vector3 centro, float radio, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 p = centro + Dir(R(0f, 360f)) * R(0f, radio);
            if (rnd.NextDouble() < 0.6)
                Cil(padre, "Lata vacia", p + Vector3.up * 0.035f, 0.035f, 0.11f, mLata, Quaternion.Euler(90f, R(0f, 360f), 0f));
            else
                Cil(padre, "Lata vacia", p + Vector3.up * 0.055f, 0.035f, 0.11f, mLata, Quaternion.identity);
        }
    }

    // Latas apiladas en piramide (4, 3, 2, 1)
    static void PilaDeLatas(Transform padre, Vector3 suelo)
    {
        Transform t = Grupo(padre, "Pila de latas", suelo, Giro(R(0f, 360f)));
        if (!t) return;
        for (int fila = 0; fila < 4; fila++)
            for (int k = 0; k < 4 - fila; k++)
                Cil(t, "Lata vacia", new Vector3((k - (3 - fila) * 0.5f) * 0.075f, 0.055f + fila * 0.11f, 0f), 0.035f, 0.11f, mLata, Quaternion.identity);
    }

    static void Velas(Transform padre, Vector3 centro, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 p = centro + Dir(R(0f, 360f)) * R(0.05f, 0.45f);
            float h = R(0.03f, 0.14f);
            Cil(padre, "Vela derretida", p + Vector3.up * h * 0.5f, 0.025f, h, mCera, Quaternion.identity);
            Cil(padre, "Cera", p + Vector3.up * 0.003f, R(0.04f, 0.08f), 0.006f, mCera, Quaternion.identity);
        }
    }

    static void Papeles(Transform padre, Vector3 centro, float radio, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 p = centro + Dir(R(0f, 360f)) * R(0f, radio) + Vector3.up * 0.004f;
            Caja(padre, "Hoja", p, new Vector3(0.21f, 0.003f, 0.3f), rnd.NextDouble() < 0.5 ? mPapel : mPapelViejo, Quaternion.Euler(R(-3f, 3f), R(0f, 360f), R(-3f, 3f)));
        }
    }

    static void Ropa(Transform padre, Vector3 centro, float radio, int n)
    {
        Material[] ms = { mRopaA, mRopaB, mMantaA, mMantaC };
        for (int i = 0; i < n; i++)
        {
            Vector3 p = centro + Dir(R(0f, 360f)) * R(0f, radio) + Vector3.up * 0.02f;
            Caja(padre, "Ropa tirada", p, new Vector3(R(0.3f, 0.5f), 0.04f, R(0.4f, 0.6f)), ms[rnd.Next(ms.Length)], Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)));
        }
    }

    static void Botellas(Transform padre, Vector3 centro, float radio, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 p = centro + Dir(R(0f, 360f)) * R(0f, radio);
            bool tumbada = rnd.NextDouble() < 0.5;
            Transform t = Grupo(padre, "Botella vacia", p + Vector3.up * (tumbada ? 0.04f : 0f),
                                tumbada ? Giro(R(0f, 360f)) * Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
            if (!t) continue;
            Cil(t, "Cuerpo", new Vector3(0f, 0.1f, 0f), 0.038f, 0.2f, mBotella, Quaternion.identity);
            Cil(t, "Cuello", new Vector3(0f, 0.24f, 0f), 0.014f, 0.09f, mBotella, Quaternion.identity);
        }
    }

    static void Bolsa(Transform padre, Vector3 suelo)
    {
        Transform t = Grupo(padre, "Bolsa de basura", suelo, Giro(R(0f, 360f)));
        if (!t) return;
        Prim(PrimitiveType.Sphere, t, "Bolsa", new Vector3(0f, 0.22f, 0f), Quaternion.Euler(R(-8f, 8f), 0f, R(-8f, 8f)), new Vector3(0.55f, 0.45f, 0.5f), mBolsa, false);
        Prim(PrimitiveType.Sphere, t, "Nudo", new Vector3(0.02f, 0.46f, 0f), Quaternion.identity, new Vector3(0.1f, 0.09f, 0.1f), mBolsa, false);
    }

    // Donde cocinaban: el suelo quemado, ladrillos, una olla y latas abiertas
    static void Cocina(Transform padre, Vector3 suelo)
    {
        Transform t = Grupo(padre, "Rincon donde cocinaban", suelo, Giro(R(0f, 360f)));
        if (!t) return;
        Cil(t, "Suelo quemado", new Vector3(0f, 0.004f, 0f), 0.55f, 0.006f, mHueco, Quaternion.identity);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Caja(t, "Ladrillo", Dir(a) * 0.2f + Vector3.up * 0.06f, new Vector3(0.22f, 0.12f, 0.1f), mLadrillo, Giro(-a + 90f));
        }
        Cil(t, "Olla", new Vector3(0f, 0.2f, 0f), 0.14f, 0.16f, mLata, Quaternion.identity);
        Cil(t, "Tapa de la olla", new Vector3(0.35f, 0.01f, 0.2f), 0.15f, 0.01f, mLata, Quaternion.Euler(0f, 0f, 4f));
        Caja(t, "Ceniza", new Vector3(0.05f, 0.012f, -0.05f), new Vector3(0.3f, 0.015f, 0.25f), mCeniza, Giro(30f));
    }

    // 60 rayitas (de a 5, la quinta cruzada): las ultimas cada vez mas torcidas
    static void Rayitas(Transform padre, float a, float alto, int total)
    {
        int grupos = total / 5;
        for (int g = 0; g < grupos; g++)
        {
            int fila = g / 4, col = g % 4;
            float x0 = -0.62f + col * 0.36f, y0 = alto - fila * 0.38f, caos = g / (float)grupos;
            for (int k = 0; k < 4; k++)
                EnMuro(padre, "Rayita", a, x0 + k * 0.055f + R(-0.01f, 0.01f) * (1f + 4f * caos), y0 + R(-0.02f, 0.02f) * (1f + 3f * caos),
                       new Vector2(0.018f, 0.26f + R(-0.03f, 0.05f) * caos), 0.008f, mTinta, R(-4f, 4f) * (1f + 5f * caos));
            EnMuro(padre, "Rayita cruzada", a, x0 + 0.08f, y0, new Vector2(0.018f, 0.32f), 0.008f, mTinta, 62f + R(-8f, 8f) * (1f + 3f * caos), 0.004f);
        }
    }

    // Dibujo de la criatura: alta, flaca y con brazos que casi llegan al suelo
    static void Dibujo(Transform padre, float a, float lado, float alto, float giro)
    {
        EnMuro(padre, "Dibujo de la silueta", a, lado, alto, new Vector2(0.3f, 0.4f), 0.004f, mPapel, giro);
        EnMuro(padre, "Silueta", a, lado, alto + 0.02f, new Vector2(0.025f, 0.3f), 0.004f, mTinta, giro, 0.004f);
        EnMuro(padre, "Cabeza", a, lado, alto + 0.17f, new Vector2(0.04f, 0.04f), 0.004f, mTinta, giro, 0.004f);
        EnMuro(padre, "Brazo", a, lado - 0.035f, alto - 0.01f, new Vector2(0.012f, 0.28f), 0.004f, mTinta, giro - 8f, 0.004f);
        EnMuro(padre, "Brazo", a, lado + 0.035f, alto - 0.01f, new Vector2(0.012f, 0.28f), 0.004f, mTinta, giro + 8f, 0.004f);
    }

    static void Tendedero(Transform padre, float a0, float a1, float alto, Material[] ropas)
    {
        Vector3 p0 = P(a0, 8.3f, Suelo + alto), p1 = P(a1, 8.3f, Suelo + alto);
        Quaternion dir = Quaternion.LookRotation(p1 - p0);
        Caja(padre, "Cuerda de tender", (p0 + p1) * 0.5f, new Vector3(0.015f, 0.015f, Vector3.Distance(p0, p1)), mCuerda, dir);
        for (int i = 0; i < ropas.Length; i++)
        {
            Vector3 p = Vector3.Lerp(p0, p1, (i + 1f) / (ropas.Length + 1f));
            float h = R(0.5f, 0.9f);
            Caja(padre, "Ropa colgada", p + Vector3.down * h * 0.5f, new Vector3(R(0.4f, 0.6f), h, 0.015f), ropas[i], dir * Quaternion.Euler(0f, 90f, R(-4f, 4f)));
        }
    }

    // Soga que cuelga del techo (xz = posicion; se ignora y)
    static void Soga(Transform padre, Vector3 xz, float largo)
    {
        Cil(padre, "Soga colgando", new Vector3(xz.x, Suelo + Techo - largo * 0.5f, xz.z), 0.02f, largo, mCuerda, Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f)));
    }

    static void Lampara(Transform padre, Vector3 pos, float alto)
    {
        if (padre == raiz && !Libre(pos)) { saltados++; return; }
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(RutaLampara);
        if (!src)
        {
            Cil(padre, "Lampara de aceite (apagada)", pos + Vector3.up * alto * 0.5f, 0.08f, alto, mLata, Quaternion.identity);
            return;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, padre);
        go.name = "Lampara de aceite (apagada)";
        go.transform.localPosition = pos;
        go.transform.localRotation = Giro(R(0f, 360f));
        go.transform.localScale = Vector3.one;
        Bounds b = Limites(go);
        if (b.size.y > 1e-4f) go.transform.localScale = Vector3.one * (alto / b.size.y);
        b = Limites(go);
        Vector3 objetivo = padre.TransformPoint(pos);
        go.transform.position += new Vector3(objetivo.x - b.center.x, objetivo.y - b.min.y, objetivo.z - b.center.z);
    }

    static Bounds Limites(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Linterna(Transform padre, Vector3 pos, Vector3 mirar)
    {
        Vector3 d = mirar - pos;
        d.y = 0f;
        Transform t = Grupo(padre, "Linterna (apagada)", pos, Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward));
        if (!t) return;
        Cil(t, "Cuerpo", new Vector3(0f, 0.035f, 0f), 0.032f, 0.22f, mNegro, Quaternion.Euler(90f, 0f, 0f));
        Cil(t, "Cabeza", new Vector3(0f, 0.048f, 0.13f), 0.048f, 0.07f, mNegro, Quaternion.Euler(90f, 0f, 0f));
        Cil(t, "Vidrio", new Vector3(0f, 0.048f, 0.166f), 0.04f, 0.004f, mCristal, Quaternion.Euler(90f, 0f, 0f));
    }

    // ------------------------------------------------------------------ Materiales

    static Material mMadera, mMaderaVieja, mPapel, mPapelViejo, mCuero, mHueco, mLata, mCera, mColchon, mMantaA, mMantaB, mMantaC,
                    mRopaA, mRopaB, mSabana, mCristal, mBotella, mTinta, mCuerda, mNegro, mBolsa, mLadrillo, mCeniza;

    static void CargarMateriales()
    {
        mMadera = Cargar("Assets/Guion/Madera.mat", "Madera", new Color(0.4f, 0.27f, 0.16f));
        mMaderaVieja = Cargar("Assets/Guion/MaderaVieja.mat", "MaderaVieja", new Color(0.3f, 0.24f, 0.18f));
        mPapel = Cargar("Assets/Guion/Papel.mat", "Papel", new Color(0.8f, 0.78f, 0.7f));
        mPapelViejo = Cargar("Assets/Guion/PapelViejo.mat", "PapelViejo", new Color(0.62f, 0.56f, 0.42f));
        mCuero = Cargar("Assets/Guion/Cuero.mat", "Cuero", new Color(0.3f, 0.18f, 0.1f));
        mHueco = Cargar("Assets/Guion/HuecoNegro.mat", "HuecoNegro", new Color(0.02f, 0.02f, 0.02f));
        mLata = Mat("Lata", new Color(0.45f, 0.45f, 0.43f), 0.3f, 0.8f);
        mCera = Mat("Cera", new Color(0.85f, 0.82f, 0.7f));
        mColchon = Mat("Colchon", new Color(0.55f, 0.5f, 0.4f));
        mMantaA = Mat("MantaVerde", new Color(0.2f, 0.27f, 0.18f));
        mMantaB = Mat("MantaRoja", new Color(0.4f, 0.12f, 0.1f));
        mMantaC = Mat("MantaGris", new Color(0.32f, 0.32f, 0.35f));
        mRopaA = Mat("RopaAzul", new Color(0.15f, 0.2f, 0.35f));
        mRopaB = Mat("RopaAmarilla", new Color(0.55f, 0.45f, 0.18f));
        mSabana = Mat("SabanaSucia", new Color(0.62f, 0.6f, 0.54f));
        mCristal = Mat("CristalRoto", new Color(0.55f, 0.65f, 0.65f), 0.8f);
        mBotella = Mat("BotellaVerde", new Color(0.1f, 0.25f, 0.12f), 0.6f);
        mTinta = Mat("Tinta", new Color(0.05f, 0.04f, 0.04f));
        mCuerda = Mat("Cuerda", new Color(0.45f, 0.38f, 0.25f));
        mNegro = Mat("PlasticoNegro", new Color(0.04f, 0.04f, 0.04f), 0.2f);
        mBolsa = Mat("BolsaBasura", new Color(0.05f, 0.07f, 0.05f), 0.35f);
        mLadrillo = Mat("Ladrillo", new Color(0.4f, 0.2f, 0.14f));
        mCeniza = Mat("Ceniza", new Color(0.25f, 0.24f, 0.23f));
    }

    static Material Cargar(string ruta, string nombre, Color respaldo) => AssetDatabase.LoadAssetAtPath<Material>(ruta) ?? Mat(nombre, respaldo);

    // Material del interior (Smoothness 0 salvo latas, vidrio y plastico: la linterna se reflejaba)
    static Material Mat(string nombre, Color color, float suavidad = 0f, float metal = 0f)
    {
        string ruta = CarpetaMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/ZonaRoja/Materiales", "InteriorFaro");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", suavidad);
        m.SetFloat("_Metallic", metal);
        if (nuevo) AssetDatabase.CreateAsset(m, ruta);
        else EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ Captura

    // 6 vistas en una imagen (3 x 2), a la altura de los ojos:
    // piso 1 junto a la puerta | piso 1 desde la puerta | piso 1 bajo la escalera
    // piso 2 al llegar (brujula) | piso 2 el rincon de Diego | piso 3 junto a la luz
    static void Capturar(Transform faro)
    {
        const int w = 800, h = 450;
        Vector3 brujula = BrujulaSetup.LugarPiso2;
        var vistas = new (Vector3 pos, Vector3 mirar, float fov, float suelo)[]
        {
            (P(215f, 3.0f, Y1 + 1.7f), P(215f, 8.4f, Y1 + 1.0f), 75f, Y1),
            (P(180f, 5.5f, Y1 + 1.7f), P(0f, 1.0f, Y1), 70f, Y1),
            (P(0f, 1.0f, Y1 + 1.7f), P(95f, 7.2f, Y1 + 0.6f), 75f, Y1),
            (brujula + new Vector3(-2.6f, 1.6f, -0.5f), brujula + new Vector3(0.3f, 0.6f, 0.1f), 70f, Y2),
            (P(250f, 2.0f, Y2 + 1.7f), P(265f, 8.4f, Y2 + 1.4f), 70f, Y2),
            (P(60f, 4.5f, Y3 + 2.2f), P(125f, 4.0f, Y3 + 0.4f), 75f, Y3),
        };
        Directory.CreateDirectory(Capturas);
        bool niebla = RenderSettings.fog;
        RenderSettings.fog = false;
        var hoja = new Texture2D(w * 3, h * 2, TextureFormat.RGB24, false);
        var camGo = new GameObject("_CamaraInteriorFaro") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
        Light l1 = LuzApoyo(6f, 22f), l2 = LuzApoyo(5f, 14f);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var antes = RenderTexture.active;
        try
        {
            for (int i = 0; i < vistas.Length; i++)
            {
                var v = vistas[i];
                cam.transform.position = faro.TransformPoint(v.pos);
                cam.transform.rotation = Quaternion.LookRotation(faro.TransformPoint(v.mirar) - cam.transform.position, faro.up);
                cam.fieldOfView = v.fov;
                l1.transform.position = cam.transform.position;
                l2.transform.position = faro.TransformPoint(new Vector3(0f, v.suelo + 5f, 0f));
                cam.Render();
                RenderTexture.active = rt;
                hoja.ReadPixels(new Rect(0, 0, w, h), (i % 3) * w, (1 - i / 3) * h);
            }
            hoja.Apply();
            File.WriteAllBytes(Capturas + "/interior_faro.png", hoja.EncodeToPNG());
            File.WriteAllText(Capturas + "/interior_listo.txt", System.DateTime.Now.ToString("HH:mm:ss") + " saltados=" + saltados);
        }
        finally
        {
            RenderSettings.fog = niebla;
            RenderTexture.active = antes;
            cam.targetTexture = null;
            Object.DestroyImmediate(hoja);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(l1.gameObject);
            Object.DestroyImmediate(l2.gameObject);
        }
        Debug.Log($"[Interior faro] Captura en {Capturas}/interior_faro.png");
    }

    // Luz solo para la foto: alumbra todos los rendering layers (el faro solo esta en el suyo)
    static Light LuzApoyo(float intensidad, float rango)
    {
        var go = new GameObject("_LuzInteriorFaro") { hideFlags = HideFlags.HideAndDontSave };
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.intensity = intensidad;
        l.range = rango;
        l.shadows = LightShadows.None;
        l.color = new Color(1f, 0.92f, 0.8f);
        l.renderingLayerMask = -1;
        l.GetUniversalAdditionalLightData().renderingLayers = uint.MaxValue;
        return l;
    }
}
