using UnityEngine;

// "Jugo" (game feel): respuestas chicas, cortas y exageradas que acompanan a cada evento para que se sienta.
// Todo es visual/sonoro: nunca mueve al jugador ni cambia la logica del juego.
//   Jugo.Sacudir(0.3f)                  temblor de camara (trauma 0..1, se suma y decae solo)
//   Jugo.SacudirEn(pos, 0.4f, 15f)      igual, pero mas flojo cuanto mas lejos este la camara de pos
//   Jugo.PatadaFOV(-2f)                 golpe de campo de vision (negativo = zoom hacia adentro) que vuelve con resorte
//   Jugo.Cabeceo(0.05f)                 la camara baja de golpe y vuelve (aterrizajes, recoger cosas)
// Niveles orientativos (un juego de terror: mejor quedarse cortos):
//   chico  trauma 0.05-0.15, sin congelar  (recoger, pasos, UI)
//   medio  trauma 0.25-0.45                 (puertas que golpean, aterrizajes fuertes, sustos)
//   grande trauma 0.7-1                     (muerte, explosiones)
// La camara la mueve CamaraJugo (se agrega sola a la camara del jugador desde HUD).
// Accesibilidad: "Movimiento de camara" en Opciones del menu de pausa escala temblor, golpes de FOV y cabeceos.
public static class Jugo
{
    const string ClaveMovimiento = "Opciones.MovimientoCamara";
    static float movimiento = -1f;

    // 0 = sin temblor ni golpes de camara, 1 = completo
    public static float Movimiento
    {
        get { if (movimiento < 0f) movimiento = Mathf.Clamp01(PlayerPrefs.GetFloat(ClaveMovimiento, 1f)); return movimiento; }
        set { movimiento = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ClaveMovimiento, movimiento); }
    }

    // ------------------------------------------------------------------ Camara

    public static void Sacudir(float trauma)
    {
        if (CamaraJugo.Instance != null) CamaraJugo.Instance.AgregarTrauma(trauma);
    }

    public static void SacudirEn(Vector3 punto, float trauma, float radio)
    {
        CamaraJugo c = CamaraJugo.Instance;
        if (c == null || radio <= 0f) return;
        float k = 1f - Mathf.Clamp01(Vector3.Distance(c.transform.position, punto) / radio);
        if (k > 0f) c.AgregarTrauma(trauma * k * k);
    }

    public static void PatadaFOV(float grados)
    {
        if (CamaraJugo.Instance != null) CamaraJugo.Instance.AgregarPatadaFOV(grados);
    }

    public static void Cabeceo(float metros)
    {
        if (CamaraJugo.Instance != null) CamaraJugo.Instance.AgregarCabeceo(metros);
    }

    // ------------------------------------------------------------------ Curvas (t de 0 a 1)

    // Se pasa un poco del final y vuelve (para "pops")
    public static float SalidaAtras(float t, float s = 1.70158f)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + (s + 1f) * t * t * t + s * t * t;
    }

    // Arranca rapido y frena suave (para asentar)
    public static float SalidaCubica(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    // Arranca lento y acelera (para salir / anticipar)
    public static float EntradaCubica(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    // Resorte amortiguado: x vuelve a 0 con un pequeno rebote. dt en segundos reales
    public static void Resorte(ref float x, ref float v, float rigidez, float amortiguacion, float dt)
    {
        dt = Mathf.Min(dt, 0.05f);
        v += (-rigidez * x - amortiguacion * v) * dt;
        x += v * dt;
        if (Mathf.Abs(x) < 0.0001f && Mathf.Abs(v) < 0.001f) { x = 0f; v = 0f; }
    }

}
