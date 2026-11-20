using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Salida de las Cloacas con la llave (la llave ya la pone EnemigosZonasSetup en la mano de una estatua al azar):
// - "Reja Salida": reja de hierro EN LA PUERTA del muro de las Cloacas (la abertura frente a "Entrada Pasillo").
//   Se busca sola con rayos a lo largo del muro. Empieza abierta (hundida en el piso) y se cierra cuando el jugador
//   ya esta adentro, entre las paredes. Asi no se puede rodear: es la unica salida del recinto.
// - "Cerradura": poste con una cerradura al lado de la puerta, del lado de adentro (ReceptorObjeto que solo acepta
//   la "Llave Cloacas"). Al entregarle la llave se abre la reja y se completa la zona (ProgresoCaminos):
//   se abre la puerta de la siguiente zona.
// Todo dentro de "Cloacas/Salida Cloacas" (SalidaConLlave).
// Se aplica sola (y se rehace una vez si es de la version vieja, la reja de 8 m en el borde del agua) y se puede
// rehacer con Tools > Cloacas > Salida con llave. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class CloacasSalidaSetup
{
    const string Nombre = "Salida Cloacas";
    // Marca de la version que pone la reja en la puerta del muro
    const string Version = "_salida en la puerta v2";
    // Si no se encuentra la puerta: reja en la entrada del pasillo (8 m de ancho, paredes de 4.25 m, ver PasillosSetup)
    const float AnchoPasillo = 8.1f, AltoPasillo = 4.25f, Grosor = 0.25f;
    // Metros hacia adentro de la entrada donde queda la reja si no se encuentra la puerta
    const float Adentro = 1f;

    static CloacasSalidaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find("Cloacas");
        if (!c || !c.transform.Find("Entrada Pasillo") || !Object.FindFirstObjectByType<LlaveAleatoria>()) return;
        Transform vieja = c.transform.Find(Nombre);
        if (vieja && vieja.Find(Version)) return;
        if (vieja)
        {
            Debug.Log("[Cloacas] Rehaciendo la salida: la reja pasa a la puerta del muro.", c);
            Undo.DestroyObjectImmediate(vieja.gameObject);
        }
        Aplicar(c.transform);
    }

    [MenuItem("Tools/Cloacas/Salida con llave")]
    static void Menu()
    {
        GameObject c = GameObject.Find("Cloacas");
        if (!c) { EditorUtility.DisplayDialog("Cloacas", "No se encontro \"Cloacas\".", "OK"); return; }
        Transform vieja = c.transform.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        Aplicar(c.transform);
    }

    static void Aplicar(Transform cloacas)
    {
        Transform entrada = cloacas.Find("Entrada Pasillo");
        LlaveAleatoria llave = Object.FindFirstObjectByType<LlaveAleatoria>();
        if (!entrada || !llave) { Debug.LogWarning("[Cloacas] Falta \"Cloacas/Entrada Pasillo\" o la llave (LlaveAleatoria).", cloacas); return; }

        // El eje azul de "Entrada Pasillo" apunta hacia afuera de la zona
        Vector3 fuera = entrada.forward; fuera.y = 0f; fuera.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, fuera);
        Quaternion giro = Quaternion.LookRotation(fuera, Vector3.up);

        // Puerta del muro (la abertura frente a la entrada del pasillo)
        Vector3 centro;
        float ancho, alto;
        bool enPuerta = BuscarPuerta(cloacas, entrada.position, fuera, lado, out centro, out ancho, out alto);
        if (!enPuerta)
        {
            Debug.LogWarning("[Cloacas] No encontre la puerta en el muro frente a \"Entrada Pasillo\": la reja va en la entrada del pasillo.", cloacas);
            centro = entrada.position - fuera * Adentro;
            ancho = AnchoPasillo;
            alto = AltoPasillo;
        }

        // Raiz sin escala (los desplazamientos de la reja son en metros)
        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Salida Cloacas");
        raiz.transform.SetParent(cloacas, true);
        raiz.transform.SetPositionAndRotation(centro, giro);
        Vector3 s = cloacas.lossyScale;
        raiz.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        new GameObject(Version).transform.SetParent(raiz.transform, false);

        // ---- Reja: hoja de barrotes de hierro + travesanos (cerrada = en su sitio; abierta = hundida en el piso)
        Material hierro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RejaHierro.mat");
        var reja = new GameObject("Reja Salida");
        reja.transform.SetParent(raiz.transform, false);
        reja.transform.localPosition = new Vector3(0f, alto * 0.5f, 0f);
        int barrotes = Mathf.Max(3, Mathf.RoundToInt(ancho / 0.22f) + 1);
        for (int i = 0; i < barrotes; i++)
        {
            float x = Mathf.Lerp(-ancho * 0.5f + 0.06f, ancho * 0.5f - 0.06f, i / (float)(barrotes - 1));
            Pieza(reja.transform, "Barrote " + (i + 1), new Vector3(x, 0f, 0f), new Vector3(0.07f, alto, 0.07f), hierro, false);
        }
        foreach (float y in new[] { -alto * 0.5f + 0.2f, 0f, alto * 0.5f - 0.1f })
            Pieza(reja.transform, "Travesano", new Vector3(0f, y, 0f), new Vector3(ancho, 0.1f, 0.12f), hierro, false);
        // Un solo collider para toda la reja (no deja pasar al jugador ni a las estatuas)
        var col = reja.AddComponent<BoxCollider>();
        col.size = new Vector3(ancho, alto, Grosor);
        var puerta = reja.AddComponent<PuertaDeslizante>();
        puerta.abiertaAlIniciar = true;
        puerta.desplazamiento = new Vector3(0f, -(alto + 0.1f), 0f);
        puerta.duracion = 0.6f;

        // ---- Cerradura: poste junto a la puerta, del lado de adentro, con la cerradura mirando a la zona
        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CloacaMetal.mat");
        Material oro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Llave Cloacas.mat");
        var cerr = new GameObject("Cerradura");
        cerr.transform.SetParent(raiz.transform, false);
        cerr.transform.localPosition = PosicionCerradura(raiz.transform, ancho, enPuerta);
        cerr.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // mirando hacia adentro de la zona
        Pieza(cerr.transform, "Poste", new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 1.1f, 0.22f), metal, true);
        Pieza(cerr.transform, "Caja", new Vector3(0f, 1.25f, 0f), new Vector3(0.42f, 0.42f, 0.3f), metal, true);
        Pieza(cerr.transform, "Bocallave", new Vector3(0f, 1.25f, 0.16f), new Vector3(0.07f, 0.15f, 0.03f), oro, false);
        var mano = new GameObject("Punto Llave").transform;
        mano.SetParent(cerr.transform, false);
        mano.localPosition = new Vector3(0f, 1.25f, 0.2f);
        // Luz tenue para encontrarla en la oscuridad
        var luz = new GameObject("Luz Cerradura").AddComponent<Light>();
        luz.transform.SetParent(cerr.transform, false);
        luz.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.75f, 0.35f);
        luz.range = 3.5f;
        luz.intensity = 2f;

        var receptor = cerr.AddComponent<ReceptorObjeto>();
        receptor.objetoEsperado = llave.GetComponent<Grabbable>();
        receptor.prompt = "Usar llave";
        receptor.puntoMano = mano;
        receptor.alinearConMano = true;
        // El raycast de interaccion le pega a la caja o al poste (colliders hijos: GetComponentInParent)

        // ---- Logica
        var salida = raiz.AddComponent<SalidaConLlave>();
        salida.zona = "Cloacas";
        salida.reja = puerta;
        salida.cerradura = receptor;
        salida.recinto = enPuerta ? cloacas : null;
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Cloacas") salida.area = z;
        salida.sonidoCerrar = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sonidos/SFX/RejaCae.wav");
        salida.sonidoAbrir = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/puerta abriendose.wav");

        // La llave solo elige estatuas que esten dentro de las paredes
        if (enPuerta && llave.recinto != cloacas) { Undo.RecordObject(llave, "Salida Cloacas"); llave.recinto = cloacas; }

        EditorSceneManager.MarkSceneDirty(cloacas.gameObject.scene);
        Debug.Log("[Cloacas] Salida con llave creada " + (enPuerta ? "en la puerta del muro (" + ancho.ToString("0.0#") + " m de ancho)" : "en la entrada del pasillo") +
                  ". Guarda la escena (Ctrl+S).", raiz);
    }

    // ----------------------------------------------------------------- Puerta

    // Barre con rayos paralelos (hacia adentro de la zona) a lo largo del muro que da a la entrada del pasillo.
    // Casi todos chocan con el muro a la misma distancia; los que pasan de largo marcan la abertura de la puerta.
    static bool BuscarPuerta(Transform cloacas, Vector3 entrada, Vector3 fuera, Vector3 lado,
                             out Vector3 centro, out float ancho, out float alto)
    {
        centro = default; ancho = 0f; alto = 0f;
        Physics.SyncTransforms();
        Vector3 dentro = -fuera;
        Vector3 baseRayos = entrada + Vector3.up * 1f;
        const float paso = 0.05f, rango = 8f, largo = 15f;

        int n = Mathf.RoundToInt(rango * 2f / paso) + 1;
        var dist = new float[n];
        var conocidas = new List<float>();
        for (int i = 0; i < n; i++)
        {
            float sl = -rango + i * paso;
            dist[i] = Rayo(cloacas, baseRayos + lado * sl, dentro, largo);
            if (dist[i] < largo) conocidas.Add(dist[i]);
        }
        if (conocidas.Count < n / 3) return false;
        conocidas.Sort();
        float muro = conocidas[conocidas.Count / 2];   // la mayoria de los rayos chocan con el muro

        // Tramos seguidos de rayos que pasan el muro; el mas cercano a la entrada con ancho de puerta
        float mejorCentro = 0f, mejorAncho = 0f, mejorDist = float.MaxValue;
        int inicio = -1;
        for (int i = 0; i <= n; i++)
        {
            bool hueco = i < n && dist[i] > muro + 1f;
            if (hueco && inicio < 0) inicio = i;
            if (hueco || inicio < 0) continue;
            float a = (i - inicio) * paso;
            float c = -rango + (inicio + i - 1) * 0.5f * paso;
            inicio = -1;
            if (a < 0.7f || a > 10f) continue;
            if (Mathf.Abs(c) < mejorDist) { mejorDist = Mathf.Abs(c); mejorCentro = c; mejorAncho = a; }
        }
        if (mejorAncho <= 0f) return false;

        // La reja queda en la cara del muro, un poco hacia adentro, tapando toda la abertura
        ancho = mejorAncho + paso + 0.06f;
        centro = entrada + lado * mejorCentro + dentro * (muro + 0.12f);
        // Alto: hasta el dintel (o la altura de las paredes del pasillo si no hay)
        alto = Physics.Raycast(centro + Vector3.up * 0.3f, Vector3.up, out RaycastHit h, AltoPasillo, ~0, QueryTriggerInteraction.Ignore)
            ? h.distance + 0.3f + 0.05f : AltoPasillo;
        return true;
    }

    // Distancia hasta lo primero del recinto que choque (sin la puerta abierta ni la salida vieja)
    static float Rayo(Transform cloacas, Vector3 origen, Vector3 dir, float largo)
    {
        float mejor = largo;
        foreach (RaycastHit h in Physics.RaycastAll(origen, dir, largo, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (!t.IsChildOf(cloacas) || h.collider.attachedRigidbody) continue;
            if (t.name == "Puerta Entrada" || (t.parent && t.parent.name == "Puerta Entrada")) continue;
            Transform salida = cloacas.Find(Nombre);
            if (salida && t.IsChildOf(salida)) continue;
            if (h.distance < mejor) mejor = h.distance;
        }
        return mejor;
    }

    // Junto a la puerta, del lado de adentro (z local negativo), en el primer lado que este libre
    static Vector3 PosicionCerradura(Transform raiz, float ancho, bool enPuerta)
    {
        if (!enPuerta) return new Vector3(AnchoPasillo * 0.5f - 1.1f, 0f, -1.4f);
        float x = ancho * 0.5f + 0.9f;
        foreach (Vector3 p in new[] { new Vector3(x, 0f, -1.2f), new Vector3(-x, 0f, -1.2f), new Vector3(x, 0f, -2f), new Vector3(-x, 0f, -2f) })
        {
            Vector3 w = raiz.TransformPoint(p) + Vector3.up * 1f;
            if (!Physics.CheckBox(w, new Vector3(0.45f, 0.8f, 0.45f), raiz.rotation, ~0, QueryTriggerInteraction.Ignore)) return p;
        }
        return new Vector3(x, 0f, -1.2f);
    }

    static Transform Pieza(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material mat, bool conCollider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!conCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }
}
