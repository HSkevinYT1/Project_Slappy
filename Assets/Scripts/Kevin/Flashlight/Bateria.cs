using UnityEngine;

// Pila que recarga la linterna. Se recoge con su Interactable (On Interact > Bateria > Recoger).
public class Bateria : MonoBehaviour
{
    // Energia que devuelve (la linterna tiene 100 como maximo)
    public float carga = 35f;
    public AudioClip sonidoRecoger;

    public void Recoger(PlayerInteractor interactor)
    {
        Flashlight linterna = FindFirstObjectByType<Flashlight>(FindObjectsInactive.Include);
        if (linterna == null)
        {
            Debug.LogWarning(name + ": no hay ninguna linterna que recargar.", this);
            return;
        }

        linterna.Recharge(carga);
        if (sonidoRecoger != null) AudioSource.PlayClipAtPoint(sonidoRecoger, transform.position);
        Destroy(gameObject);
    }
}
