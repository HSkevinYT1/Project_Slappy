using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Orilla de la Zona Roja (parte de ZonaRojaSetup, usa la misma forma de isla):
// - "Paredes invisibles": muro sin render (solo MeshCollider) por toda la orilla, justo donde el terreno empieza
//   a bajar al mar, asi el jugador no puede meterse al agua. La orilla pasa por adentro del faro: ahi el muro se
//   corta a mitad del muro del faro (el propio faro cierra el paso). Va en la capa "Ignore Raycast" para no tapar
//   los raycasts (interaccion, linterna, vision de los enemigos).
// - "Paredes lago": lo mismo alrededor del lago del bosque, ~1 m antes del agua (el cubo negro queda afuera, alcanzable).
// - "Olas": sonido del mar (Sounds/SFX/sonido-stone-ocean) con SonidoOrilla: una sola fuente 3D que sigue al
//   punto de la orilla mas cercano al jugador (fuerte en la orilla, se apaga tierra adentro).
// Se agrega sola a la Zona Roja que ya existe (sin reconstruirla) y se puede rehacer con
// Tools > Zona Roja > Rehacer orilla (paredes y olas). Despues guarda la escena (Ctrl+S).
public static partial class ZonaRojaSetup
{
    const string NombreOrilla = "Orilla";
    const string MarcaOrilla = "_orilla v2"; // v2: + paredes del lago
    const string RutaOlas = "Assets/Sounds/SFX/sonido-stone-ocean.mp3";
    // Valores de Terreno.Orilla: 1 = termina lo plano; el agua empieza en ~1.025.
    const float OrillaPared = 1.01f;
    const float OrillaAgua = 1.03f;
    const float ParedAbajo = 3f, ParedArriba = 10f, GrosorPared = 1f; // relativo al suelo de la isla
    // Lago: el terreno empieza a hundirse a RadioLagoEn + 7 m y el agua llega a ~RadioLagoEn + 3.5 m.
    const float LagoPared = 4.5f;
    const float AlturaOlas = 0.5f;
    const float OlasCerca = 6f, OlasLejos = 40f; // a menos de 6 m suena a todo volumen, a 40 m ya no se oye
    const int PuntosOrilla = 480;                // ~1.3 m entre puntos

    static Terreno TerrenoIsla() => new Terreno(new Vector3(-(MitadIsla - 1f), -2f * AltoPiso, -DesplazamientoSur));

    // Zona Roja ya creada: agrega la orilla si falta (o si es de una version vieja).
    static void AsegurarOrilla(Transform raiz)
    {
        Transform vieja = raiz.Find(NombreOrilla);
        if (vieja && vieja.Find(MarcaOrilla)) return;
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        ConstruirOrilla(raiz, TerrenoIsla());
        EditorSceneManager.MarkSceneDirty(raiz.gameObject.scene);
        Debug.Log("[Zona Roja] Orilla: paredes invisibles y sonido de olas agregados. Guarda la escena (Ctrl+S).", raiz.Find(NombreOrilla));
    }

    [MenuItem("Tools/Zona Roja/Rehacer orilla (paredes y olas)")]
    static void MenuOrilla()
    {
        GameObject zr = GameObject.Find("Zona Roja");
        if (!zr) { EditorUtility.DisplayDialog("Zona Roja", "No se encontro \"Zona Roja\" en la escena.", "OK"); return; }
        Transform vieja = zr.transform.Find(NombreOrilla);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        AsegurarOrilla(zr.transform);
        Selection.activeGameObject = zr.transform.Find(NombreOrilla).gameObject;
    }

