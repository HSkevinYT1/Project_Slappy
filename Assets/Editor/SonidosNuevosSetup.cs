using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Asigna los sonidos nuevos de Assets/Sounds/SFX:
// - "puerta abriendose": la puerta de la cabina al abrirse (CabinaSalida).
// - "fede grito ambiente", "fede grito cuando te ve", "fede screamer": el enemigo 1 (JefeIndigoAI).
// - "sonido cable 1/2/3": al conectar un cable en el minijuego del telefono (prefab Telefono).
// Solo rellena lo que esta vacio. Rehacer: Tools > Sonidos > Asignar sonidos nuevos.
[InitializeOnLoad]
public static class SonidosNuevosSetup
{
    const string Carpeta = "Assets/Sounds/SFX/";
    const string RutaTelefono = "Assets/Prefabs/Telefono.prefab";

    static SonidosNuevosSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Aplicar(false);
        };
    }

    [MenuItem("Tools/Sonidos/Asignar sonidos nuevos")]
    static void Rehacer() => Aplicar(true);

    static AudioClip Clip(string nombre) => AssetDatabase.LoadAssetAtPath<AudioClip>(Carpeta + nombre);

    static void Aplicar(bool forzar)
    {
        string informe = "";
        var puerta = Clip("puerta abriendose.wav");
        var ambiente = Clip("fede grito ambiente.mp3");
        var alVer = Clip("fede grito cuando te ve.mp3");
        var screamer = Clip("fede screamer.mp3");

        // Cabina que se abre
        foreach (var c in Object.FindObjectsByType<CabinaSalida>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!puerta || (c.sonidoPuerta && !forzar)) continue;
            Undo.RecordObject(c, "Sonidos nuevos");
            c.sonidoPuerta = puerta;
            Marcar(c);
            informe += " puerta de " + c.name + ";";
        }

        // Enemigo 1 (Fede)
        foreach (var e in Object.FindObjectsByType<JefeIndigoAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool cambio = false;
            Undo.RecordObject(e, "Sonidos nuevos");
            if (!EsFede(e))
            {
                // Los sonidos de Fede son solo del enemigo 1
                if (e.sonidoAmbiente == ambiente && ambiente) { e.sonidoAmbiente = null; cambio = true; }
                if (e.sonidoAlVer == alVer && alVer) { e.sonidoAlVer = null; cambio = true; }
                if (e.sonidoScreamer == screamer && screamer) { e.sonidoScreamer = null; cambio = true; }
                // La velocidad por distancia es solo de Fede (una vez; despues se respeta el Inspector)
                if (e.acelerarConDistancia && !EditorPrefs.GetBool(Application.dataPath + ".OtrosNormales", false)) { e.acelerarConDistancia = false; cambio = true; }
                if (cambio) { Marcar(e); informe += " quitados de " + e.name + ";"; }
                continue;
            }
            // Solo el enemigo 1 va mas rapido con el jugador lejos (se activa una vez; despues se respeta el Inspector)
            if (!e.acelerarConDistancia && !EditorPrefs.GetBool(Application.dataPath + ".FedeRapido", false))
            {
                e.acelerarConDistancia = true;
                EditorPrefs.SetBool(Application.dataPath + ".FedeRapido", true);
                cambio = true;
                informe += " velocidad por distancia en " + e.name + ";";
            }
            if (ambiente && (!e.sonidoAmbiente || forzar)) { e.sonidoAmbiente = ambiente; cambio = true; }
            if (alVer && (!e.sonidoAlVer || forzar)) { e.sonidoAlVer = alVer; cambio = true; }
            if (screamer && (!e.sonidoScreamer || forzar)) { e.sonidoScreamer = screamer; cambio = true; }
            if (!cambio) continue;
            Marcar(e);
            informe += " sonidos de " + e.name + ";";
        }

        // Tic-tac del reloj: el RelojJugador lo crea el HUD al jugar si no hay uno en la escena;
        // se deja uno en la escena (objeto "HUD Jugador") para poder asignarle el sonido
        var manecillas = Clip("reloj manecillas.wav");
        if (manecillas)
        {
            var rj = Object.FindFirstObjectByType<RelojJugador>(FindObjectsInactive.Include);
            var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (!rj && pm)
            {
                var go = new GameObject("HUD Jugador");
                SceneManager.MoveGameObjectToScene(go, pm.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go, "Sonidos nuevos");
                rj = go.AddComponent<RelojJugador>();
            }
            if (rj && (!rj.sonidoManecillas || forzar))
            {
                Undo.RecordObject(rj, "Sonidos nuevos");
                rj.sonidoManecillas = manecillas;
                Marcar(rj);
                informe += " tic-tac del reloj en " + rj.name + ";";
            }
        }

        // Cables del telefono (prefab)
        var cables = new[] { Clip("sonido cable 1.mp3"), Clip("sonido cable 2.mp3"), Clip("sonido cable 3.mp3") };
        if (cables[0] && cables[1] && cables[2] && AssetDatabase.LoadAssetAtPath<GameObject>(RutaTelefono))
        {
            GameObject raiz = PrefabUtility.LoadPrefabContents(RutaTelefono);
            try
            {
                bool cambio = false;
                foreach (var w in raiz.GetComponentsInChildren<WiresMinigame>(true))
                {
                    var so = new SerializedObject(w);
                    var lista = so.FindProperty("connectClips");
                    if (lista == null || (lista.arraySize > 0 && !forzar)) continue;
                    lista.arraySize = cables.Length;
                    for (int i = 0; i < cables.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = cables[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    cambio = true;
                }
                if (cambio) { PrefabUtility.SaveAsPrefabAsset(raiz, RutaTelefono); informe += " cables del telefono;"; }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        EditorPrefs.SetBool(Application.dataPath + ".OtrosNormales", true);
        if (informe != "") Debug.Log("[Sonidos]" + informe + " Guarda la escena (Ctrl+S).");
        else if (forzar) Debug.Log("[Sonidos] No habia nada que asignar.");
    }

    static bool EsFede(JefeIndigoAI e)
    {
        string n = e.name.ToLowerInvariant();
        return n.Contains("fede") || n.Contains("enemy 1");
    }

    static void Marcar(Component c)
    {
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }
}
