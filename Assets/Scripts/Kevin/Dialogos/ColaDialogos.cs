using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cola de los pensamientos del protagonista (ver Pensamiento). Se crea sola la primera vez que se usa.
// Espera a que no haya otro dialogo en pantalla (ni muerte ni menu) y los muestra de a uno con el DialogueManager.
// Los dialogos de los personajes (cliente, cultista...) nunca se pisan: si llegan mientras se piensa algo,
// esperan a que termine, como ya hacen todos los que usan el DialogueManager.
public class ColaDialogos : MonoBehaviour
{
    static ColaDialogos instancia;

    public static ColaDialogos Instancia
    {
        get
        {
            if (instancia == null) instancia = new GameObject("Cola Dialogos").AddComponent<ColaDialogos>();
            return instancia;
        }
    }

    struct Pedido
    {
        public string[] lineas;
        public float retraso;
    }

    readonly Queue<Pedido> cola = new Queue<Pedido>();
    bool procesando;

    public int Pendientes => cola.Count;

    public void Encolar(string[] lineas, float retraso)
    {
        cola.Enqueue(new Pedido { lineas = lineas, retraso = Mathf.Max(0f, retraso) });
        if (!procesando) StartCoroutine(Procesar());
    }

    IEnumerator Procesar()
    {
        procesando = true;
        while (cola.Count > 0)
        {
            Pedido p = cola.Dequeue();
            if (p.retraso > 0f) yield return new WaitForSeconds(p.retraso);
            while (!PuedeHablar()) yield return null;

            DialogueManager dm = DialogueManager.Instance;
            if (dm == null)
            {
                // Sin DialogueManager en la escena: aviso corto arriba
                string texto = string.Join(" ", p.lineas);
                HUD.Aviso(texto, Mathf.Clamp(texto.Length * 0.06f, 2.5f, 7f));
                yield return new WaitForSeconds(Mathf.Clamp(texto.Length * 0.06f, 2.5f, 7f));
                continue;
            }

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.name = "Pensamiento";
            data.modo = DialogueMode.Examinar;
            data.lineas = p.lineas;
            dm.StartDialogue(data);
            while (dm != null && dm.IsActive && dm.CurrentData == data) yield return null;
            Destroy(data);
            yield return new WaitForSeconds(0.35f);
        }
        procesando = false;
    }

    static bool PuedeHablar()
    {
        if (PantallaMuerte.Muerto || HUD.Oculto) return false;
        return DialogueManager.Instance == null || !DialogueManager.Instance.IsActive;
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }
}
