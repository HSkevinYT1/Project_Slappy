using UnityEngine;
using UnityEngine.Events;

// Zona que hace que el protagonista diga algo al entrar (una ayuda o un pensamiento: "tal vez haya una llave por aqui").
// Uso: un objeto vacio con un BoxCollider (se marca solo como trigger), este componente y las lineas.
// Cada linea es un subtitulo; se muestran en gris, sin nombre, y no bloquean al jugador (ver Pensamiento).
[RequireComponent(typeof(BoxCollider))]
public class DialogoTrigger : MonoBehaviour
{
    [TextArea(2, 5)] public string[] lineas = { "Tal vez haya algo por aquí." };
    [Tooltip("Segundos de espera desde que entra el jugador")]
    public float retraso = 0f;
    [Tooltip("Solo la primera vez que entra")]
    public bool soloUnaVez = true;
    [Tooltip("Opcional: solo si este objeto esta activo (ej: una reja cerrada, un enemigo cerca)")]
    public GameObject requiereActivo;

    public UnityEvent alDecir;

    bool dicho;

    void Reset()
    {
        var b = GetComponent<BoxCollider>();
        b.isTrigger = true;
        b.size = new Vector3(4f, 3f, 4f);
        b.center = new Vector3(0f, 1.5f, 0f);
        gameObject.layer = 2; // Ignore Raycast: no estorba al raycast de interaccion
    }

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if ((soloUnaVez && dicho) || other.GetComponentInParent<PlayerMovement>() == null) return;
        if (requiereActivo != null && !requiereActivo.activeInHierarchy) return;
        dicho = true;
        Pensamiento.Decir(soloUnaVez ? "trigger." + GetInstanceID() : null, retraso, lineas);
        alDecir?.Invoke();
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.55f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.55f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
