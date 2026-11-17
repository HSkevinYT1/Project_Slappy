using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// Props del laberinto (se construyen junto con el trazado, en "Laberinto/Props"):
// 1. Escombros  2. Raices gruesas  3. Cristales indigo en el piso  4. Charcos oscuros
// 5. Rastros: mochilas y linternas tiradas (con una bateria al lado para recargar la linterna) y zapatos
// 6. Marcas de tiza en las paredes  8. Esqueletos y huesos  9. Estatuas distintas en cada sala de panel
// 10. Arcos de piedra en las entradas de la habitacion central y de las salas de panel.
// Todo va pegado a las paredes o en rincones para no tapar pasillos; ninguno cerca de las paredes elevadas.
public static partial class LaberintoNuevoSetup
{
    const string PrefabBateria = "Assets/Prefabs/Bateria.prefab";

    static float px0, pz0;
    static Vector3 C(int i, int j) => new Vector3(px0 + (i + 0.5f) * Celda, 0f, pz0 + (j + 0.5f) * Celda);

    static void Props(Transform lab, float x0, float z0, List<Vector2Int> atajos)
    {
        px0 = x0; pz0 = z0;
        Transform vieja = lab.Find("Props");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        // Contenedor con transformacion de mundo identidad (el Laberinto esta girado y escalado)
        var raizGo = new GameObject("Props");
        Undo.RegisterCreatedObjectUndo(raizGo, "Props");
        Transform raiz = raizGo.transform;
        raiz.SetParent(lab, false);
        Vector3 ls = lab.lossyScale;
        raiz.rotation = Quaternion.identity;
        raiz.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        raiz.position = Vector3.zero;

        Material piedra = PrimerMaterial(lab.Find("Paredes"));
        var rnd = new System.Random(Semilla + 99);
        var usadas = new List<Vector2Int>();

        // Celdas prohibidas: salas, entrada y alrededor de las paredes elevadas
        var prohibidas = new HashSet<Vector2Int>();
        foreach (Vector2Int a in atajos)
        {
            int i = a.x / 1000, j = a.x % 1000;
            if (a.y == 1) { prohibidas.Add(new Vector2Int(i - 1, j)); prohibidas.Add(new Vector2Int(i, j)); }
            else { prohibidas.Add(new Vector2Int(i, j - 1)); prohibidas.Add(new Vector2Int(i, j)); }
        }

        // Candidatos: (celda de pasillo, direccion hacia una pared que tenga)
        var candidatos = new List<(Vector2Int c, Vector3 d)>();
        for (int i = 0; i < N; i++)
        for (int j = 0; j < N; j++)
        {
            var c = new Vector2Int(i, j);
            if (EnSala(i, j) || prohibidas.Contains(c)) continue;
            if (muroH[i, j]) candidatos.Add((c, Vector3.left));
            if (muroH[i + 1, j]) candidatos.Add((c, Vector3.right));
            if (muroV[i, j]) candidatos.Add((c, Vector3.back));
            if (muroV[i, j + 1]) candidatos.Add((c, Vector3.forward));
        }
        for (int k = candidatos.Count - 1; k > 0; k--) { int r = rnd.Next(k + 1); (candidatos[k], candidatos[r]) = (candidatos[r], candidatos[k]); }

        bool Tomar(out Vector2Int celda, out Vector3 dir, int separacion = 2)
        {
            foreach (var (c, d) in candidatos)
            {
                bool libre = true;
                foreach (Vector2Int u in usadas) if (Mathf.Max(Mathf.Abs(u.x - c.x), Mathf.Abs(u.y - c.y)) < separacion) { libre = false; break; }
                if (!libre) continue;
                usadas.Add(c);
                celda = c; dir = d;
                return true;
            }
            celda = default; dir = Vector3.zero;
            return false;
        }
        // Punto en la cara de la pared (a nivel del piso)
        Vector3 Cara(Vector2Int c, Vector3 d) => C(c.x, c.y) + d * (Celda * 0.5f - Grosor * 0.5f);

        Transform Grupo(string n) { var g = new GameObject(n).transform; g.SetParent(raiz, false); return g; }

        // ---- 10. Arcos y 9. Estatuas (posiciones fijas) ----
        Transform arcos = Grupo("Arcos");
        var puertas = new (Vector2Int c, Vector2Int d)[]
        {
            (new Vector2Int(8, 10), Vector2Int.left), (new Vector2Int(12, 10), Vector2Int.right),
            (new Vector2Int(10, 8), Vector2Int.down), (new Vector2Int(10, 12), Vector2Int.up),
            (new Vector2Int(3, 17), Vector2Int.up), (new Vector2Int(5, 15), Vector2Int.right),
            (new Vector2Int(17, 17), Vector2Int.right), (new Vector2Int(15, 15), Vector2Int.down),
            (new Vector2Int(17, 3), Vector2Int.down), (new Vector2Int(15, 5), Vector2Int.left),
            (new Vector2Int(3, 3), Vector2Int.left), (new Vector2Int(5, 5), Vector2Int.up),
        };
        foreach (var (c, d) in puertas) Arco(arcos, c, d, piedra);

        Transform estatuas = Grupo("Estatuas");
        var rincones = new (Vector2Int c, Vector2 d)[]
        {
            (new Vector2Int(3, 15), new Vector2(-1, -1)), (new Vector2Int(15, 17), new Vector2(-1, 1)),
            (new Vector2Int(17, 5), new Vector2(1, 1)), (new Vector2Int(5, 3), new Vector2(1, -1)),
        };
        for (int k = 0; k < rincones.Length; k++)
        {
            var (c, d) = rincones[k];
            Vector3 p = C(c.x, c.y) + new Vector3(d.x, 0f, d.y) * 1.0f;
            Estatua(estatuas, k, p, Quaternion.LookRotation(new Vector3(-d.x, 0f, -d.y)), piedra);
        }

        // ---- Casilleros (prefab Armario) en esquinas de pasillo: celda con dos paredes en L ----
        Transform cas = lab.Find("casilleros");
        var prefabArm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Armario.prefab");
        if (cas && prefabArm)
        {
            for (int k = cas.childCount - 1; k >= 0; k--)
                if (cas.GetChild(k).name.StartsWith("Armario Esquina")) Undo.DestroyObjectImmediate(cas.GetChild(k).gameObject);
            Vector3 escala = Vector3.one;
            foreach (Transform t in cas) { escala = t.localScale; break; }
            var esquinas = new List<(Vector2Int c, Vector3 pared, Vector3 lado)>();
            for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                var c = new Vector2Int(i, j);
                if (EnSala(i, j) || prohibidas.Contains(c)) continue;
                bool l = muroH[i, j], r = muroH[i + 1, j], b = muroV[i, j], f = muroV[i, j + 1];
                int muros = (l ? 1 : 0) + (r ? 1 : 0) + (b ? 1 : 0) + (f ? 1 : 0);
                if (muros != 2 || (l && r) || (b && f)) continue; // solo esquinas en L
                Vector3 pared = l ? Vector3.left : Vector3.right;   // se apoya en la pared este/oeste
                Vector3 lado = b ? Vector3.back : Vector3.forward;  // pegado hacia la otra pared
                esquinas.Add((c, pared, lado));
            }
            for (int k = esquinas.Count - 1; k > 0; k--) { int r2 = rnd.Next(k + 1); (esquinas[k], esquinas[r2]) = (esquinas[r2], esquinas[k]); }
            int nArm = 0;
            var puestos = new List<Vector2Int>();
            foreach (var (c, pared, lado) in esquinas)
            {
                if (nArm >= 8) break;
                bool lejos = true;
                foreach (Vector2Int u in puestos) if (Mathf.Max(Mathf.Abs(u.x - c.x), Mathf.Abs(u.y - c.y)) < 5) lejos = false;
                foreach (Vector2Int u in usadas) if (u == c) lejos = false;
                if (!lejos) continue;
                puestos.Add(c); usadas.Add(c);
                var arm = (GameObject)PrefabUtility.InstantiatePrefab(prefabArm, cas);
                Undo.RegisterCreatedObjectUndo(arm, "Casillero");
                arm.name = "Armario Esquina " + (++nArm);
                arm.transform.localScale = escala;
                Vector3 frente = -pared;
                arm.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
                Transform salida = null;
                foreach (Transform t in arm.GetComponentsInChildren<Transform>(true)) if (t.name == "Punto Salida") { salida = t; break; }
                if (salida)
                {
                    Vector3 fr = salida.position - arm.transform.position; fr.y = 0f;
                    if (fr.sqrMagnitude > 0.0001f) arm.transform.rotation = Quaternion.FromToRotation(fr.normalized, frente) * arm.transform.rotation;
                }
                // contra la pared y corrido hacia la esquina
                arm.transform.position = C(c.x, c.y) + pared * (Celda * 0.5f - Grosor * 0.5f - 0.6f) + lado * (Celda * 0.5f - Grosor * 0.5f - 0.65f);
            }
        }

