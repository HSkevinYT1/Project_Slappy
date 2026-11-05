using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Tutorial de la tienda:
// 1. Llega un cliente al mostrador y pide un cuchillo (dialogo).
// 2. Al cerrar el dialogo, el cuchillo brilla (Highlightable) y se puede agarrar.
// 3. El jugador agarra el cuchillo y se lo entrega al cliente (apuntarle + tecla de interaccion).
// 4. El cliente agradece y se va.
// 5. Llega el cultista y pide un muneco (dialogo).
// 6. Al cerrar el dialogo, la camara del jugador gira 180 grados (hacia la bajada al sotano).
//
// Empieza cuando el jugador esta dentro de la tienda (zona "areaTienda").
public class TutorialTienda : MonoBehaviour
{
    [Header("Inicio")]
    [Tooltip("El tutorial empieza cuando el jugador entra en esta zona (la tienda).")]
    public Bounds areaTienda = new Bounds(Vector3.zero, Vector3.one * 10f);
    [Tooltip("Segundos de espera antes de que entre el cliente.")]
    public float esperaInicial = 2f;
    [Tooltip("Segundos entre que se va el cliente y llega el cultista.")]
    public float esperaCultista = 3f;

    [Header("Recorrido de los NPCs (en orden: afuera -> puerta -> mostrador)")]
    public Transform[] caminoEntrada;
    [Range(0.5f, 5f)] public float velocidadNPC = 1.6f;

    [Header("Cliente")]
    public Transform cliente;
    public DialogueData dialogoClientePide;
    public DialogueData dialogoClienteGracias;
    [Tooltip("Lo que pide el cliente (brilla y se habilita al terminar el dialogo).")]
    public Grabbable cuchillo;

    [Header("Cultista")]
    public Transform cultista;
    public DialogueData dialogoCultistaPide;

    [Header("Giro de camara")]
    [Tooltip("Grados que gira el jugador al terminar el dialogo del cultista (180 = darse la vuelta).")]
    public float gradosGiro = 180f;
    public float duracionGiro = 1.2f;

    [Header("Eventos")]
    public UnityEvent alPedirCuchillo;
    public UnityEvent alEntregarCuchillo;
    public UnityEvent alTerminar;

    public string Paso { get; private set; } = "Esperando al jugador";

    PlayerMovement jugador;
    MouseLook mirada;
    bool dialogoTerminado;

    void Start()
    {
        jugador = FindFirstObjectByType<PlayerMovement>();
        mirada = FindFirstObjectByType<MouseLook>();

        if (cliente) cliente.gameObject.SetActive(false);
        if (cultista) cultista.gameObject.SetActive(false);

        // El cuchillo no se puede agarrar hasta que el cliente lo pida.
        if (cuchillo && cuchillo.TryGetComponent(out Interactable inter)) inter.enabled = false;

        if (DialogueManager.Instance != null) DialogueManager.Instance.OnDialogueEnded += AlTerminarDialogo;
        else Debug.LogWarning("[TutorialTienda] No hay DialogueManager en la escena: los dialogos no se veran.", this);

        StartCoroutine(Secuencia());
    }

