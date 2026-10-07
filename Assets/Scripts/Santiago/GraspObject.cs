using UnityEngine;
// Clase encargada de permitir que el jugador agarre y suelte objetos.
public class GraspObject : MonoBehaviour
{
    // Punto donde se colocará el objeto cuando sea agarrado.
    public GameObject HandPoint;

    // Guarda el objeto que actualmente está agarrado.
    private GameObject picketObject = null;










    // Update is called once per frame
    void Update()
    {
        // Comprueba si actualmente hay un objeto agarrado.
        if (picketObject != null)
        {
            // Comprueba si el jugador está presionando la tecla R.
            if (Input.GetKey("r"))
            {
                // Reactiva la gravedad del objeto agarrado.
                picketObject.GetComponent<Rigidbody>().useGravity = true;

                // Reactiva las fisicas del objeto agarrado.
                picketObject.GetComponent<Rigidbody>().isKinematic = false;

                // El objeto deja de ser hijo del HandPoint.
                picketObject.gameObject.transform.SetParent(null);

                // Indica que ya no estamos agarrando ningún objeto.
                picketObject = null;
            }
        }



    }



    // Se ejecuta mientras otro Collider permanezca
    // dentro del Trigger de este objeto.
    private void OnTriggerStay(Collider other)
    {
        // Comprueba si el objeto que está dentro del Trigger
        if (other.gameObject.CompareTag("Object"))
        {
            // Comprueba si se está presionando E
            // y si actualmente no tenemos ningún objeto agarrado.
            if (Input.GetKey("e") && picketObject==null)
            {
                // Desactiva la gravedad del objeto que se va a agarrar.
                other.GetComponent<Rigidbody>().useGravity = false;

                // Desactiva las fisicar del objeto que se va a agarrar.
                other.GetComponent<Rigidbody>().isKinematic = true;

                // Mueve el objeto hasta la posición del HandPoint.
                other.transform.position = HandPoint.transform.position;

                // Convierte el objeto en hijo del HandPoint,
                // haciendo que se mueva junto con él.
                other.gameObject.transform.SetParent(HandPoint.gameObject.transform);

                // Guarda el objeto agarrado dentro de picketObject.
                picketObject = other.gameObject;
            }
        }



    }
}
