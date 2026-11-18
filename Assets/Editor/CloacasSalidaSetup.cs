using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Salida de las Cloacas con la llave (la llave ya la pone EnemigosZonasSetup en la mano de una estatua al azar):
// - "Reja Salida": reja de hierro en la entrada de las Cloacas (en "Entrada Pasillo", un poco hacia adentro).
//   Empieza abierta (hundida en el piso) y se cierra cuando el jugador ya esta adentro.
// - "Cerradura": poste con una cerradura al lado de la reja (ReceptorObjeto que solo acepta la "Llave Cloacas").
//   Al entregarle la llave se abre la reja y se completa la zona (ProgresoCaminos).
// Todo dentro de "Cloacas/Salida Cloacas" (SalidaConLlave).
// Se aplica sola una vez y se puede rehacer con Tools > Cloacas > Salida con llave. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class CloacasSalidaSetup
{
    const string Nombre = "Salida Cloacas";
    // Pasillos: 8 m de ancho interior y paredes de 4.25 m (ver PasillosSetup)
    const float Ancho = 8.1f, Alto = 4.25f, Grosor = 0.25f;
    // Metros hacia adentro de la entrada donde queda la reja
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
        if (!c || c.transform.Find(Nombre) || !c.transform.Find("Entrada Pasillo") || !Object.FindFirstObjectByType<LlaveAleatoria>()) return;
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

        // Raiz sin escala (los desplazamientos de la reja son en metros)
        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Salida Cloacas");
        raiz.transform.SetParent(cloacas, true);
        raiz.transform.SetPositionAndRotation(entrada.position - fuera * Adentro, giro);
        Vector3 s = cloacas.lossyScale;
        raiz.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

        // ---- Reja: hoja de barrotes de hierro + travesanos (cerrada = en su sitio; abierta = hundida en el piso)
        Material hierro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RejaHierro.mat");
        var reja = new GameObject("Reja Salida");
        reja.transform.SetParent(raiz.transform, false);
        reja.transform.localPosition = new Vector3(0f, Alto * 0.5f, 0f);
        int barrotes = 17;
        for (int i = 0; i < barrotes; i++)
        {
            float x = Mathf.Lerp(-Ancho * 0.5f + 0.15f, Ancho * 0.5f - 0.15f, i / (float)(barrotes - 1));
            Pieza(reja.transform, "Barrote " + (i + 1), new Vector3(x, 0f, 0f), new Vector3(0.09f, Alto, 0.09f), hierro, false);
        }
        foreach (float y in new[] { -Alto * 0.5f + 0.25f, 0f, Alto * 0.5f - 0.15f })
            Pieza(reja.transform, "Travesano", new Vector3(0f, y, 0f), new Vector3(Ancho, 0.12f, 0.14f), hierro, false);
        // Un solo collider para toda la reja (no deja pasar al jugador ni a las estatuas)
        var col = reja.AddComponent<BoxCollider>();
        col.size = new Vector3(Ancho, Alto, Grosor);
        var puerta = reja.AddComponent<PuertaDeslizante>();
        puerta.abiertaAlIniciar = true;
        puerta.desplazamiento = new Vector3(0f, -(Alto + 0.1f), 0f);
        puerta.duracion = 0.6f;

        // ---- Cerradura: poste a un costado, del lado de adentro, con la cerradura mirando a la zona
        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CloacaMetal.mat");
        Material oro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Llave Cloacas.mat");
        var cerr = new GameObject("Cerradura");
        cerr.transform.SetParent(raiz.transform, false);
        cerr.transform.localPosition = new Vector3(Ancho * 0.5f - 1.1f, 0f, -1.4f);
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
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Cloacas") salida.area = z;
        salida.sonidoCerrar = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sonidos/SFX/RejaCae.wav");
        salida.sonidoAbrir = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/puerta abriendose.wav");

        EditorSceneManager.MarkSceneDirty(cloacas.gameObject.scene);
        Debug.Log("[Cloacas] Salida con llave creada en la entrada de las Cloacas. Guarda la escena (Ctrl+S).", raiz);
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