    void OnDestroy()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.OnDialogueEnded -= AlTerminarDialogo;
    }

    void AlTerminarDialogo() => dialogoTerminado = true;

    IEnumerator Secuencia()
    {
        // ---- Esperar a que el jugador este en la tienda ----
        while (!JugadorEnTienda()) yield return null;
        yield return new WaitForSeconds(esperaInicial);

        // ---- Cliente: entra y pide el cuchillo ----
        Paso = "Cliente entrando";
        yield return Entrar(cliente);
        yield return Dialogo(dialogoClientePide);

        Paso = "Buscar el cuchillo";
        if (cuchillo)
        {
            if (cuchillo.TryGetComponent(out Interactable inter)) inter.enabled = true;
            if (cuchillo.TryGetComponent(out Highlightable brillo)) brillo.Show();
        }
        alPedirCuchillo?.Invoke();

        // ---- Esperar la entrega ----
        Paso = "Entregar el cuchillo";
        ReceptorObjeto receptor = cliente ? cliente.GetComponent<ReceptorObjeto>() : null;
        while (receptor != null && !receptor.Recibido)
        {
            MirarJugador(cliente);
            yield return null;
        }
        alEntregarCuchillo?.Invoke();

        yield return Dialogo(dialogoClienteGracias);

        // ---- Cliente: se va ----
        Paso = "Cliente saliendo";
        yield return Salir(cliente);
        yield return new WaitForSeconds(esperaCultista);

        // ---- Cultista: entra y pide el muneco ----
        Paso = "Cultista entrando";
        yield return Entrar(cultista);
        yield return Dialogo(dialogoCultistaPide);

        // ---- Giro de 180 hacia el sotano ----
        Paso = "Girando hacia el sotano";
        yield return GirarJugador(gradosGiro, duracionGiro);

        Paso = "Terminado";
        alTerminar?.Invoke();
    }

    // ------------------------------------------------------------------

    bool JugadorEnTienda()
    {
        Transform t = jugador ? jugador.transform : (Camera.main ? Camera.main.transform : null);
        return t != null && areaTienda.Contains(t.position);
    }

    IEnumerator Entrar(Transform npc)
    {
        if (!npc || caminoEntrada == null || caminoEntrada.Length == 0) yield break;
        npc.position = caminoEntrada[0].position;
        npc.gameObject.SetActive(true);
        for (int i = 1; i < caminoEntrada.Length; i++)
            yield return Caminar(npc, caminoEntrada[i].position);
        yield return GirarHaciaJugador(npc, 0.5f);
    }

    IEnumerator Salir(Transform npc)
    {
        if (!npc || caminoEntrada == null || caminoEntrada.Length == 0) yield break;
        for (int i = caminoEntrada.Length - 2; i >= 0; i--)
            yield return Caminar(npc, caminoEntrada[i].position);
        npc.gameObject.SetActive(false);
    }

    IEnumerator Caminar(Transform npc, Vector3 destino)
    {
        destino.y = npc.position.y;
        while ((npc.position - destino).sqrMagnitude > 0.0025f)
        {
            Vector3 dir = destino - npc.position;
            if (dir.sqrMagnitude > 0.0001f)
                npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 10f * Time.deltaTime);
            npc.position = Vector3.MoveTowards(npc.position, destino, velocidadNPC * Time.deltaTime);
            yield return null;
        }
        npc.position = destino;
    }

    IEnumerator GirarHaciaJugador(Transform npc, float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            MirarJugador(npc, 8f);
            yield return null;
        }
    }

    void MirarJugador(Transform npc, float suavidad = 5f)
    {
        Transform objetivo = Camera.main ? Camera.main.transform : (jugador ? jugador.transform : null);
        if (!npc || !objetivo) return;
        Vector3 dir = objetivo.position - npc.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), suavidad * Time.deltaTime);
    }

    IEnumerator Dialogo(DialogueData data)
    {
        if (data == null || DialogueManager.Instance == null) yield break;
        while (DialogueManager.Instance.IsActive) yield return null; // si habia otro abierto, se espera
        dialogoTerminado = false;
        DialogueManager.Instance.StartDialogue(data);
        while (!dialogoTerminado) yield return null;
    }

    // Gira al jugador (cuerpo) suavemente. MouseLook se pausa durante el giro y al reactivarse
    // toma el nuevo giro del cuerpo.
    IEnumerator GirarJugador(float grados, float segundos)
    {
        if (!jugador) yield break;
        Transform cuerpo = jugador.transform;
        Rigidbody rb = cuerpo.GetComponent<Rigidbody>();

        bool movia = jugador.enabled;
        jugador.enabled = false;
        if (rb) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        bool miraba = mirada && mirada.enabled;
        if (mirada) mirada.enabled = false;

        float inicio = cuerpo.eulerAngles.y;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / segundos);
            Quaternion rot = Quaternion.Euler(0f, inicio + grados * k, 0f);
            if (rb) rb.MoveRotation(rot); else cuerpo.rotation = rot;
            yield return null;
        }
        Quaternion final = Quaternion.Euler(0f, inicio + grados, 0f);
        if (rb) { rb.MoveRotation(final); rb.rotation = final; }
        cuerpo.rotation = final;
        yield return new WaitForFixedUpdate();

        if (mirada && miraba) mirada.enabled = true; // OnEnable toma el nuevo giro
        jugador.enabled = movia;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(areaTienda.center, areaTienda.size);
        if (caminoEntrada == null) return;
        for (int i = 1; i < caminoEntrada.Length; i++)
            if (caminoEntrada[i - 1] && caminoEntrada[i])
                Gizmos.DrawLine(caminoEntrada[i - 1].position, caminoEntrada[i].position);
    }
}
