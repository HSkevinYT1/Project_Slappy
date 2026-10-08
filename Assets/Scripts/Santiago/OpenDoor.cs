using UnityEngine;

public class OpenDoor : MonoBehaviour
{
    public float rotationSpeed = 120f;

    public Vector3 openRotation = new Vector3(0f, -90f, 0f);

    private Quaternion closedRotation;
    private Quaternion targetRotation;

    void Start()
    {
        closedRotation = transform.localRotation;

        targetRotation = closedRotation;
    }

    void Update()
    {
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    public void OpenTheDoor()
    {
        targetRotation = Quaternion.Euler(openRotation);
    }
}