using System.Globalization;
using UnityEngine;

[RequireComponent (typeof(Player))]
public class PlayerController : MonoBehaviour
{
    private Player _player;
    private void Awake()
    {
        _player = GetComponent<Player>();   
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)){
            TestSendUDPData();
        }

        if (!_player.IsInitialized)
            return;

        Move();

        if (Input.GetMouseButtonDown(0))
            Shoot();
    }

    private void Move()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movementVector3 = new Vector3(horizontal, vertical);
        movementVector3.z = 0f;
        if (movementVector3.magnitude > 1)
            movementVector3.Normalize();
        transform.position += movementVector3 * _player.PlayerTypeData.MovementSpeed * Time.deltaTime;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2 direction = mousePosition - transform.position;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
    }

    private void Shoot()
    {

    }

    private void TestSendUDPData()
    {
        if (!NetworkManager.Instance.IsConnectionEstablished)
            return;

        Vector2Int position = transform.position.ToServer();
        NetworkManager.Instance.UdpSendMessageToServer(string.Format("{0}:{1}:{2}", position.x, position.y, Mathf.RoundToInt(transform.eulerAngles.z)));
    }
}