        // ---- Props pegados a las paredes ----
        Transform gHuesos = Grupo("Huesos"), gRastros = Grupo("Rastros"), gMarcas = Grupo("Marcas"),
                  gRaices = Grupo("Raices"), gEscombros = Grupo("Escombros"), gCristales = Grupo("Cristales"), gCharcos = Grupo("Charcos");
        var bateria = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBateria);

        for (int k = 0; k < 4; k++) if (Tomar(out var c, out var d)) Esqueleto(gHuesos, Cara(c, d), d, rnd);
        for (int k = 0; k < 7; k++) if (Tomar(out var c, out var d)) Huesos(gHuesos, Cara(c, d), d, rnd);
        for (int k = 0; k < 6; k++)
            if (Tomar(out var c, out var d, 3)) Rastro(gRastros, Cara(c, d), d, rnd, k, k % 2 == 0, bateria);
        string[] textos = { "NO SIGAS", "YA PASE POR AQUI", "¿CUANTOS DIAS?", "NO ESTAS SOLO" };
        for (int k = 0; k < 12; k++)
            if (Tomar(out var c, out var d)) Marca(gMarcas, Cara(c, d), d, rnd, k % 3, textos[(k / 3) % textos.Length]);
        for (int k = 0; k < 10; k++) if (Tomar(out var c, out var d)) Raiz(gRaices, Cara(c, d), d, rnd);
        for (int k = 0; k < 14; k++) if (Tomar(out var c, out var d)) Escombros(gEscombros, Cara(c, d), d, rnd, piedra);
        for (int k = 0; k < 10; k++) if (Tomar(out var c, out var d)) Cristales(gCristales, Cara(c, d), d, rnd);
        for (int k = 0; k < 10; k++) if (Tomar(out var c, out var d)) Charco(gCharcos, C(c.x, c.y) + d * 0.4f, rnd);

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (!t.GetComponent<Bateria>() && !t.GetComponentInParent<Bateria>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // ------------------------------------------------------------------ Piezas

    static GameObject Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Quaternion rot, Vector3 escala, Material m, bool colision = false)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = escala;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Bloque de piedra con UV en metros (la textura no se estira)
    static GameObject Piedra(Transform padre, string nombre, Vector3 pos, Quaternion rot, Vector3 tam, Material m, bool colision)
    {
        GameObject go = Pieza(padre, PrimitiveType.Cube, nombre, pos, rot, tam, m, colision);
        go.GetComponent<MeshFilter>().sharedMesh = CuboMetros(tam);
        return go;
    }

    // Tubo recto entre dos puntos
    static GameObject Hueso(Transform padre, string nombre, Vector3 a, Vector3 b, float radio, Material m)
    {
        Vector3 d = b - a;
        return Pieza(padre, PrimitiveType.Capsule, nombre, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d),
                     new Vector3(radio * 2f, d.magnitude * 0.5f + radio, radio * 2f), m);
    }

    static GameObject Tubo(Transform padre, string nombre, Vector3 a, Vector3 b, float radio, Material m)
    {
        Vector3 d = b - a;
        return Pieza(padre, PrimitiveType.Cylinder, nombre, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d),
                     new Vector3(radio * 2f, d.magnitude * 0.5f, radio * 2f), m);
    }

    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    static Vector3 Tangente(Vector3 d) => new Vector3(d.z, 0f, -d.x);

    // ---- 10. Arco de piedra en una puerta (entre la celda c y su vecina d)
    static void Arco(Transform padre, Vector2Int c, Vector2Int d, Material piedra)
    {
        Vector3 dv = new Vector3(d.x, 0f, d.y);
        Vector3 t = new Vector3(Mathf.Abs(dv.z), 0f, Mathf.Abs(dv.x));
        Vector3 centro = C(c.x, c.y) + dv * (Celda * 0.5f);
        float hueco = Celda - Grosor, prof = Grosor + 0.3f, ancho = 0.45f;
        var g = new GameObject("Arco").transform;
        g.SetParent(padre, false);
        g.position = centro;
        Quaternion rot = Quaternion.LookRotation(dv);
        foreach (float lado in new[] { -1f, 1f })
        {
            Vector3 p = centro + t * lado * (hueco * 0.5f - ancho * 0.5f) + Vector3.up * (Alto * 0.5f);
            Piedra(g, "Pilar", p, rot, new Vector3(ancho, Alto, prof), piedra, true);
            Piedra(g, "Base", centro + t * lado * (hueco * 0.5f - ancho * 0.5f) + Vector3.up * 0.15f, rot, new Vector3(ancho + 0.12f, 0.3f, prof + 0.12f), piedra, false);
        }
        Piedra(g, "Dintel", centro + Vector3.up * (Alto - 0.45f), rot, new Vector3(hueco, 0.9f, prof + 0.05f), piedra, true);
        Piedra(g, "Clave", centro + Vector3.up * (Alto - 1.0f), rot, new Vector3(0.45f, 0.5f, prof + 0.12f), piedra, false);
    }

    // ---- 9. Estatua distinta en cada sala de panel
    static void Estatua(Transform padre, int tipo, Vector3 p, Quaternion rot, Material piedra)
    {
        Material m = MatProp("Lab_Estatua", new Color(0.36f, 0.36f, 0.4f), 0.25f, 0f, default);
        string[] nombres = { "Estatua Obelisco", "Estatua Orbe", "Estatua Encapuchado", "Estatua Mano" };
        var g = new GameObject(nombres[tipo]).transform;
        g.SetParent(padre, false);
        g.SetPositionAndRotation(p, rot);
        Piedra(g, "Pedestal", p + Vector3.up * 0.45f, rot, new Vector3(0.9f, 0.9f, 0.9f), piedra, true);
        Piedra(g, "Moldura", p + Vector3.up * 0.95f, rot, new Vector3(1.0f, 0.1f, 1.0f), piedra, false);
        Vector3 b = p + Vector3.up * 1.0f;
        switch (tipo)
        {
            case 0: // obelisco escalonado
                for (int i = 0; i < 5; i++)
                {
                    float w = 0.5f - i * 0.08f, h = 0.4f;
                    Pieza(g, PrimitiveType.Cube, "Bloque", b + Vector3.up * (h * 0.5f + i * h), rot, new Vector3(w, h, w), m);
                }
                Pieza(g, PrimitiveType.Cube, "Punta", b + Vector3.up * 2.15f, rot * Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.16f, m);
                break;
            case 1: // orbe sobre una columna
                Pieza(g, PrimitiveType.Cylinder, "Columna", b + Vector3.up * 0.5f, rot, new Vector3(0.3f, 0.5f, 0.3f), m);
                Pieza(g, PrimitiveType.Sphere, "Orbe", b + Vector3.up * 1.25f, rot, Vector3.one * 0.55f, MatCristal());
                break;
            case 2: // figura encapuchada
                Pieza(g, PrimitiveType.Capsule, "Cuerpo", b + Vector3.up * 0.75f, rot, new Vector3(0.55f, 0.75f, 0.45f), m);
                Pieza(g, PrimitiveType.Sphere, "Capucha", b + Vector3.up * 1.55f + rot * Vector3.back * 0.03f, rot, new Vector3(0.42f, 0.48f, 0.44f), m);
                Pieza(g, PrimitiveType.Sphere, "Cara", b + Vector3.up * 1.52f + rot * Vector3.forward * 0.12f, rot, new Vector3(0.26f, 0.3f, 0.2f), MatProp("Lab_Sombra", new Color(0.02f, 0.02f, 0.03f), 0f, 0f, default));
                break;
            default: // mano abierta hacia arriba
                Pieza(g, PrimitiveType.Cylinder, "Antebrazo", b + Vector3.up * 0.4f, rot, new Vector3(0.22f, 0.4f, 0.22f), m);
                Pieza(g, PrimitiveType.Cube, "Palma", b + Vector3.up * 0.95f, rot, new Vector3(0.36f, 0.32f, 0.14f), m);
                for (int f = 0; f < 4; f++)
                {
                    float x = -0.13f + f * 0.087f;
                    float largo = f == 0 || f == 3 ? 0.28f : 0.36f;
                    Vector3 a = b + Vector3.up * 1.1f + rot * new Vector3(x, 0f, 0f);
                    Hueso(g, "Dedo", a, a + Vector3.up * largo + rot * new Vector3(x * 0.4f, 0f, 0.05f), 0.035f, m);
                }
                Vector3 pulgar = b + Vector3.up * 0.95f + rot * new Vector3(0.2f, 0f, 0f);
                Hueso(g, "Pulgar", pulgar, pulgar + rot * new Vector3(0.18f, 0.18f, 0.04f), 0.04f, m);
                break;
        }
    }

    // ---- 8. Esqueleto sentado contra la pared
    static void Esqueleto(Transform padre, Vector3 cara, Vector3 d, System.Random rnd)
    {
        Material m = MatProp("Lab_Hueso", new Color(0.7f, 0.66f, 0.56f), 0.3f, 0f, default);
        Vector3 n = -d, t = Tangente(d);
        Vector3 o = cara + n * 0.25f + t * Rand(rnd, -0.8f, 0.8f);
        var g = new GameObject("Esqueleto").transform;
        g.SetParent(padre, false);
        g.position = o;
        Quaternion mira = Quaternion.LookRotation(n);
        // columna inclinada contra la pared
        Vector3 cadera = o + Vector3.up * 0.2f, cuello = o - n * 0.12f + Vector3.up * 0.85f;
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = Vector3.Lerp(cadera, cuello, i / 5f);
            Pieza(g, PrimitiveType.Cube, "Vertebra", p, Quaternion.LookRotation(n, cuello - cadera), new Vector3(0.07f, 0.06f, 0.07f), m);
        }
        Pieza(g, PrimitiveType.Cube, "Pelvis", cadera, mira, new Vector3(0.32f, 0.14f, 0.16f), m);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = Vector3.Lerp(cadera, cuello, 0.45f + i * 0.13f) + n * 0.08f;
            Hueso(g, "Costilla", p - t * 0.15f, p + t * 0.15f, 0.018f, m);
        }
        float giro = Rand(rnd, -25f, 25f);
        Vector3 cabeza = cuello + Vector3.up * 0.13f + n * 0.04f;
        Pieza(g, PrimitiveType.Sphere, "Craneo", cabeza, mira * Quaternion.Euler(Rand(rnd, 10f, 35f), giro, 0f), new Vector3(0.19f, 0.21f, 0.22f), m);
        foreach (float lado in new[] { -1f, 1f })
        {
            Pieza(g, PrimitiveType.Sphere, "Ojo", cabeza + n * 0.09f + t * lado * 0.045f + Vector3.up * 0.01f, mira, Vector3.one * 0.05f, MatProp("Lab_Sombra", new Color(0.02f, 0.02f, 0.03f), 0f, 0f, default));
            // piernas estiradas hacia el pasillo
            Vector3 cad = cadera + t * lado * 0.1f, rod = cad + n * 0.45f + Vector3.up * Rand(rnd, 0.0f, 0.2f) + t * lado * 0.05f;
            Hueso(g, "Femur", cad, rod, 0.035f, m);
            Hueso(g, "Tibia", rod, new Vector3(rod.x, 0.04f, rod.z) + n * 0.4f, 0.03f, m);
            // brazos colgando
            Vector3 hombro = cuello - Vector3.up * 0.06f + t * lado * 0.18f, codo = hombro - Vector3.up * 0.3f + n * 0.05f;
            Hueso(g, "Humero", hombro, codo, 0.03f, m);
            Hueso(g, "Antebrazo", codo, new Vector3(codo.x, 0.05f, codo.z) + n * 0.25f + t * lado * 0.05f, 0.025f, m);
        }
    }

    // ---- 8. Huesos sueltos
    static void Huesos(Transform padre, Vector3 cara, Vector3 d, System.Random rnd)
    {
        Material m = MatProp("Lab_Hueso", new Color(0.7f, 0.66f, 0.56f), 0.3f, 0f, default);
        Vector3 n = -d, t = Tangente(d);
        var g = new GameObject("Huesos").transform;
        g.SetParent(padre, false);
        Vector3 o = cara + n * 0.45f + t * Rand(rnd, -1f, 1f);
        g.position = o;
        int k = rnd.Next(3, 6);
        for (int i = 0; i < k; i++)
        {
            Vector3 p = o + t * Rand(rnd, -0.5f, 0.5f) + n * Rand(rnd, -0.25f, 0.3f) + Vector3.up * 0.03f;
            Vector3 dir = Quaternion.Euler(0f, Rand(rnd, 0f, 360f), 0f) * Vector3.forward * Rand(rnd, 0.15f, 0.4f);
            Hueso(g, "Hueso", p - dir * 0.5f, p + dir * 0.5f, 0.025f, m);
        }
        if (rnd.NextDouble() < 0.6)
            Pieza(g, PrimitiveType.Sphere, "Craneo", o + t * Rand(rnd, -0.3f, 0.3f) + Vector3.up * 0.09f,
                  Quaternion.Euler(Rand(rnd, -30f, 30f), Rand(rnd, 0f, 360f), Rand(rnd, 60f, 100f)), new Vector3(0.19f, 0.21f, 0.22f), m);
    }

    // ---- 5. Mochila o linterna tirada (con una bateria) y zapatos
    static void Rastro(Transform padre, Vector3 cara, Vector3 d, System.Random rnd, int k, bool mochila, GameObject bateria)
    {
        Vector3 n = -d, t = Tangente(d);
        var g = new GameObject(mochila ? "Mochila" : "Linterna tirada").transform;
        g.SetParent(padre, false);
        Vector3 o = cara + n * 0.35f + t * Rand(rnd, -0.8f, 0.8f);
        g.position = o;
        Quaternion giro = Quaternion.Euler(0f, Rand(rnd, 0f, 360f), 0f);
        if (mochila)
        {
            Material tela = MatProp("Lab_Tela", new Color(0.16f, 0.18f, 0.11f), 0.15f, 0f, default);
            Material correa = MatProp("Lab_Correa", new Color(0.06f, 0.05f, 0.04f), 0.3f, 0f, default);
            Quaternion caida = giro * Quaternion.Euler(Rand(rnd, 60f, 80f), 0f, 0f); // tumbada
            Pieza(g, PrimitiveType.Cube, "Cuerpo", o + Vector3.up * 0.13f, caida, new Vector3(0.36f, 0.46f, 0.22f), tela, true);
            Pieza(g, PrimitiveType.Cube, "Bolsillo", o + Vector3.up * 0.13f + caida * new Vector3(0f, -0.08f, 0.13f), caida, new Vector3(0.28f, 0.18f, 0.08f), tela);
            Pieza(g, PrimitiveType.Cube, "Tapa", o + Vector3.up * 0.13f + caida * new Vector3(0f, 0.22f, 0.02f), caida * Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.37f, 0.06f, 0.24f), tela);
            foreach (float lado in new[] { -1f, 1f })
                Pieza(g, PrimitiveType.Cube, "Correa", o + Vector3.up * 0.13f + caida * new Vector3(lado * 0.1f, 0f, -0.12f), caida, new Vector3(0.04f, 0.4f, 0.015f), correa);
        }
        else
        {
            Material metal = MatProp("Lab_MetalLinterna", new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.8f, default);
            Material vidrio = MatProp("Lab_VidrioRoto", new Color(0.5f, 0.55f, 0.6f), 0.9f, 0f, default);
            Quaternion tumbada = giro * Quaternion.Euler(0f, 0f, 90f);
            Pieza(g, PrimitiveType.Cylinder, "Mango", o + Vector3.up * 0.04f, tumbada, new Vector3(0.07f, 0.12f, 0.07f), metal, true);
            Vector3 cabezaP = o + Vector3.up * 0.05f + tumbada * new Vector3(0f, 0.16f, 0f);
            Pieza(g, PrimitiveType.Cylinder, "Cabeza", cabezaP, tumbada, new Vector3(0.1f, 0.04f, 0.1f), metal);
            Pieza(g, PrimitiveType.Cylinder, "Vidrio roto", cabezaP + tumbada * new Vector3(0f, 0.042f, 0f), tumbada, new Vector3(0.085f, 0.003f, 0.085f), vidrio);
        }
        // Bateria al lado (para recargar la linterna del jugador)
        if (bateria)
        {
            var b = (GameObject)PrefabUtility.InstantiatePrefab(bateria, g);
            Undo.RegisterCreatedObjectUndo(b, "Bateria");
            b.name = (mochila ? "Bateria Mochila " : "Bateria Linterna ") + (k + 1);
            b.transform.position = o + t * 0.35f + n * 0.1f + Vector3.up * 0.1f;
            b.transform.rotation = Quaternion.Euler(0f, Rand(rnd, 0f, 360f), 0f);
        }
        // Un zapato o dos cerca
        Material cuero = MatProp("Lab_Cuero", new Color(0.1f, 0.06f, 0.04f), 0.35f, 0f, default);
        int zap = rnd.Next(1, 3);
        for (int i = 0; i < zap; i++)
        {
            Vector3 p = o - t * Rand(rnd, 0.4f, 0.8f) + n * Rand(rnd, -0.1f, 0.4f);
            Quaternion r = Quaternion.Euler(0f, Rand(rnd, 0f, 360f), rnd.NextDouble() < 0.4 ? 90f : 0f);
            var zg = new GameObject("Zapato").transform;
            zg.SetParent(g, false);
            Pieza(zg, PrimitiveType.Cube, "Suela", p + Vector3.up * 0.02f, r, new Vector3(0.11f, 0.03f, 0.28f), MatProp("Lab_Sombra", new Color(0.02f, 0.02f, 0.03f), 0f, 0f, default));
            Pieza(zg, PrimitiveType.Cube, "Capellada", p + Vector3.up * 0.065f + r * new Vector3(0f, 0f, 0.03f), r, new Vector3(0.1f, 0.07f, 0.2f), cuero);
            Pieza(zg, PrimitiveType.Cube, "Talon", p + Vector3.up * 0.09f + r * new Vector3(0f, 0f, -0.1f), r, new Vector3(0.1f, 0.12f, 0.07f), cuero);
        }
    }

    // ---- 6. Marcas de tiza en la pared: conteo de dias, flecha o un texto
    static void Marca(Transform padre, Vector3 cara, Vector3 d, System.Random rnd, int tipo, string texto)
    {
        Material tiza = MatProp("Lab_Tiza", new Color(0.78f, 0.77f, 0.72f), 0.05f, 0f, new Color(0.08f, 0.08f, 0.08f));
        Vector3 n = -d, t = Tangente(d);
        Vector3 o = cara + n * 0.012f + t * Rand(rnd, -0.9f, 0.9f) + Vector3.up * Rand(rnd, 1.1f, 1.6f);
        var g = new GameObject(tipo == 0 ? "Conteo" : tipo == 1 ? "Flecha" : "Texto").transform;
        g.SetParent(padre, false);
        g.position = o;
        Quaternion pared = Quaternion.LookRotation(n);
        void Raya(Vector3 centro, float largo, float ang, float grosor = 0.025f)
        {
            var r = Pieza(g, PrimitiveType.Cube, "Raya", centro, pared * Quaternion.Euler(0f, 0f, ang), new Vector3(grosor, largo, 0.004f), tiza);
            r.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        switch (tipo)
        {
            case 0:
                int grupos = rnd.Next(3, 6);
                for (int k = 0; k < grupos; k++)
                {
                    Vector3 base_ = o + t * (k * 0.22f - grupos * 0.11f);
                    int palos = k == grupos - 1 ? rnd.Next(1, 5) : 4;
                    for (int s = 0; s < palos; s++) Raya(base_ + t * (s * 0.035f), 0.2f + Rand(rnd, -0.02f, 0.02f), Rand(rnd, -6f, 6f));
                    if (palos == 4) Raya(base_ + t * 0.052f, 0.24f, 62f);
                }
                break;
            case 1:
                float lado = rnd.NextDouble() < 0.5 ? -1f : 1f;
                Raya(o, 0.7f, 90f, 0.035f);
                Raya(o + t * lado * 0.3f + Vector3.up * 0.08f, 0.22f, 90f + lado * 45f, 0.035f);
                Raya(o + t * lado * 0.3f - Vector3.up * 0.08f, 0.22f, 90f - lado * 45f, 0.035f);
                break;
            default:
                var tg = new GameObject("Texto " + texto);
                tg.transform.SetParent(g, false);
                tg.transform.SetPositionAndRotation(o, Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, Rand(rnd, -6f, 6f)));
                var tmp = tg.AddComponent<TextMeshPro>();
                var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (fuente) tmp.font = fuente;
                tmp.text = texto;
                tmp.fontSize = 3.2f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.8f, 0.78f, 0.72f, 0.85f);
                tmp.rectTransform.sizeDelta = new Vector2(2.6f, 0.6f);
                break;
        }
    }

    // ---- 2. Raiz gruesa que sale del piso y trepa por la pared, con ramas por el suelo
    static void Raiz(Transform padre, Vector3 cara, Vector3 d, System.Random rnd)
    {
        Material m = MatRaiz();
        Vector3 n = -d, t = Tangente(d);
        var g = new GameObject("Raiz").transform;
        g.SetParent(padre, false);
        Vector3 p = cara + n * 0.12f + t * Rand(rnd, -1f, 1f) + Vector3.down * 0.05f;
        g.position = p;
        float radio = Rand(rnd, 0.1f, 0.15f);
        float alto = Rand(rnd, 2.2f, 3.8f);
        int segs = 9;
        Vector3 prev = p;
        for (int i = 1; i <= segs; i++)
        {
            float f = i / (float)segs;
            Vector3 q = p + Vector3.up * (alto * f) + t * (Mathf.Sin(f * 5f + (float)rnd.NextDouble()) * 0.25f) + n * (0.02f * Mathf.Sin(f * 9f));
            float r = Mathf.Lerp(radio, radio * 0.25f, f);
            Tubo(g, "Tramo", prev, q, r, m);
            Pieza(g, PrimitiveType.Sphere, "Nudo", q, Quaternion.identity, Vector3.one * r * 2f, m);
            prev = q;
        }
        // ramas que se arrastran por el piso
        int ramas = rnd.Next(2, 4);
        for (int k = 0; k < ramas; k++)
        {
            Vector3 a = p + Vector3.up * 0.06f;
            Vector3 dir = (n * Rand(rnd, 0.4f, 1f) + t * Rand(rnd, -1f, 1f)).normalized;
            float largo = Rand(rnd, 0.6f, 1.4f), r0 = radio * 0.6f;
            for (int s = 0; s < 4; s++)
            {
                dir = Quaternion.Euler(0f, Rand(rnd, -30f, 30f), 0f) * dir;
                Vector3 b = a + dir * (largo / 4f);
                b.y = 0.04f;
                float r = Mathf.Lerp(r0, r0 * 0.3f, (s + 1) / 4f);
                Tubo(g, "Rama", a, b, r, m);
                Pieza(g, PrimitiveType.Sphere, "Nudo", b, Quaternion.identity, Vector3.one * r * 2f, m);
                a = b;
            }
        }
    }

    // ---- 1. Escombros: piedras caidas contra la pared
    static void Escombros(Transform padre, Vector3 cara, Vector3 d, System.Random rnd, Material piedra)
    {
        Vector3 n = -d, t = Tangente(d);
        var g = new GameObject("Escombros").transform;
        g.SetParent(padre, false);
        Vector3 o = cara + t * Rand(rnd, -0.9f, 0.9f);
        g.position = o;
        int k = rnd.Next(5, 9);
        for (int i = 0; i < k; i++)
        {
            float s = i == 0 ? Rand(rnd, 0.45f, 0.65f) : Rand(rnd, 0.1f, 0.35f);
            Vector3 tam = new Vector3(s * Rand(rnd, 0.8f, 1.4f), s * Rand(rnd, 0.6f, 1f), s * Rand(rnd, 0.8f, 1.3f));
            Vector3 p = o + n * (s * 0.5f + Rand(rnd, 0f, 0.6f)) + t * Rand(rnd, -0.7f, 0.7f) + Vector3.up * (tam.y * 0.35f);
            Piedra(g, "Piedra", p, Quaternion.Euler(Rand(rnd, -25f, 25f), Rand(rnd, 0f, 360f), Rand(rnd, -25f, 25f)), tam, piedra, i == 0);
        }
        // un pedazo de pared desprendido apoyado
        Piedra(g, "Pedazo de pared", o + n * 0.3f + Vector3.up * 0.45f, Quaternion.LookRotation(n) * Quaternion.Euler(-20f, 0f, Rand(rnd, -10f, 10f)),
               new Vector3(Rand(rnd, 0.6f, 1f), 0.9f, 0.18f), piedra, true);
    }

    // ---- 3. Cristales indigo que brotan del piso
    static void Cristales(Transform padre, Vector3 cara, Vector3 d, System.Random rnd)
    {
        Material m = MatCristal();
        Vector3 n = -d, t = Tangente(d);
        var g = new GameObject("Cristales").transform;
        g.SetParent(padre, false);
        Vector3 o = cara + n * 0.3f + t * Rand(rnd, -0.9f, 0.9f);
        g.position = o;
        int k = rnd.Next(3, 7);
        for (int i = 0; i < k; i++)
        {
            float h = Rand(rnd, 0.3f, 0.9f), w = Rand(rnd, 0.07f, 0.16f);
            Vector3 p = o + n * Rand(rnd, -0.15f, 0.25f) + t * Rand(rnd, -0.3f, 0.3f);
            Quaternion r = Quaternion.Euler(Rand(rnd, -30f, 30f), Rand(rnd, 0f, 360f), Rand(rnd, -30f, 30f));
            var c = Pieza(g, PrimitiveType.Cube, "Cristal", p + r * Vector3.up * (h * 0.4f), r, new Vector3(w, h, w), m);
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var lg = new GameObject("Luz Cristales");
        lg.transform.SetParent(g, false);
        lg.transform.position = o + Vector3.up * 0.5f + n * 0.2f;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.38f, 0.12f, 1f); l.range = 3.5f; l.intensity = 1.5f; l.shadows = LightShadows.None;
    }

    // ---- 4. Charco oscuro que refleja
    static void Charco(Transform padre, Vector3 p, System.Random rnd)
    {
        Material m = MatProp("Lab_Charco", new Color(0.015f, 0.015f, 0.03f), 0.97f, 0.2f, new Color(0.03f, 0.01f, 0.08f));
        var c = Pieza(padre, PrimitiveType.Cylinder, "Charco", new Vector3(p.x, 0.006f, p.z), Quaternion.Euler(0f, Rand(rnd, 0f, 360f), 0f),
                      new Vector3(Rand(rnd, 1.0f, 1.9f), 0.004f, Rand(rnd, 0.7f, 1.3f)), m);
        c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ Materiales

    static Material MatProp(string nombre, Color c, float suave, float metal, Color emision)
    {
        string ruta = $"Assets/Laberinto/Materiales/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        System.IO.Directory.CreateDirectory("Assets/Laberinto/Materiales");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre, enableInstancing = true };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", suave);
        m.SetFloat("_Metallic", metal);
        if (emision != default) { m.SetColor("_EmissionColor", emision); m.EnableKeyword("_EMISSION"); }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material MatRaiz() => MatProp("Lab_Raiz", new Color(0.07f, 0.03f, 0.1f), 0.35f, 0f, new Color(0.38f, 0.12f, 1f) * 0.18f);
    static Material MatCristal()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Laberinto/Materiales/Lab_CristalIndigo.mat");
        return m ? m : MatProp("Lab_CristalIndigo", new Color(0.25f, 0.08f, 0.6f), 0.9f, 0f, new Color(0.38f, 0.12f, 1f) * 3f);
    }
}