    static void ConstruirOrilla(Transform raiz, Terreno ter)
    {
        var orilla = new GameObject(NombreOrilla);
        Undo.RegisterCreatedObjectUndo(orilla, "Orilla Zona Roja");
        orilla.transform.SetParent(raiz, false);
        orilla.transform.localPosition = ter.centro; // centro de la isla, a nivel del suelo
        new GameObject(MarcaOrilla).transform.SetParent(orilla.transform, false);

        // ---- Paredes invisibles ----
        Vector2 faro = new Vector2(-ter.centro.x, -ter.centro.z); // el faro es el origen de "Zona Roja"
        var tramos = Recortar(Contorno(ter, OrillaPared), faro, RadioFaro - GrosorMuro * 0.5f);
        var paredes = new GameObject("Paredes invisibles");
        paredes.layer = 2; // Ignore Raycast
        paredes.transform.SetParent(orilla.transform, false);
        paredes.AddComponent<MeshCollider>().sharedMesh = GuardarMalla(MallaPared(tramos), "ParedesOrilla");

        // ---- Paredes del lago ----
        var lago = new List<Vector2>(PuntosOrilla);
        for (int i = PuntosOrilla - 1; i >= 0; i--) // horario: asi el grosor del muro queda hacia el agua
        {
            float ang = i * Mathf.PI * 2f / PuntosOrilla;
            lago.Add(ter.lago + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (ter.RadioLagoEn(ang) + LagoPared));
        }
        lago.Add(lago[0]);
        var paredesLago = new GameObject("Paredes lago");
        paredesLago.layer = 2; // Ignore Raycast
        paredesLago.transform.SetParent(orilla.transform, false);
        paredesLago.AddComponent<MeshCollider>().sharedMesh = GuardarMalla(MallaPared(new List<List<Vector2>> { lago }), "ParedesLago");

        // ---- Olas ----
        List<Vector2> agua = Contorno(ter, OrillaAgua);
        var puntos = new Vector3[agua.Count];
        for (int i = 0; i < agua.Count; i++) puntos[i] = new Vector3(agua[i].x, AlturaOlas, agua[i].y);

        var olas = new GameObject("Olas");
        olas.transform.SetParent(orilla.transform, false);
        olas.transform.localPosition = puntos[0];
        var fuente = olas.AddComponent<AudioSource>();
        fuente.clip = ClipOlas();
        fuente.loop = true;
        fuente.playOnAwake = true;
        fuente.volume = 1f;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = OlasCerca;
        fuente.maxDistance = OlasLejos;
        fuente.spread = 90f;       // mas envolvente, no un punto exacto
        fuente.dopplerLevel = 0f;  // la fuente se mueve con el jugador: sin doppler
        olas.AddComponent<SonidoOrilla>().orilla = puntos;
    }

    // El clip es largo: mejor en Streaming que descomprimido entero en memoria.
    static AudioClip ClipOlas()
    {
        if (AssetImporter.GetAtPath(RutaOlas) is AudioImporter imp && imp.defaultSampleSettings.loadType != AudioClipLoadType.Streaming)
        {
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaOlas);
        if (!clip) Debug.LogWarning("[Zona Roja] No encuentro el sonido de olas en " + RutaOlas);
        return clip;
    }

    // Contorno de la isla donde Terreno.Orilla == q, en orden antihorario (en planta X/Z, relativo al centro de la isla).
    // Orilla(x, z) es proporcional a la distancia al centro en cada direccion, asi que el radio sale directo.
    static List<Vector2> Contorno(Terreno ter, float q)
    {
        var pts = new List<Vector2>(PuntosOrilla);
        for (int i = 0; i < PuntosOrilla; i++)
        {
            float ang = i * Mathf.PI * 2f / PuntosOrilla;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            pts.Add(dir * (q / ter.Orilla(dir.x, dir.y)));
        }
        return pts;
    }

