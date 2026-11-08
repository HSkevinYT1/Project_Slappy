using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Crea el prefab Assets/Prefabs/Armario.prefab (modelo Armario + Puerta_Armario con bisagra y el script
// Escondite) y lo coloca en la escena "Kevin" frente al jugador, una sola vez al abrir esa escena.
// Se puede rehacer con Tools > Armario. El sonido de agitacion se pone en el Escondite del prefab.
[InitializeOnLoad]
public static class ArmarioSetup
{
    const string Prefab = "Assets/Prefabs/Armario.prefab";
    const string ModeloArmario = "Assets/Models/Armario.obj";
    const string ModeloPuerta = "Assets/Models/Puerta_Armario.obj";
    const string Escena = "Kevin";
    // Alto del armario en metros (los .obj vienen en otra escala)
    const float Alto = 2.3f;

    static ArmarioSetup()
    {
        EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().name != Escena) return;
        if (Object.FindFirstObjectByType<Escondite>(FindObjectsInactive.Include)) return;
        ColocarEnEscena();
    }

    [MenuItem("Tools/Armario/Rehacer prefab")]
    static void RehacerPrefab()
    {
        AssetDatabase.DeleteAsset(Prefab);
        CrearPrefab();
    }

    [MenuItem("Tools/Armario/Poner armario frente al jugador")]
    public static void ColocarEnEscena()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!prefab) prefab = CrearPrefab();
        if (!prefab) return;

        var armario = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(armario, "Armario");

        // 4 m delante del jugador, apoyado en el piso y con la puerta mirandolo
        PlayerMovement jugador = Object.FindFirstObjectByType<PlayerMovement>();
        Vector3 origen = jugador ? jugador.transform.position : Vector3.zero;
        Vector3 adelante = jugador ? jugador.transform.forward : Vector3.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        adelante.Normalize();
        Vector3 pos = origen + adelante * 4f;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        armario.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-adelante));

        EditorSceneManager.MarkSceneDirty(armario.scene);
        Selection.activeGameObject = armario;
        Debug.Log("[Armario] Colocado frente al jugador. Pon el sonido de agitacion en Escondite (prefab Armario) y guarda la escena.", armario);
    }

    static GameObject CrearPrefab()
    {
        var mArmario = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloArmario);
        var mPuerta = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloPuerta);
        if (!mArmario || !mPuerta) { Debug.LogWarning("[Armario] Faltan " + ModeloArmario + " o " + ModeloPuerta); return null; }

        var raiz = new GameObject("Armario");
        var cuerpo = (GameObject)PrefabUtility.InstantiatePrefab(mArmario, raiz.transform);
        cuerpo.name = "Modelo";
        var puertaModelo = (GameObject)PrefabUtility.InstantiatePrefab(mPuerta, raiz.transform);
        puertaModelo.name = "Puerta";

        // Misma escala para los dos (vienen en el mismo espacio), con el alto pedido y la base en el suelo
        Bounds b = Limites(cuerpo.transform);
        float escala = Alto / Mathf.Max(0.0001f, b.size.y);
        cuerpo.transform.localScale = puertaModelo.transform.localScale = Vector3.one * escala;
        b = Limites(cuerpo.transform);
        Vector3 mover = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        cuerpo.transform.position += mover;
        puertaModelo.transform.position += mover;
        b = Limites(cuerpo.transform);
        Bounds bp = Limites(puertaModelo.transform);

        // Frente: hacia donde esta la puerta. Se gira todo para que el frente sea +Z del prefab
        Vector3 frente = bp.center - b.center; frente.y = 0f;
        frente = Mathf.Abs(frente.x) > Mathf.Abs(frente.z) ? new Vector3(Mathf.Sign(frente.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(frente.z));
        var contenido = new GameObject("Contenido").transform;
        contenido.SetParent(raiz.transform, false);
        cuerpo.transform.SetParent(contenido, true);
        puertaModelo.transform.SetParent(contenido, true);
        contenido.localRotation = Quaternion.FromToRotation(frente, Vector3.forward);
        b = Limites(cuerpo.transform);
        bp = Limites(puertaModelo.transform);

        // Colision: el cuerpo con su malla (hueco por dentro), la puerta con una caja
        foreach (MeshFilter mf in cuerpo.GetComponentsInChildren<MeshFilter>())
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

        // Bisagra en el borde izquierdo de la puerta (visto desde fuera)
        var bisagra = new GameObject("Bisagra").transform;
        bisagra.SetParent(contenido.parent, false);
        bisagra.position = new Vector3(bp.min.x, 0f, bp.center.z);
        puertaModelo.transform.SetParent(bisagra, true);
        var cajaPuerta = bisagra.gameObject.AddComponent<BoxCollider>();
        cajaPuerta.center = bisagra.InverseTransformPoint(bp.center);
        cajaPuerta.size = new Vector3(bp.size.x, bp.size.y, Mathf.Max(0.03f, bp.size.z));

        // La puerta abre hacia fuera
        Vector3 brazo = bp.center - bisagra.position; brazo.y = 0f;
        float angulo = (Quaternion.Euler(0f, -105f, 0f) * brazo).z > (Quaternion.Euler(0f, 105f, 0f) * brazo).z ? -105f : 105f;

        // Dentro: centro del armario, a la altura de los ojos, mirando a la puerta
        var dentro = new GameObject("Punto Dentro").transform;
        dentro.SetParent(raiz.transform, false);
        dentro.localPosition = new Vector3(b.center.x, Mathf.Min(1.6f, b.max.y - 0.35f), b.center.z - b.extents.z * 0.15f);
        dentro.localRotation = Quaternion.identity;
        var salida = new GameObject("Punto Salida").transform;
        salida.SetParent(raiz.transform, false);
        salida.localPosition = new Vector3(b.center.x, 0f, b.max.z + 1.1f);
        salida.localRotation = Quaternion.identity;

        var interactable = raiz.AddComponent<Interactable>();
        var so = new SerializedObject(interactable);
        so.FindProperty("prompt").stringValue = "Esconderse";
        so.ApplyModifiedPropertiesWithoutUndo();
        var escondite = raiz.AddComponent<Escondite>();
        escondite.puerta = bisagra;
        escondite.anguloAbierta = angulo;
        escondite.puntoDentro = dentro;
        escondite.puntoSalida = salida;
        UnityEventTools.AddPersistentListener(interactable.onInteract, escondite.Usar);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        Object.DestroyImmediate(raiz);
        Debug.Log($"[Armario] Prefab creado: {Prefab} (escala {escala:0.####}, {b.size.x:0.##} x {b.size.y:0.##} x {b.size.z:0.##} m).", prefab);
        return prefab;
    }

    static Bounds Limites(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
