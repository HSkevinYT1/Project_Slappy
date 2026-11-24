using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Correcciones de ortografia en los textos que ve el jugador y que ya estaban guardados (los dialogos de
// Assets/Dialogos y los textos de los componentes de la escena). Solo reemplaza estas frases exactas (no toca nombres
// de objetos ni nada que no sea uno de estos textos). Se aplica sola al abrir Unity si encuentra alguna;
// a mano: Tools > Guion > Corregir ortografia. Despues guarda la escena (Ctrl+S) si cambio algo en ella.
[InitializeOnLoad]
public static class OrtografiaSetup
{
    // (frase con error, frase corregida)
    static readonly (string mal, string bien)[] Cambios =
    {
        ("Vengo por el muneco.", "Vengo por el muñeco."),
        ("Esta abajo, en el sotano. Traelo.", "Está abajo, en el sótano. Tráelo."),
        ("Buenos dias buen hombre, vengo buscando un cuchillo antiguo que vi por internet", "Buenos días, buen hombre. Vengo buscando un cuchillo antiguo que vi por internet."),
        ("Que tenga buen dia.", "Que tenga buen día."),
        ("Para interactuar con objetos pulsa la tecla F. Si necesitas recordar algo mas", "Para interactuar con objetos, pulsa la tecla F. Si necesitas recordar algo más,"),
        ("Bienvenido al juego, para avanzar los diálogos pulsa", "Bienvenido al juego. Para avanzar los diálogos, pulsa"),
        ("cuando lo necesites ", "cuando lo necesites."),
        ("Tienda de antiguedades Barlato fundada en 1847", "Tienda de antigüedades Barlato, fundada en 1847"),
        ("SALA DE ENERGIA", "SALA DE ENERGÍA"),
        ("Tal vez haya algo por aqui.", "Tal vez haya algo por aquí."),
        ("Algo te encontro en la oscuridad.", "Algo te encontró en la oscuridad."),
        ("El reloj intento avisarte.", "El reloj intentó avisarte."),
        ("(Más abajo, con otra letra:)", "— — —"),
    };

    const string Hecho = "OrtografiaSetup.v1";

    static OrtografiaSetup()
    {
        // Una vez por sesion del editor (recorre toda la escena: no hace falta en cada recompilacion)
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(Hecho, false)) return;
            SessionState.SetBool(Hecho, true);
            Aplicar(false);
        };
    }

    [MenuItem("Tools/Guion/Corregir ortografia")]
    static void Menu() => Aplicar(true);

    static void Aplicar(bool avisar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var hechos = new List<string>();

        // Los dialogos (ScriptableObjects)
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Dialogos" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            Object o = AssetDatabase.LoadMainAssetAtPath(ruta);
            if (o != null && Corregir(o, false)) { EditorUtility.SetDirty(o); hechos.Add(System.IO.Path.GetFileNameWithoutExtension(ruta)); }
        }
        if (hechos.Count > 0) AssetDatabase.SaveAssets();

        // Los textos de los componentes de las escenas abiertas
        int enEscena = 0;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var escena = EditorSceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            bool cambio = false;
            foreach (GameObject raiz in escena.GetRootGameObjects())
                foreach (MonoBehaviour m in raiz.GetComponentsInChildren<MonoBehaviour>(true))
                    if (m != null && Corregir(m, true)) { cambio = true; enEscena++; }
            if (cambio) EditorSceneManager.MarkSceneDirty(escena);
        }

        if (hechos.Count > 0 || enEscena > 0)
            Debug.Log("[Ortografía] Corregidos: " + (hechos.Count > 0 ? "diálogos " + string.Join(", ", hechos) : "") +
                      (enEscena > 0 ? (hechos.Count > 0 ? "; " : "") + enEscena + " componente(s) de la escena (guarda la escena, Ctrl+S)" : "") + ".");
        else if (avisar) Debug.Log("[Ortografía] No había nada que corregir.");
    }

    // Todos los textos (string, tambien dentro de listas) del objeto
    static bool Corregir(Object o, bool conUndo)
    {
        var so = new SerializedObject(o);
        SerializedProperty p = so.GetIterator();
        bool algo = false;
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.String) continue;
            string v = p.stringValue;
            if (string.IsNullOrEmpty(v)) continue;
            string n = v;
            foreach (var (mal, bien) in Cambios)
                if (n.Contains(mal) && !n.Contains(bien)) n = n.Replace(mal, bien);
            if (n == v) continue;
            if (!algo && conUndo) Undo.RecordObject(o, "Ortografía");
            p.stringValue = n;
            algo = true;
        }
        if (algo) so.ApplyModifiedPropertiesWithoutUndo();
        return algo;
    }
}