    // Parte el contorno cerrado en tramos abiertos que quedan FUERA del circulo (c, r), cortados justo en el borde.
    static List<List<Vector2>> Recortar(List<Vector2> cerrado, Vector2 c, float r)
    {
        int n = cerrado.Count;
        bool Fuera(Vector2 p) => (p - c).sqrMagnitude >= r * r;
        var tramos = new List<List<Vector2>>();

        int dentro = -1;
        for (int i = 0; i < n && dentro < 0; i++) if (!Fuera(cerrado[i])) dentro = i;
        if (dentro < 0)
        {
            var todo = new List<Vector2>(cerrado) { cerrado[0] }; // no toca el circulo: lazo cerrado
            tramos.Add(todo);
            return tramos;
        }

        // Empezando en un punto de adentro, ningun tramo da la vuelta por el final de la lista.
        List<Vector2> actual = null;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = cerrado[(dentro + k) % n], b = cerrado[(dentro + k + 1) % n];
            bool fa = Fuera(a), fb = Fuera(b);
            if (!fa && fb) actual = new List<Vector2> { Cruce(a, b, c, r, false), b };
            else if (fa && fb) actual?.Add(b);
            else if (fa && !fb && actual != null)
            {
                actual.Add(Cruce(a, b, c, r, true));
                tramos.Add(actual);
                actual = null;
            }
        }
        return tramos;
    }

    // Punto donde el segmento a-b cruza el circulo (entrando = de afuera hacia adentro).
    static Vector2 Cruce(Vector2 a, Vector2 b, Vector2 c, float r, bool entrando)
    {
        Vector2 d = b - a, f = a - c;
        float A = Vector2.Dot(d, d), B = 2f * Vector2.Dot(f, d), C = Vector2.Dot(f, f) - r * r;
        float raiz = Mathf.Sqrt(Mathf.Max(0f, B * B - 4f * A * C));
        float t = (-B + (entrando ? -raiz : raiz)) / (2f * A);
        return a + d * Mathf.Clamp01(t);
    }

    // Muro cerrado (cara interior, exterior, arriba, abajo y tapas) de GrosorPared hacia el agua, para cada tramo.
    static Mesh MallaPared(List<List<Vector2>> tramos)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        float y0 = -ParedAbajo, y1 = ParedArriba;

        foreach (List<Vector2> p in tramos)
        {
            int n = p.Count;
            if (n < 2) continue;
            int b0 = v.Count;
            var normales = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 tg = p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)];
                Vector2 hacia = new Vector2(tg.y, -tg.x).normalized; // hacia el agua (costa en antihorario, lago en horario)
                normales[i] = new Vector3(hacia.x, 0f, hacia.y);
                var dentro = new Vector3(p[i].x, 0f, p[i].y);
                var fuera = dentro + normales[i] * GrosorPared;
                v.Add(dentro + Vector3.up * y0); // 4i + 0
                v.Add(dentro + Vector3.up * y1); // 4i + 1
                v.Add(fuera + Vector3.up * y0);  // 4i + 2
                v.Add(fuera + Vector3.up * y1);  // 4i + 3
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = b0 + 4 * i, b = a + 4;
                Vector3 m = normales[i];
                Quad(v, t, a + 0, a + 1, b + 0, b + 1, -m);          // cara hacia la isla
                Quad(v, t, a + 2, a + 3, b + 2, b + 3, m);           // cara hacia el mar
                Quad(v, t, a + 1, a + 3, b + 1, b + 3, Vector3.up);  // arriba
                Quad(v, t, a + 0, a + 2, b + 0, b + 2, Vector3.down);// abajo
            }
            Vector3 ini = v[b0 + 4] - v[b0], fin = v[b0 + 4 * (n - 1)] - v[b0 + 4 * (n - 2)];
            int u = b0 + 4 * (n - 1);
            Quad(v, t, b0 + 0, b0 + 1, b0 + 2, b0 + 3, -ini); // tapas de los extremos (contra el faro)
            Quad(v, t, u + 0, u + 1, u + 2, u + 3, fin);
        }

        var malla = new Mesh { name = "ParedesOrilla" };
        if (v.Count > 65000) malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malla.SetVertices(v);
        malla.SetTriangles(t, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    // Cuadrilatero (a, b, c, d con a-d y b-c en diagonal) con su cara de frente hacia "normal".
    static void Quad(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 normal)
    {
        Tri(v, t, a, b, c, normal);
        Tri(v, t, c, b, d, normal);
    }

    static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 normal)
    {
        // En Unity la cara de frente es la que mira hacia Cross(b - a, c - a).
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), normal) < 0f) { int x = b; b = c; c = x; }
        t.Add(a); t.Add(b); t.Add(c);
    }
}
