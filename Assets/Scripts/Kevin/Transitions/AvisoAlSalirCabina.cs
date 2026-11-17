using UnityEngine;

// Muestra un aviso (un dialogo, ej: "Pulsa Shift para correr") la primera vez que el jugador sale de la cabina
// despues de que se abre su puerta (CabinaSalida). Va en la cabina de la Zona Principal.
public class AvisoAlSalirCabina : MonoBehaviour
{
    public DialogueData aviso;
    [Tooltip("Cuerpo de la cabina (sus limites dicen si el jugador esta adentro). Vacio = el hijo \"Cabina\".")]
    public Renderer cuerpo;

    CabinaSalida salida;
    Transform jugador;
    bool estuvoAdentro, mostrado;

    void Start()
    {
        salida = GetComponent<CabinaSalida>();
        if (!cuerpo) { Transform c = transform.Find("Cabina"); if (c) cuerpo = c.GetComponentInChildren<Renderer>(); }
    }

    void Update()
    {
        if (mostrado || aviso == null || cuerpo == null) return;
        if (!jugador)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (!pm) return;
            jugador = pm.transform;
        }
        Bounds b = cuerpo.bounds;
        b.Expand(new Vector3(0.4f, 10f, 0.4f));
        bool adentro = b.Contains(jugador.position);
        if (adentro) { estuvoAdentro = true; return; }
        if (!estuvoAdentro) return;
        if (salida != null && salida.Cerrada) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsActive) return; // espera a que termine otro dialogo

        mostrado = true;
        DialogueManager.Instance.StartDialogue(aviso);
    }
}
