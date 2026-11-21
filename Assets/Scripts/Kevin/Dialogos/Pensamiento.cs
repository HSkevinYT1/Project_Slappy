using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Frases del protagonista (pensamientos y ayudas: "tal vez haya una llave por aqui").
// Se muestran como subtitulos del DialogueManager en modo Examinar (sin nombre, en gris) y no bloquean al jugador.
//   Pensamiento.Decir("cloacas.reja", 0.5f, "Se cerro...", "Tal vez haya una llave por aqui.");
// - Cada id se dice UNA sola vez por partida (vacio = siempre).
// - Si ya hay otro dialogo en pantalla, espera su turno en una cola (ColaDialogos); "retraso" se cuenta desde
//   que termino lo anterior de la cola.
// - Un texto con varias lineas (Enter) se muestra como varios subtitulos seguidos.
public static class Pensamiento
{
    static readonly HashSet<string> dichos = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        dichos.Clear();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    // Al recargar la escena (ej: "Volver al inicio") se pueden volver a decir
    static void AlCargarEscena(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) dichos.Clear(); }

    public static bool YaDicho(string id) => !string.IsNullOrEmpty(id) && dichos.Contains(id);

    public static void Decir(string id, float retraso, params string[] textos)
    {
        var lineas = new List<string>();
        if (textos != null)
            foreach (string t in textos)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                foreach (string l in t.Split('\n')) if (!string.IsNullOrWhiteSpace(l)) lineas.Add(l.Trim());
            }
        if (lineas.Count == 0) return;
        if (!string.IsNullOrEmpty(id) && !dichos.Add(id)) return;
        ColaDialogos.Instancia.Encolar(lineas.ToArray(), retraso);
    }

    public static void Decir(string id, params string[] textos) => Decir(id, 0f, textos);
}
