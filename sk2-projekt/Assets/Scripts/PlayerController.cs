using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _movementSpeed;

    private void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movementVector3 = new Vector3(horizontal, vertical);
        movementVector3.z = 0f;
        if (movementVector3.magnitude > 1)
            movementVector3.Normalize();
        transform.position += movementVector3 * _movementSpeed * Time.deltaTime;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2 direction = mousePosition - transform.position;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
    }
}
