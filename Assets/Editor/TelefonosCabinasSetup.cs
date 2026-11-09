using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Telefonos de las dos cabinas (el prefab Telefono, en el mismo sitio donde estaban antes):
// - Cabina del sotano: solo decorativo, sin puzzle (no se puede interactuar).
// - Cabina de la Zona Principal (a donde llega el jugador): la puerta empieza cerrada y paredes invisibles
//   no le dejan salir; al resolver el puzzle de cables (PhoneMinigame > On Repaired) se abre la puerta.
// Se aplica sola una vez; rehacer: Tools > Cabinas > Telefonos y puerta de salida.
[InitializeOnLoad]
public static class TelefonosCabinasSetup
{
    const string RutaSotano = "zotano/props/Cabina_Telefonica";
    const string RutaPrincipal = "Zona_Principal/Cabina_Telefonica (1)";
    const string Prefab = "Assets/Prefabs/Telefono.prefab";

    static TelefonosCabinasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoAplicar;
        };
    }

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject sotano = GameObject.Find(RutaSotano), principal = GameObject.Find(RutaPrincipal);
        if (!sotano || !principal) return;
        if (sotano.transform.Find("Telefono") || principal.transform.Find("Telefono") || principal.GetComponent<CabinaSalida>()) return;
        Aplicar(sotano.transform, principal.transform);
    }

    [MenuItem("Tools/Cabinas/Telefonos y puerta de salida")]
    static void Menu()
    {
        GameObject sotano = GameObject.Find(RutaSotano), principal = GameObject.Find(RutaPrincipal);
        if (!sotano || !principal) { EditorUtility.DisplayDialog("Cabinas", "No se encontraron las dos cabinas.", "OK"); return; }
        foreach (Transform c in new[] { sotano.transform, principal.transform })
        {
            Transform t = c.Find("Telefono");
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        foreach (var n in new[] { "Bisagra Salida", "Paredes Cabina" })
        {
            Transform t = principal.transform.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        var vieja = principal.GetComponent<CabinaSalida>();
        if (vieja) Undo.DestroyObjectImmediate(vieja);
        Aplicar(sotano.transform, principal.transform);
    }

    static void Aplicar(Transform sotano, Transform principal)
    {
        // Sotano: telefono decorativo
        PhoneMinigame telSotano = Telefono(sotano, new Vector3(-0.67f, 75.4f, 0f));
        if (telSotano == null) return;
        var inter = telSotano.GetComponent<Interactable>();
        inter.enabled = false;
        EditorUtility.SetDirty(inter);

        // Zona Principal: telefono con el puzzle
        PhoneMinigame telPrincipal = Telefono(principal, new Vector3(1.48f, 88f, 0f));

        // Puerta cerrada al llegar
        if (CabinaSotanoSetup.Puerta(principal, out Transform puerta, out Bounds bc, out Vector3 hinge, out float angulo))
        {
            var bisagra = new GameObject("Bisagra Salida").transform;
            Undo.RegisterCreatedObjectUndo(bisagra.gameObject, "Bisagra salida");
            bisagra.SetParent(principal, true);
            bisagra.position = hinge;

            // Paredes invisibles alrededor del interior (la cabina no tiene colision)
            var paredes = new GameObject("Paredes Cabina");
            Undo.RegisterCreatedObjectUndo(paredes, "Paredes cabina");
            paredes.transform.SetParent(principal, true);
            paredes.transform.SetPositionAndRotation(new Vector3(bc.center.x, bc.min.y, bc.center.z), Quaternion.identity);
            paredes.transform.localScale = Vector3.one;
            Vector3 escala = paredes.transform.lossyScale;
            float alto = bc.size.y, grosor = 0.15f;

            // Lado de la puerta: el que queda mas cerca de la puerta ya cerrada
            Bounds bp = CabinaSotanoSetup.Limites(puerta);
            Vector3 centroCerrada = hinge + Quaternion.Euler(0f, angulo, 0f) * (new Vector3(bp.center.x, hinge.y, bp.center.z) - hinge);
            Vector3 rel = centroCerrada - bc.center;
            Vector3 ladoPuerta = Mathf.Abs(rel.x) / bc.extents.x > Mathf.Abs(rel.z) / bc.extents.z
                ? new Vector3(Mathf.Sign(rel.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(rel.z));

            Collider paredPuerta = null;
            foreach (Vector3 lado in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var box = paredes.AddComponent<BoxCollider>();
                bool enX = lado.x != 0f;
                Vector3 centro = new Vector3(lado.x * (bc.extents.x - grosor * 0.5f), alto * 0.5f, lado.z * (bc.extents.z - grosor * 0.5f));
                Vector3 tam = enX ? new Vector3(grosor, alto, bc.size.z) : new Vector3(bc.size.x, alto, grosor);
                box.center = new Vector3(centro.x / escala.x, centro.y / escala.y, centro.z / escala.z);
                box.size = new Vector3(tam.x / escala.x, tam.y / escala.y, tam.z / escala.z);
                if (lado == ladoPuerta) paredPuerta = box;
            }

            var salida = Undo.AddComponent<CabinaSalida>(principal.gameObject);
            salida.puerta = puerta;
            salida.bisagra = bisagra;
            salida.anguloCerrar = angulo;
            salida.paredPuerta = paredPuerta;
            EditorUtility.SetDirty(salida);

            if (telPrincipal != null)
            {
                UnityEventTools.AddVoidPersistentListener(telPrincipal.onRepaired, salida.Abrir);
                EditorUtility.SetDirty(telPrincipal);
            }
        }

        EditorSceneManager.MarkSceneDirty(sotano.gameObject.scene);
        Debug.Log("[Cabinas] Telefonos puestos: el del sotano sin puzzle, el de la Zona Principal abre la puerta de la cabina al repararlo. Guarda la escena.", principal);
    }

    static PhoneMinigame Telefono(Transform cabina, Vector3 posLocal)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!prefab) { Debug.LogWarning("[Cabinas] No encuentro " + Prefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cabina);
        Undo.RegisterCreatedObjectUndo(go, "Telefono");
        go.name = "Telefono";
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.49088f;

        // Mismos materiales que tenian
        var gris = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gris.mat");
        var negro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/negro.mat");
        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = r.name.StartsWith("Panel1_polySurface") && r.name != "Panel1_polySurface10" ? gris : r.name == "Telefono_telefono" ? negro : null;
            if (m == null) continue;
            var mats = r.sharedMaterials;
            mats[0] = m;
            r.sharedMaterials = mats;
        }
        Transform panel = go.transform.Find("panel del telefono");
        if (panel) panel.gameObject.SetActive(true);
        return go.GetComponent<PhoneMinigame>();
    }
}
