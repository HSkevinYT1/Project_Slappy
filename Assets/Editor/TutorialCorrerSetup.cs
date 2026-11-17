using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Pistas del paso sotano -> Zona Principal:
// - Al revisar las 3 cajas del sotano (RevisionCajas > Al Encontrar Linterna) brilla el muneco de la cabina;
//   deja de brillar al entrar a la cabina (CabinaTeletransporte > Al Entrar).
// - Al llegar a la Zona Principal (FaintTeleport > On Arrived) brilla el panel numerico del telefono;
//   deja de brillar al arreglarlo (PhoneMinigame > On Repaired).
// - Al salir de la cabina de la Zona Principal aparece "Pulsa Shift para correr" (AvisoAlSalirCabina).
// Se aplica sola si falta algo; rehacer: Tools > Cabinas > Pistas (muneco, panel y correr).
[InitializeOnLoad]
public static class TutorialCorrerSetup
{
    const string Muneco = "zotano/muñeco ventriloco";
    const string Principal = "Zona_Principal/Cabina_Telefonica (1)";
    const string Brillo = "Assets/Materials/GlowMaterial.mat";
    const string RutaAviso = "Assets/Dialogos/Tutorial/PulsaShift.asset";

    static TutorialCorrerSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject p = GameObject.Find(Principal);
            if (p && !p.GetComponent<AvisoAlSalirCabina>()) Aplicar();
        };
    }

    [MenuItem("Tools/Cabinas/Pistas (muneco, panel y correr)")]
    static void Aplicar()
    {
        var glow = AssetDatabase.LoadAssetAtPath<Material>(Brillo);
        string log = "";

        // Muneco
        GameObject muneco = GameObject.Find(Muneco);
        var cajas = Object.FindFirstObjectByType<RevisionCajas>();
        var cabinaSotano = Object.FindFirstObjectByType<CabinaTeletransporte>();
        if (muneco && glow)
        {
            Highlightable h = Resaltable(muneco, glow);
            if (cajas) Conectar(cajas, cajas.alEncontrarLinterna, h.Show);
            if (cabinaSotano) Conectar(cabinaSotano, cabinaSotano.alEntrar, h.Hide);
            log += "muneco brilla al revisar las cajas; ";
        }

        // Panel del telefono de la Zona Principal
        GameObject principal = GameObject.Find(Principal);
        Transform panel = principal ? principal.transform.Find("Telefono/panel del telefono") : null;
        var desmayo = Object.FindFirstObjectByType<FaintTeleport>();
        if (panel && glow)
        {
            Highlightable h = Resaltable(panel.gameObject, glow);
            if (desmayo) Conectar(desmayo, desmayo.onArrived, h.Show);
            var tel = principal.GetComponentInChildren<PhoneMinigame>(true);
            if (tel) Conectar(tel, tel.onRepaired, h.Hide);
            log += "panel brilla al llegar; ";
        }

        // Aviso para correr al salir de la cabina
        if (principal)
        {
            var aviso = AssetDatabase.LoadAssetAtPath<DialogueData>(RutaAviso);
            if (!aviso)
            {
                aviso = ScriptableObject.CreateInstance<DialogueData>();
                aviso.modo = DialogueMode.Examinar;
                aviso.lineas = new[] { "Pulsa Shift para correr" };
                aviso.segundosExtra = 1.5f;
                System.IO.Directory.CreateDirectory("Assets/Dialogos/Tutorial");
                AssetDatabase.CreateAsset(aviso, RutaAviso);
            }
            var a = principal.GetComponent<AvisoAlSalirCabina>();
            if (!a) a = Undo.AddComponent<AvisoAlSalirCabina>(principal);
            a.aviso = aviso;
            Transform cuerpo = principal.transform.Find("Cabina");
            if (cuerpo) a.cuerpo = cuerpo.GetComponentInChildren<Renderer>();
            EditorUtility.SetDirty(a);
            log += "aviso de correr al salir.";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Cabinas] Pistas: " + log + " Guarda la escena (Ctrl+S).");
    }

    static Highlightable Resaltable(GameObject go, Material glow)
    {
        var h = go.GetComponent<Highlightable>();
        if (!h) h = Undo.AddComponent<Highlightable>(go);
        var so = new SerializedObject(h);
        so.FindProperty("outlineMaterial").objectReferenceValue = glow;
        so.ApplyModifiedProperties();
        return h;
    }

    // Agrega el listener persistente si no estaba (dueno = componente que tiene el evento, para marcarlo sucio)
    static void Conectar(Object dueno, UnityEvent ev, UnityAction accion)
    {
        for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            if (ev.GetPersistentTarget(i) == (Object)accion.Target && ev.GetPersistentMethodName(i) == accion.Method.Name) return;
        Undo.RecordObject(dueno, "Pista");
        UnityEventTools.AddPersistentListener(ev, accion);
        EditorUtility.SetDirty(dueno);
    }
}
