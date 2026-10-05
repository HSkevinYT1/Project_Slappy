using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    private Rigidbody rb;
    private Vector3 movement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();

    }

    // Update is called once per frame
    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        movement = (transform.right * x + transform.forward * z).normalized;
    }
    void FixedUpdate()
    {
        Vector3 velocity = movement * speed;

        rb.linearVelocity = new Vector3(
        velocity.x,
        rb.linearVelocity.y,
        velocity.z
    );
    }
}
