using UnityEngine;

// Este script se encargar de el Shake de la camara al caminar
public class HeadBob : MonoBehaviour
{
    public float stepInterval = 0.45f;
    public float bobHeight = 0.10f;
    public float returnSpeed = 8f;

    private Vector3 startPosition;
    private Rigidbody playerRb;
    private float stepTimer;

    private void Start()
    {
        startPosition = transform.localPosition;
        playerRb = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        if (playerRb == null)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, returnSpeed) * Time.deltaTime);
            SetVerticalPosition(Mathf.Lerp(transform.localPosition.y, startPosition.y, blend));
            return;
        }

        float safeStepInterval = Mathf.Max(0.01f, stepInterval);
        Vector3 horizontalVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
        bool isMoving = horizontalVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= safeStepInterval)
                stepTimer %= safeStepInterval;

            float stepProgress = stepTimer / safeStepInterval;
            float bob = Mathf.Sin(stepProgress * Mathf.PI) * bobHeight;
            SetVerticalPosition(startPosition.y - bob);
        }
        else
        {
            stepTimer = 0f;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, returnSpeed) * Time.deltaTime);
            SetVerticalPosition(Mathf.Lerp(transform.localPosition.y, startPosition.y, blend));
        }
    }

    private void SetVerticalPosition(float y)
    {
        //mantiene la posicion en x y z, solo cambia la posicion en y
        transform.localPosition = new Vector3(transform.localPosition.x, y, startPosition.z);
    }
}
