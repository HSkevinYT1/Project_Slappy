using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

// ZONA ROJA (bloqueo): isla con un faro.
// - Faro de 3 pisos. La entrada a la zona es en el PISO 3 y cuadra con el pasillo rojo de Zona_Principal.
// - Piso 3 = sala de la linterna: enfrente de la entrada esta la estructura de la luz giratoria (APAGADA).
// - Los pisos se conectan con una escalera de caracol pegada a la pared interior.
// - Isla con bosque (cada arbol es un solo bloque), un lago en medio del bosque con un cubo negro al lado,
//   y 4 notas (esferas) en medio del bosque.
// - El mar y el lago usan una COPIA del material de agua de las Cloacas (el shader no se modifica).
//
// Se crea sola una vez (si no existe "Zona Roja") y se puede rehacer con Tools > Zona Roja > Reconstruir.
public static class ZonaRojaSetup
{
    const string Version = "_zona roja v5";
    const string Carpeta = "Assets/ZonaRoja";
    const string CarpetaMat = Carpeta + "/Materiales";
    const string CarpetaMallas = Carpeta + "/Mallas";

    // ---- Faro ----
    const float AltoPiso = 10f;         // cada piso mide 10 m (al principio 4): mismos 3 pisos, mucho mas altos
    const float RadioFaro = 9f;         // radio exterior (antes 7)
    const float GrosorMuro = 0.6f;
    const float RadioInterior = RadioFaro - GrosorMuro;
    const float AnchoEscalera = 2.5f;   // ancho de cada escalera
    const float AlturaLibre = 3f;       // altura libre sobre la escalera (el jugador mide ~1.72 m)
    const float GiroEscalera = 288f;    // grados que gira cada tramo de escalera (de un piso al siguiente)
    const int Escalones = (int)(AltoPiso / 0.25f); // escalones de 0.25 m
    const float AnchoPuerta = 13f;      // medio angulo de las puertas (grados)
    const float DistEntrada = RadioFaro - 2f; // la punta del pasillo entra 2 m en el faro (dentro del muro)
    // Angulos (0 = hacia el pasillo / entrada del piso 3, 180 = hacia la isla / puerta del piso 1).
    // Tramo 1->2 por el anillo EXTERIOR (pegado al muro) y tramo 2->3 por un anillo INTERIOR,
    // asi los huecos de cada piso no se cruzan y las puertas quedan libres.
    const float InicioEscalera1a2 = -108f; // termina en 180 (sobre la puerta del piso 1, a 4 m)
    const float InicioEscalera2a3 = 30f;   // su hueco en el piso 3 queda lejos de la entrada (0)
    static float ExtIn => RadioInterior - AnchoEscalera;   // anillo exterior
    static float ExtOut => RadioInterior;
    static float IntIn => RadioInterior - 2f * AnchoEscalera; // anillo interior
    static float IntOut => RadioInterior - AnchoEscalera;

    // ---- Isla ----
    // Isla casi cuadrada (con esquinas) de ~168 m de lado (antes ~128 m: +70% de area).
    // Crece hacia el oeste y el sur: al norte esta la Tienda y al este la Zona Principal, Cloacas y Backrooms.
    const float MitadIsla = 84f;          // medio lado
    const float DesplazamientoSur = 13f;  // el centro de la isla queda al sur del faro (el faro sigue en la orilla este)
    const float Playa = 9f;               // pendiente del borde hasta debajo del mar
    const float CeldaTerreno = 2f;        // resolucion de la malla del terreno
    const int Arboles = 700;              // maximo (los limita la separacion)
    const float SeparacionArboles = 5.5f; // distancia minima entre troncos
    const string ModeloArbol = "Assets/Scenes/jesus/tree 1.obj";
    // Lago "como tal": una cuenca hundida en el terreno, en una ESQUINA de la isla.
    const float RadioLago = 24f;
    const float ProfundidadLago = 3f;
    const float NivelLago = -0.6f;        // relativo al suelo de la isla
    const float NivelMar = -0.8f;
    // Grupo de la Tienda (se mueve hacia el norte si queda encima de la isla).
    static readonly string[] GrupoTienda = { "Tienda", "escaleras", "zotano", "FogVolume", "Linterna" };

    internal static void AutoAplicar()
    {
        GameObject existente = GameObject.Find("Zona Roja");
        if (existente && existente.transform.Find(Version))
        {
            // Sin reconstruir (para no perder cambios hechos a mano): solo se asegura la capa del faro.
            ConfigurarLuzFaro(existente.transform);
            return;
        }
        if (!GameObject.Find("Zona_Principal")) return;
        Crear();
    }

    // ------------------------------------------------------------------ Luz del faro

    const string CapaFaro = "Faro";

    // Todo el faro va en la capa "Faro" y el foco la excluye de su Culling Mask:
    // asi la luz atraviesa el propio faro sin iluminarlo y solo alumbra lo de afuera (el bosque).
    // No toca la intensidad, el color ni la rotacion del foco.
    static void ConfigurarLuzFaro(Transform zonaRoja)
    {
        Transform faro = zonaRoja.Find("Faro");
        if (!faro) return;

        int capa = AsegurarCapa(CapaFaro);
        if (capa < 0)
        {
            Debug.LogWarning("[Zona Roja] No hay capas libres para crear la capa \"Faro\".");
            return;
        }

        bool cambio = false;
        foreach (Transform t in faro.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject.layer == capa) continue;
            Undo.RecordObject(t.gameObject, "Capa del faro");
            t.gameObject.layer = capa;
            cambio = true;
        }
        foreach (Light l in faro.GetComponentsInChildren<Light>(true))
        {
            int mascara = l.cullingMask & ~(1 << capa);
            if (l.cullingMask == mascara) continue;
            Undo.RecordObject(l, "Culling mask del foco");
            l.cullingMask = mascara;
            EditorUtility.SetDirty(l);
            cambio = true;
        }

        // Forward+ (el modo de este proyecto) filtra las luces con RENDERING LAYERS, no con el Culling Mask:
        // - los renderers del faro van SOLO en el rendering layer del faro,
        // - el foco del faro NO incluye ese rendering layer,
        // - todas las demas luces SI lo incluyen (asi el faro se sigue viendo iluminado por el resto).
        cambio |= AplicarRenderingLayers(faro);

        if (cambio) EditorSceneManager.MarkSceneDirty(zonaRoja.gameObject.scene);

        // Diagnostico: que encontro y como quedo.
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Zona Roja] Escena \"{zonaRoja.gameObject.scene.name}\": faro en capa \"{CapaFaro}\" ({capa}), rendering layer {BitRenderingFaro}.");
        foreach (Light l in faro.GetComponentsInChildren<Light>(true))
        {
            var datos = l.GetUniversalAdditionalLightData();
            sb.Append($" Foco \"{l.name}\": activo={l.gameObject.activeInHierarchy}, encendido={l.enabled}, intensidad={l.intensity}, " +
                      $"ignora capa Faro={(l.cullingMask & (1 << capa)) == 0}, ignora rendering layer Faro={(datos.renderingLayers & (1u << BitRenderingFaro)) == 0}.");
        }
        sb.Append(cambio ? " Se aplicaron cambios: guarda la escena (Ctrl+S) y el proyecto (File > Save Project)." : " Ya estaba aplicado.");
        Debug.Log(sb.ToString(), faro);
    }

    const int BitRenderingFaro = 7;

    static bool AplicarRenderingLayers(Transform faro)
    {
        uint bit = 1u << BitRenderingFaro;
        bool cambio = false;

        foreach (Renderer r in faro.GetComponentsInChildren<Renderer>(true))
        {
            if (r.renderingLayerMask == bit) continue;
            Undo.RecordObject(r, "Rendering layer del faro");
            r.renderingLayerMask = bit;
            cambio = true;
        }

        var lucesFaro = new HashSet<Light>(faro.GetComponentsInChildren<Light>(true));
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != faro.gameObject.scene) continue;
            var datos = l.GetUniversalAdditionalLightData();
            uint actual = datos.renderingLayers;
            uint nuevo = lucesFaro.Contains(l) ? ((actual | 1u) & ~bit) : (actual | bit);
            if (nuevo == actual) continue;
            Undo.RecordObject(datos, "Rendering layers de la luz");
            Undo.RecordObject(l, "Rendering layers de la luz");
            datos.renderingLayers = nuevo;
            EditorUtility.SetDirty(datos);
            EditorUtility.SetDirty(l);
            cambio = true;
        }
        return cambio;
    }

    // Devuelve el indice de la capa (la crea en el primer hueco libre de usuario si no existe).
    static int AsegurarCapa(string nombre)
    {
        int existente = LayerMask.NameToLayer(nombre);
        if (existente >= 0) return existente;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty capas = tagManager.FindProperty("layers");
        for (int i = 8; i < capas.arraySize; i++)
        {
            SerializedProperty c = capas.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(c.stringValue)) continue;
            c.stringValue = nombre;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[Zona Roja] Capa \"{nombre}\" creada en la posicion {i}.");
            return i;
        }
        return -1;
    }

    [MenuItem("Tools/Zona Roja/Reconstruir")]
    static void Menu()
    {
        Crear();
        Selection.activeGameObject = GameObject.Find("Zona Roja");
    }

    // ------------------------------------------------------------------

    static void Crear()
    {
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Crear Zona Roja");

        GameObject vieja = GameObject.Find("Zona Roja");
        Guardado guardado = Guardar(vieja);
        if (vieja) Undo.DestroyObjectImmediate(vieja);

        // Donde termina el pasillo rojo.
        Vector3 fin = new Vector3(62.5f, 0f, -73f), dir = Vector3.left;
        MarcaCamino marcaRoja = null;
        GameObject zp = GameObject.Find("Zona_Principal");
        if (zp)
        {
            Transform pasillo = zp.transform.Find("Pasillos/Pasillo - Proximamente");
            if (pasillo)
            {
                dir = pasillo.forward; dir.y = 0f; dir.Normalize();
                Transform finPasillo = pasillo.Find("Entrada Zona Roja (pendiente)");
                fin = finPasillo ? finPasillo.position : pasillo.position + dir * 25f;
            }
            foreach (MarcaCamino m in zp.GetComponentsInChildren<MarcaCamino>(true))
                if (m.nombreDestino == "Proximamente") marcaRoja = m;
        }

        Mats mats = CrearMateriales();

        // Raiz: centro del faro a nivel del piso 3. Eje X local = hacia el pasillo.
        var raiz = new GameObject("Zona Roja");
        Undo.RegisterCreatedObjectUndo(raiz, "Crear Zona Roja");
        Quaternion rot = Quaternion.FromToRotation(Vector3.right, -dir);
        raiz.transform.SetPositionAndRotation(fin - rot * new Vector3(DistEntrada, 0f, 0f), rot);
        new GameObject(Version).transform.SetParent(raiz.transform, false);

        // Ancla para Tools > Zona Principal > Alinear zonas y crear pasillos (eje azul hacia afuera).
        var ancla = new GameObject("Entrada Pasillo");
        ancla.transform.SetParent(raiz.transform, false);
        ancla.transform.localPosition = new Vector3(DistEntrada, 0f, 0f);
        ancla.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        Transform faro = Hijo(raiz.transform, "Faro");
        ConstruirFaro(faro, mats);

        float ySuelo = -2f * AltoPiso; // el piso 1 del faro esta al nivel del suelo de la isla
        // El faro queda en el centro del borde ESTE de la isla (lado del pasillo).
        Vector3 centroIsla = new Vector3(-(MitadIsla - 1f), ySuelo, -DesplazamientoSur);
        var terreno = new Terreno(centroIsla);
        ConstruirIsla(raiz.transform, terreno, mats);
        ConstruirBosque(raiz.transform, terreno, ySuelo, mats);
        MoverTiendaLejos(raiz.transform, terreno);

        if (marcaRoja)
        {
            Undo.RecordObject(marcaRoja, "Asignar zona roja");
            marcaRoja.zonaDestino = raiz.transform;
            EditorUtility.SetDirty(marcaRoja);
        }

        Restaurar(guardado, raiz.transform, terreno);
        ConfigurarLuzFaro(raiz.transform);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Zona Roja] Creada. Entrada (piso 3 del faro) en ({fin.x:0.#}, {fin.y:0.#}, {fin.z:0.#}). Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ Faro

    static void ConstruirFaro(Transform faro, Mats m)
    {
        float yP1 = -2f * AltoPiso, yP2 = -AltoPiso, yP3 = 0f;

        // Base de roca bajo el faro (llega hasta debajo del mar).
        Cilindro(faro, "Base de roca", new Vector3(0f, yP1 - 2.5f, 0f), RadioFaro + 2.5f, 5f, m.roca, true);

        // ---- Piso 1 (suelo de la isla): puerta hacia la isla ----
        Transform p1 = Hijo(faro, "Piso 1");
        Cilindro(p1, "Suelo", new Vector3(0f, yP1 - 0.15f, 0f), RadioInterior + 0.05f, 0.3f, m.madera, true);
        Anillo(p1, "Muro", yP1, AltoPiso, RadioInterior, RadioFaro, 36, new[] { 180f }, m.blanco);
        Escalera(p1, "Escalera de caracol (1 a 2)", yP1, InicioEscalera1a2, ExtIn, ExtOut, m.metal);

        // ---- Piso 2 ----
        Transform p2 = Hijo(faro, "Piso 2");
        SueloConHueco(p2, yP2, InicioEscalera1a2, ExtIn, ExtOut, m.madera);
        Anillo(p2, "Muro", yP2, AltoPiso, RadioInterior, RadioFaro, 36, new float[0], m.rojo);
        Escalera(p2, "Escalera de caracol (2 a 3)", yP2, InicioEscalera2a3, IntIn, IntOut, m.metal);

        // ---- Piso 3: sala de la linterna, entrada desde el pasillo ----
        Transform p3 = Hijo(faro, "Piso 3 - Linterna");
        SueloConHueco(p3, yP3, InicioEscalera2a3, IntIn, IntOut, m.madera);
        float k = AltoPiso / 4f, kr = RadioFaro / 7f; // detalles proporcionales (el diseno original era de 4 m x 7 m)
        float murete = 1.2f, vidrioHasta = AltoPiso * 0.9f;
        Anillo(p3, "Murete", yP3, murete, RadioInterior, RadioFaro, 36, new[] { 0f }, m.blanco);
        Anillo(p3, "Vidrio", yP3 + murete, vidrioHasta - murete, RadioInterior + 0.2f, RadioFaro - 0.2f, 36, new[] { 0f }, m.vidrio);
        Anillo(p3, "Marco superior", yP3 + vidrioHasta, AltoPiso - vidrioHasta, RadioInterior, RadioFaro, 36, new[] { 0f }, m.blanco);

        // Techo conico.
        Transform techo = Hijo(p3, "Techo");
        Cilindro(techo, "Alero", new Vector3(0f, AltoPiso + 0.15f, 0f), RadioFaro + 0.5f, 0.3f, m.rojo, true);
        float[] radios = { 6f, 4.6f, 3.2f, 1.8f };
        for (int i = 0; i < radios.Length; i++)
            Cilindro(techo, "Cupula " + i, new Vector3(0f, AltoPiso + (0.6f + i * 0.6f) * k, 0f), radios[i] * kr, 0.6f * k, m.rojo, false);
        Esfera(techo, "Remate", new Vector3(0f, AltoPiso + 3.1f * k, 0f), 0.5f * k, m.metal);

        // La luz giratoria, APAGADA, en el centro (enfrente de la entrada).
        Transform luz = Hijo(p3, "Luz del faro (apagada)");
        Cilindro(luz, "Pedestal", new Vector3(0f, 0.5f * k, 0f), 0.9f * k, 1f * k, m.metal, true);
        Cilindro(luz, "Base giratoria", new Vector3(0f, 1.1f * k, 0f), 1.1f * k, 0.2f * k, m.metal, false);
        Cilindro(luz, "Lente (apagada)", new Vector3(0f, 1.9f * k, 0f), 0.9f * k, 1.4f * k, m.lente, false);
        Cilindro(luz, "Tapa", new Vector3(0f, 2.7f * k, 0f), 1.0f * k, 0.2f * k, m.metal, false);
        var foco = new GameObject("Foco (desactivado)");
        foco.transform.SetParent(luz, false);
        foco.transform.localPosition = new Vector3(0f, 1.9f * k, 0f);
        foco.transform.localRotation = Quaternion.LookRotation(Vector3.right);
        var l = foco.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = 120f;
        l.spotAngle = 25f;
        l.intensity = 100000f;
        l.color = new Color(1f, 0.95f, 0.8f);
        l.enabled = false; // apagada
    }

    // Suelo de un piso con el hueco por donde sale la escalera que viene de abajo.
    // rIn/rOut: anillo por donde sube la escalera de abajo.
    static void SueloConHueco(Transform padre, float y, float inicioEscaleraAbajo, float rIn, float rOut, Material mat)
    {
        Transform suelo = Hijo(padre, "Suelo");
        Cilindro(suelo, "Centro", new Vector3(0f, y - 0.15f, 0f), rIn + 0.02f, 0.3f, mat, true);

        // El hueco cubre el ultimo tramo de la escalera de abajo (donde no hay altura para la cabeza).
        float huecoDesde = inicioEscaleraAbajo + GiroEscalera * (1f - AlturaLibre / AltoPiso);
        float huecoHasta = inicioEscaleraAbajo + GiroEscalera;
        int sectores = 36;
        for (int i = 0; i < sectores; i++)
        {
            float a = (i + 0.5f) * 360f / sectores;
            if (!EnArco(a, huecoDesde - 4f, huecoHasta + 2f))
                Sector(suelo, "Sector " + i, y - 0.15f, 0.3f, rIn, rOut, a, 360f / sectores, mat, true);
            // Lo que queda entre la escalera y el muro va completo.
            if (rOut < RadioInterior - 0.01f)
                Sector(suelo, "Borde " + i, y - 0.15f, 0.3f, rOut, RadioInterior + 0.05f, a, 360f / sectores, mat, true);
        }
    }

    // Escalera de caracol de un piso al siguiente: escalones visibles + rampa invisible para caminar.
    static void Escalera(Transform padre, string nombre, float y0, float angInicio, float rIn, float rOut, Material mat)
    {
        Transform esc = Hijo(padre, nombre);
        float paso = GiroEscalera / Escalones, subida = AltoPiso / Escalones;
        for (int i = 0; i < Escalones; i++)
        {
            float a = angInicio + (i + 0.5f) * paso;
            float alto = y0 + (i + 1) * subida;
            Sector(esc, "Escalon " + (i + 1), alto - subida * 0.5f, subida, rIn, rOut, a, paso + 2f, mat, false);
        }
        Rampa(esc, "Rampa (colision)", y0, angInicio, rIn, rOut);
    }

    static void Rampa(Transform padre, string nombre, float y0, float angInicio, float rIn, float rOut)
    {
        int n = 64;
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            float a = (angInicio + f * GiroEscalera) * Mathf.Deg2Rad;
            float y = y0 + f * AltoPiso;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * rIn + Vector3.up * y);
            v.Add(dir * rOut + Vector3.up * y);
            if (i > 0)
            {
                int k = i * 2;
                // Dos caras (arriba y abajo) para que la colision funcione desde ambos lados.
                t.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 });
                t.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k });
            }
        }
        var malla = new Mesh { name = nombre };
        malla.SetVertices(v);
        malla.SetTriangles(t, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        malla = GuardarMalla(malla, padre.parent.name.Replace(" ", "") + "_" + nombre.Replace(" ", "").Replace("(", "").Replace(")", ""));

        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshCollider>().sharedMesh = malla;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // Muro circular hecho de bloques, con puertas (huecos) en los angulos indicados.
    static void Anillo(Transform padre, string nombre, float y, float alto, float rIn, float rOut, int n, float[] puertas, Material mat)
    {
        Transform anillo = Hijo(padre, nombre);
        for (int i = 0; i < n; i++)
        {
            float a = (i + 0.5f) * 360f / n;
            bool esPuerta = false;
            foreach (float p in puertas) if (EnArco(a, p - AnchoPuerta, p + AnchoPuerta)) esPuerta = true;
            if (esPuerta) continue;
            Sector(anillo, nombre + " " + i, y + alto * 0.5f, alto, rIn, rOut, a, 360f / n, mat, true);
        }
    }

    // Bloque con forma aproximada de "porcion" de anillo, centrado en el angulo a (grados).
    static GameObject Sector(Transform padre, string nombre, float yCentro, float alto, float rIn, float rOut, float a, float grados, Material mat, bool colision)
    {
        float rMed = (rIn + rOut) * 0.5f;
        float largo = 2f * Mathf.PI * rOut * grados / 360f + 0.05f;
        float rad = a * Mathf.Deg2Rad;
        return Caja(padre, nombre,
                    new Vector3(Mathf.Cos(rad) * rMed, yCentro, Mathf.Sin(rad) * rMed),
                    Quaternion.Euler(0f, -a, 0f),
                    new Vector3(rOut - rIn, alto, largo), mat, colision);
    }

    static bool EnArco(float a, float desde, float hasta)
    {
        float largo = hasta - desde;
        float rel = Mathf.Repeat(a - desde, 360f);
        return rel <= largo;
    }

    // ------------------------------------------------------------------ Isla, mar y lago

    // Forma de la isla y del lago (en coordenadas locales de "Zona Roja", relativo a centroIsla).
    class Terreno
    {
        public readonly Vector3 centro;   // centro de la isla (y = suelo)
        public readonly Vector2 lago;     // centro del lago (relativo al centro de la isla)
        readonly float f1, f2, f3, g1, g2;

        public Terreno(Vector3 centro)
        {
            this.centro = centro;
            var rnd = new System.Random(7);
            f1 = (float)rnd.NextDouble() * 6.28f; f2 = (float)rnd.NextDouble() * 6.28f; f3 = (float)rnd.NextDouble() * 6.28f;
            g1 = (float)rnd.NextDouble() * 6.28f; g2 = (float)rnd.NextDouble() * 6.28f;
            // Esquina suroeste (lejos del faro, que esta al este).
            lago = new Vector2(-MitadIsla * 0.5f, -MitadIsla * 0.5f);
        }

        // 0 en el centro, 1 en la orilla (cuadrado redondeado con bordes irregulares).
        public float Orilla(float x, float z)
        {
            float ang = Mathf.Atan2(z, x);
            float s = MitadIsla * (1f + 0.07f * Mathf.Sin(3f * ang + f1) + 0.04f * Mathf.Sin(5f * ang + f2) + 0.025f * Mathf.Sin(11f * ang + f3));
            float ax = Mathf.Abs(x) / s, az = Mathf.Abs(z) / s;
            return Mathf.Pow(ax * ax * ax * ax + az * az * az * az, 0.25f);
        }

        public float RadioLagoEn(float ang) => RadioLago * (1f + 0.16f * Mathf.Sin(3f * ang + g1) + 0.08f * Mathf.Sin(5f * ang + g2));

        // Altura del terreno (relativa al suelo) en (x, z) relativos al centro de la isla.
        public float Altura(float x, float z)
        {
            float q = Orilla(x, z);
            float y = 0f;
            if (q > 1f) y = -Mathf.Min(3.5f, (q - 1f) * MitadIsla / Playa * 3.5f);

            float dx = x - lago.x, dz = z - lago.y;
            float dl = Mathf.Sqrt(dx * dx + dz * dz);
            float rl = RadioLagoEn(Mathf.Atan2(dz, dx));
            float hundido = Mathf.Clamp01(Mathf.InverseLerp(rl + 7f, rl - 5f, dl));
            y -= ProfundidadLago * hundido * hundido * (3f - 2f * hundido);

            // Dentro del faro el terreno baja un poco (para que no se cruce con el suelo del piso 1).
            float fx = x + centro.x, fz = z + centro.z; // posicion relativa al faro (origen de "Zona Roja")
            if (fx * fx + fz * fz < (RadioFaro + 0.5f) * (RadioFaro + 0.5f)) y = Mathf.Min(y, -0.3f);
            return y;
        }

        public bool EnTierra(float x, float z) => Orilla(x, z) < 0.9f && Altura(x, z) > -0.05f;
    }

    static void ConstruirIsla(Transform raiz, Terreno ter, Mats m)
    {
        float ext = MitadIsla * 1.25f + Playa + 6f;
        int n = Mathf.CeilToInt(2f * ext / CeldaTerreno);
        var v = new List<Vector3>((n + 1) * (n + 1));
        var uv = new List<Vector2>((n + 1) * (n + 1));
        var t = new List<int>(n * n * 6);
        for (int iz = 0; iz <= n; iz++)
        for (int ix = 0; ix <= n; ix++)
        {
            float x = -ext + ix * CeldaTerreno, z = -ext + iz * CeldaTerreno;
            v.Add(new Vector3(x, ter.Altura(x, z), z));
            uv.Add(new Vector2(x, z) * 0.1f);
        }
        for (int iz = 0; iz < n; iz++)
        for (int ix = 0; ix < n; ix++)
        {
            int a = iz * (n + 1) + ix, b = a + 1, c = a + (n + 1), d = c + 1;
            t.AddRange(new[] { a, c, b, b, c, d });
        }
        var malla = new Mesh { name = "Isla", indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        malla.SetVertices(v);
        malla.SetUVs(0, uv);
        malla.SetTriangles(t, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        malla = GuardarMalla(malla, "Isla");

        var isla = new GameObject("Isla");
        isla.transform.SetParent(raiz, false);
        isla.transform.localPosition = ter.centro;
        isla.AddComponent<MeshFilter>().sharedMesh = malla;
        isla.AddComponent<MeshRenderer>().sharedMaterial = m.pasto;
        isla.AddComponent<MeshCollider>().sharedMesh = malla;
        GameObjectUtility.SetStaticEditorFlags(isla, StaticEditorFlags.BatchingStatic);

        // Mar alrededor (copia del agua de las Cloacas).
        var mar = GameObject.CreatePrimitive(PrimitiveType.Plane);
        mar.name = "Mar";
        Object.DestroyImmediate(mar.GetComponent<Collider>());
        mar.transform.SetParent(raiz, false);
        mar.transform.localPosition = ter.centro + Vector3.up * NivelMar;
        mar.transform.localScale = new Vector3(42f, 1f, 42f);
        if (m.agua) mar.GetComponent<Renderer>().sharedMaterial = m.agua;

        // Lago: el agua llena la cuenca hundida (la orilla la marca el propio terreno).
        Transform lago = Hijo(raiz, "Lago");
        lago.localPosition = ter.centro + new Vector3(ter.lago.x, 0f, ter.lago.y);
        // Superficie con la misma forma irregular del lago (un poco mas grande: el borde queda bajo la orilla).
        int lados = 72;
        var va = new List<Vector3> { Vector3.zero };
        var uva = new List<Vector2> { Vector2.zero };
        var ta = new List<int>();
        for (int i = 0; i < lados; i++)
        {
            float ang = i * Mathf.PI * 2f / lados;
            float rr = ter.RadioLagoEn(ang) + 4.5f;
            var p = new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
            va.Add(p);
            uva.Add(new Vector2(p.x, p.z) * 0.1f);
            ta.AddRange(new[] { 0, 1 + (i + 1) % lados, 1 + i });
        }
        var mallaAgua = new Mesh { name = "AguaLago" };
        mallaAgua.SetVertices(va);
        mallaAgua.SetUVs(0, uva);
        mallaAgua.SetTriangles(ta, 0);
        mallaAgua.RecalculateNormals();
        mallaAgua.RecalculateBounds();
        mallaAgua = GuardarMalla(mallaAgua, "AguaLago");

        var agua = new GameObject("Agua del lago");
        agua.transform.SetParent(lago, false);
        agua.transform.localPosition = new Vector3(0f, NivelLago, 0f);
        agua.AddComponent<MeshFilter>().sharedMesh = mallaAgua;
        var r = agua.AddComponent<MeshRenderer>();
        r.sharedMaterial = m.aguaLago ? m.aguaLago : m.agua;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ Bosque, notas y cubo

    static void ConstruirBosque(Transform raiz, Terreno ter, float ySuelo, Mats m)
    {
        var rnd = new System.Random(1234);
        float R() => (float)rnd.NextDouble();
        Vector3 c = ter.centro;

        // Cubo negro en la orilla del lago (lado este del lago).
        float rOrilla = ter.RadioLagoEn(0f);
        Vector2 cuboRel = ter.lago + new Vector2(rOrilla + 8f, 0f); // en la orilla plana, a ~4 m del agua
        Caja(raiz, "Objeto del lago (cubo negro)", new Vector3(c.x + cuboRel.x, ySuelo + 0.75f, c.z + cuboRel.y),
             Quaternion.identity, Vector3.one * 1.5f, m.negro, true);

        // 4 notas (esferas) repartidas en medio del bosque.
        Transform notas = Hijo(raiz, "Notas");
        Vector2[] posNotas =
        {
            new Vector2(26f, -22f), new Vector2(-6f, 30f), new Vector2(-34f, 6f), new Vector2(12f, 4f),
        };
        for (int i = 0; i < posNotas.Length; i++) posNotas[i] *= MitadIsla / 64f;
        for (int i = 0; i < posNotas.Length; i++)
            Esfera(notas, "Nota " + (i + 1), new Vector3(c.x + posNotas[i].x, ySuelo + 1f, c.z + posNotas[i].y), 0.35f, m.nota);

        // Arboles: el modelo "tree 1" (pino), separados para que el bosque no se sienta pegado.
        Transform bosque = Hijo(raiz, "Bosque");
        Mesh pino = MallaPino(out float radioTronco);
        Material[] matsPino = { Lit("ArbolTronco", new Color(0.24f, 0.15f, 0.09f)), Lit("ArbolHojas", new Color(0.07f, 0.19f, 0.08f)) };
        foreach (Material mp in matsPino) mp.enableInstancing = true;
        Vector2 faro = new Vector2(-c.x, -c.z); // el faro esta en el origen de "Zona Roja"
        var puestos = new List<Vector2>();
        int intentos = 0;
        while (puestos.Count < Arboles && intentos++ < Arboles * 80)
        {
            var p = new Vector2((R() * 2f - 1f) * MitadIsla * 1.1f, (R() * 2f - 1f) * MitadIsla * 1.1f);
            if (!ter.EnTierra(p.x, p.y)) continue;                // fuera de la isla o en el lago
            if ((p - faro).magnitude < RadioFaro + 10f) continue;  // faro y su puerta
            if ((p - cuboRel).magnitude < 4f) continue;
            bool cerca = false;
            foreach (Vector2 n in posNotas) if ((p - n).magnitude < 3f) { cerca = true; break; }
            if (!cerca) foreach (Vector2 q in puestos) if ((q - p).sqrMagnitude < SeparacionArboles * SeparacionArboles) { cerca = true; break; }
            if (cerca) continue;
            puestos.Add(p);

            float escala = 0.8f + R() * 0.45f; // 8.8 a 13.7 m de alto
            var arbol = new GameObject("Arbol " + puestos.Count);
            arbol.transform.SetParent(bosque, false);
            arbol.transform.localPosition = new Vector3(c.x + p.x, ySuelo + ter.Altura(p.x, p.y) - 0.15f, c.z + p.y);
            arbol.transform.localRotation = Quaternion.Euler(0f, R() * 360f, 0f);
            arbol.transform.localScale = Vector3.one * escala;
            arbol.AddComponent<MeshFilter>().sharedMesh = pino;
            arbol.AddComponent<MeshRenderer>().sharedMaterials = matsPino;
            var tronco = arbol.AddComponent<CapsuleCollider>(); // solo el tronco choca (las ramas quedan por encima del jugador)
            tronco.radius = radioTronco;
            tronco.height = 5f;
            tronco.center = new Vector3(0f, 2.5f, 0f);
            GameObjectUtility.SetStaticEditorFlags(arbol, StaticEditorFlags.BatchingStatic);
        }
        Debug.Log($"[Zona Roja] {puestos.Count} arboles.");
    }

    // ------------------------------------------------------------------ Mover la Tienda

    // Si la Tienda (y su sotano, escaleras, niebla y linterna) queda encima de la isla,
    // se mueve todo el grupo junto hacia el norte (+Z) hasta que quede libre.
    static void MoverTiendaLejos(Transform raiz, Terreno ter)
    {
        var grupo = new List<Transform>();
        foreach (GameObject r in raiz.gameObject.scene.GetRootGameObjects())
            if (System.Array.IndexOf(GrupoTienda, r.name) >= 0) grupo.Add(r.transform);
        if (grupo.Count == 0) return;

        // Huella de la isla (con playa) en el mundo.
        float ext = MitadIsla * 1.14f + Playa;
        Vector3 cMundo = raiz.TransformPoint(ter.centro);
        var isla = new Bounds(new Vector3(cMundo.x, 0f, cMundo.z), new Vector3(ext * 2f + 10f, 1f, ext * 2f + 10f));

        Bounds bg = default; bool hay = false;
        foreach (Transform t in grupo)
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!hay) { bg = r.bounds; hay = true; } else bg.Encapsulate(r.bounds);
            }
        if (!hay) return;

        float mover = 0f;
        while (bg.min.z + mover < isla.max.z && bg.max.x > isla.min.x && bg.min.x < isla.max.x
               && bg.max.z + mover > isla.min.z && mover < 400f)
            mover += 5f;
        // Ya no se mueve sola (la Tienda tiene cabinas, teletransporte y tutorial): solo se avisa.
        if (mover > 0f)
            Debug.LogWarning($"[Zona Roja] La isla toca la Tienda/sotano: habria que moverlos {mover} m al norte o achicar MitadIsla.");
    }

    // ------------------------------------------------------------------ Pino (modelo tree 1)

    // Une las 8 piezas del modelo en una malla de 2 submallas (tronco y hojas), con la base en y = 0 y ~11 m de alto.
    static Mesh MallaPino(out float radioTronco)
    {
        const float AltoBase = 11f;
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloArbol);
        radioTronco = 0.6f;
        if (!modelo) { Debug.LogWarning("[Zona Roja] No encuentro " + ModeloArbol); return null; }
        var inst = Object.Instantiate(modelo);
        try
        {
            MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>();
            Bounds total = default; bool hay = false;
            MeshFilter troncoMf = null;
            foreach (MeshFilter mf in mfs)
            {
                Bounds b = mf.GetComponent<Renderer>().bounds;
                if (!hay) { total = b; hay = true; } else total.Encapsulate(b);
                if (!troncoMf || b.min.y < troncoMf.GetComponent<Renderer>().bounds.min.y) troncoMf = mf; // el tronco llega hasta abajo
            }
            float esc = AltoBase / total.size.y;
            Matrix4x4 ajuste = Matrix4x4.Scale(Vector3.one * esc) * Matrix4x4.Translate(new Vector3(-total.center.x, -total.min.y, -total.center.z));
            var tronco = new List<CombineInstance>();
            var hojas = new List<CombineInstance>();
            foreach (MeshFilter mf in mfs)
                (mf == troncoMf ? tronco : hojas).Add(new CombineInstance { mesh = mf.sharedMesh, transform = ajuste * mf.transform.localToWorldMatrix });

            var mT = new Mesh(); mT.CombineMeshes(tronco.ToArray(), true, true);
            var mH = new Mesh(); mH.CombineMeshes(hojas.ToArray(), true, true);
            var pino = new Mesh { name = "Pino" };
            pino.CombineMeshes(new[] { new CombineInstance { mesh = mT, transform = Matrix4x4.identity }, new CombineInstance { mesh = mH, transform = Matrix4x4.identity } }, false, true);
            pino.RecalculateBounds();
            Object.DestroyImmediate(mT); Object.DestroyImmediate(mH);

            Bounds bt = troncoMf.GetComponent<Renderer>().bounds;
            radioTronco = Mathf.Max(0.25f, Mathf.Min(bt.extents.x, bt.extents.z) * esc * 0.7f);
            return GuardarMalla(pino, "Pino");
        }
        finally { Object.DestroyImmediate(inst); }
    }

    // ------------------------------------------------------------------ Lo que depende de la isla

    // Al reconstruir se conserva el foco del faro (intensidad, etc. cambiados a mano) y el NavMesh de "Enemigos y Zonas";
    // despues se re-hornea solo el NavMesh de la isla, se ajusta su trigger de Volume y se reubican su enemigo y sus baterias.
    class Guardado
    {
        public string luz, luzUrp, superficie;
        public readonly Dictionary<string, string> modificadores = new Dictionary<string, string>();
    }

    const string RutaFoco = "Faro/Piso 3 - Linterna/Luz del faro (apagada)/Foco (desactivado)";

    static Guardado Guardar(GameObject vieja)
    {
        var g = new Guardado();
        if (!vieja) return g;
        Transform foco = vieja.transform.Find(RutaFoco);
        if (foco && foco.TryGetComponent(out Light l)) g.luz = EditorJsonUtility.ToJson(l);
        if (foco && foco.TryGetComponent(out UniversalAdditionalLightData d)) g.luzUrp = EditorJsonUtility.ToJson(d);
        if (vieja.TryGetComponent(out NavMeshSurface s)) g.superficie = EditorJsonUtility.ToJson(s);
        foreach (NavMeshModifier mod in vieja.GetComponentsInChildren<NavMeshModifier>(true))
            g.modificadores[RutaRel(mod.transform, vieja.transform)] = EditorJsonUtility.ToJson(mod);
        return g;
    }

    static string RutaRel(Transform t, Transform raiz) => t == raiz ? "" : (t.parent == raiz ? t.name : RutaRel(t.parent, raiz) + "/" + t.name);

    static void Restaurar(Guardado g, Transform raiz, Terreno ter)
    {
        Transform foco = raiz.Find(RutaFoco);
        if (foco && g.luz != null) EditorJsonUtility.FromJsonOverwrite(g.luz, foco.GetComponent<Light>());
        if (foco && g.luzUrp != null)
        {
            var urp = foco.GetComponent<UniversalAdditionalLightData>();
            if (!urp) urp = foco.gameObject.AddComponent<UniversalAdditionalLightData>();
            EditorJsonUtility.FromJsonOverwrite(g.luzUrp, urp);
        }
        if (g.superficie == null) return; // todavia no hay "Enemigos y Zonas": su script lo hara todo despues

        // NavMesh solo de la isla (mismos ajustes que puso EnemigosZonasSetup)
        var surface = raiz.gameObject.AddComponent<NavMeshSurface>();
        EditorJsonUtility.FromJsonOverwrite(g.superficie, surface);
        surface.navMeshData = null;
        foreach (var kv in g.modificadores)
        {
            Transform t = kv.Key == "" ? raiz : raiz.Find(kv.Key);
            if (t) EditorJsonUtility.FromJsonOverwrite(kv.Value, t.gameObject.AddComponent<NavMeshModifier>());
        }
        Physics.SyncTransforms();
        var guardarNav = typeof(EnemigosZonasSetup).GetMethod("GuardarNavMesh", BindingFlags.NonPublic | BindingFlags.Static);
        if (guardarNav != null) guardarNav.Invoke(null, new object[] { surface });
        else surface.BuildNavMesh();

        // Trigger del Volume de la zona: caja de los colliders de la isla (recortada contra la Zona Principal)
        GameObject trig = GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Zona Roja");
        var limites = typeof(EnemigosZonasSetup).GetMethod("LimitesColliders", BindingFlags.NonPublic | BindingFlags.Static);
        var recortar = typeof(EnemigosZonasSetup).GetMethod("RecortarContraZonaPrincipal", BindingFlags.NonPublic | BindingFlags.Static);
        if (trig && limites != null && recortar != null)
        {
            var args = new object[] { raiz, null };
            if ((bool)limites.Invoke(null, args))
            {
                Bounds b = (Bounds)args[1];
                b.Expand(new Vector3(0f, 4f, 0f));
                var args2 = new object[] { b };
                recortar.Invoke(null, args2);
                b = (Bounds)args2[0];
                Undo.RecordObject(trig.transform, "Trigger Zona Roja");
                trig.transform.position = b.center;
                var box = trig.GetComponent<BoxCollider>();
                Undo.RecordObject(box, "Trigger Zona Roja");
                box.center = Vector3.zero;
                box.size = b.size;
            }
        }

        // Enemigo: misma direccion respecto al faro, sobre el NavMesh nuevo
        Transform faro = raiz.Find("Faro");
        Transform grupo = EnemigosEnSuNivelSetup.Grupo("Zona Roja");
        if (grupo && faro)
            foreach (Transform e in grupo)
            {
                Vector3 d = e.position - faro.position; d.y = 0f;
                if (d.sqrMagnitude < 0.01f) d = Vector3.left;
                float dist = Mathf.Clamp(d.magnitude, RadioFaro + 8f, RadioFaro + 30f);
                Vector3 p = faro.position + d.normalized * dist + Vector3.down * 2f * AltoPiso;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 25f, NavMesh.AllAreas)) continue;
                float alto = e.TryGetComponent(out NavMeshAgent agent) ? agent.baseOffset : 0f;
                Undo.RecordObject(e, "Enemigo Zona Roja");
                e.position = hit.position + Vector3.up * alto;
            }

        // Baterias de la zona: en tierra firme, lejos del faro, del lago y entre si
        GameObject bats = GameObject.Find("Menu y Progreso/Baterias");
        if (bats)
        {
            var rnd = new System.Random(77);
            var puestas = new List<Vector3>();
            Vector2 faroRel = new Vector2(-ter.centro.x, -ter.centro.z);
            foreach (Transform b in bats.transform)
            {
                if (!b.name.StartsWith("Bateria Zona Roja")) continue;
                for (int i = 0; i < 400; i++)
                {
                    var q = new Vector2(((float)rnd.NextDouble() * 2f - 1f) * MitadIsla * 0.85f, ((float)rnd.NextDouble() * 2f - 1f) * MitadIsla * 0.85f);
                    if (!ter.EnTierra(q.x, q.y)) continue;
                    if ((q - faroRel).magnitude < RadioFaro + 6f || (q - ter.lago).magnitude < RadioLago + 8f) continue;
                    Vector3 w = raiz.TransformPoint(ter.centro + new Vector3(q.x, ter.Altura(q.x, q.y), q.y));
                    bool lejos = true;
                    foreach (Vector3 o in puestas) if ((o - w).magnitude < 30f) lejos = false;
                    if (!lejos) continue;
                    // Que no quede dentro de un tronco
                    if (Physics.CheckSphere(w + Vector3.up * 1f, 0.8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Raycast(w + Vector3.up * 3f, Vector3.down, out RaycastHit suelo, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    puestas.Add(w);
                    Undo.RecordObject(b, "Bateria Zona Roja");
                    b.position = suelo.point + Vector3.up * 0.1f;
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ Materiales y utilidades

    class Mats
    {
        public Material blanco, rojo, roca, madera, metal, vidrio, lente, pasto, tierra, arbol, nota, negro, agua, aguaLago;
    }

    static Mats CrearMateriales()
    {
        CrearCarpeta(Carpeta);
        CrearCarpeta(CarpetaMat);
        var m = new Mats
        {
            blanco = Lit("FaroBlanco", new Color(0.85f, 0.85f, 0.8f)),
            rojo = Lit("FaroRojo", new Color(0.55f, 0.08f, 0.07f)),
            roca = Lit("Roca", new Color(0.28f, 0.28f, 0.3f)),
            madera = Lit("Madera", new Color(0.33f, 0.23f, 0.14f)),
            metal = Lit("Metal", new Color(0.2f, 0.2f, 0.22f), 0.8f),
            vidrio = Lit("Vidrio", new Color(0.6f, 0.75f, 0.8f, 0.25f), 0f, true),
            lente = Lit("LenteApagada", new Color(0.12f, 0.12f, 0.1f, 0.7f), 0f, true),
            pasto = Lit("Pasto", new Color(0.18f, 0.27f, 0.12f)),
            tierra = Lit("Tierra", new Color(0.22f, 0.17f, 0.11f)),
            arbol = Lit("ArbolBloque", new Color(0.09f, 0.2f, 0.08f)),
            nota = Lit("Nota", new Color(1f, 0.85f, 0.2f), 0f, false, new Color(1f, 0.8f, 0.2f) * 1.5f),
            negro = Lit("Negro", Color.black),
        };

        // Copia del material de agua de las Cloacas (mismo shader, sin modificarlo).
        const string origenAgua = "Assets/Shaders/Agua/prueba/Simple Water/agua prueba.mat";
        string destinoAgua = CarpetaMat + "/AguaIsla.mat";
        if (!File.Exists(destinoAgua) && File.Exists(origenAgua)) AssetDatabase.CopyAsset(origenAgua, destinoAgua);
        m.agua = AssetDatabase.LoadAssetAtPath<Material>(destinoAgua);
        // Otra copia para el lago, asi se puede ajustar distinto que el mar.
        string destinoLago = CarpetaMat + "/AguaLago.mat";
        if (!File.Exists(destinoLago) && File.Exists(origenAgua)) AssetDatabase.CopyAsset(origenAgua, destinoLago);
        m.aguaLago = AssetDatabase.LoadAssetAtPath<Material>(destinoLago);
        return m;
    }

    static Material Lit(string nombre, Color color, float metalico = 0f, bool transparente = false, Color emision = default)
    {
        string ruta = CarpetaMat + "/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(shader) { name = nombre };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metalico);
        mat.SetFloat("_Smoothness", metalico > 0f ? 0.6f : 0.2f);
        if (transparente)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        if (emision != default)
        {
            mat.SetColor("_EmissionColor", emision);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    static Mesh GuardarMalla(Mesh malla, string nombre)
    {
        CrearCarpeta(Carpeta);
        CrearCarpeta(CarpetaMallas);
        string ruta = CarpetaMallas + "/" + nombre + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta)) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
        return malla;
    }

    static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static GameObject Caja(Transform padre, string nombre, Vector3 pos, Quaternion rot, Vector3 escala, Material mat, bool colision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = escala;
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Cilindro(Transform padre, string nombre, Vector3 pos, float radio, float alto, Material mat, bool colision)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(radio * 2f, alto * 0.5f, radio * 2f);
        // El cilindro primitivo trae una capsula: se cambia por su malla real.
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (colision) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Esfera(Transform padre, string nombre, Vector3 pos, float radio, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * radio * 2f;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
