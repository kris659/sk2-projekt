using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent (typeof(Player))]
public class PlayerController : MonoBehaviour
{
    private Player _player;
    private bool _isShooting;
    private float _lastShootTime;

    private void Awake()
    {
        _player = GetComponent<Player>();   
    }

    private void Update()
    {
        if (!_player.IsInitialized)
            return;

        Move();

        if (Input.GetMouseButtonDown(0))
            _isShooting = true;
        
        if (Input.GetMouseButtonUp(0))
            _isShooting = false;

        if(_isShooting)
            Shoot();

        SendTransformData();
    }

    private void Move()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movementVector3 = new Vector3(horizontal, vertical);
        movementVector3.z = 0f;
        if (movementVector3.magnitude > 1)
            movementVector3.Normalize();
        Vector3 position = transform.position + movementVector3 * _player.PlayerTypeData.MovementSpeed * Time.deltaTime;
        if(position.x > 30f)
            position.x = 30f;
        if(position.x < -30f)
            position.x = -30f;
        if(position.y > 20f)
            position.y = 20f;
        if(position.y < -20f)
            position.y = -20f;

        transform.position = position;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2 direction = mousePosition - transform.position;
        _player.Visual.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
    }

    private void Shoot()
    {
        if (!NetworkManager.Instance.IsConnected)
            return;

        if (Time.time - _lastShootTime < _player.PlayerTypeData.ShootingCooldown)
            return;

        _lastShootTime = Time.time;
        Vector2Int position = transform.position.ToServer();
        NetworkManager.Instance.TcpSendMessageToServer(string.Format("S;{0};{1};{2};", position.x, position.y, Mathf.RoundToInt(_player.Visual.transform.eulerAngles.z)));
    }

    private void SendTransformData()
    {
        if (!NetworkManager.Instance.IsConnected)
            return;

        Vector2Int position = transform.position.ToServer();
        NetworkManager.Instance.UdpSendMessageToServer(string.Format("{0};{1};{2};{3};", NetworkManager.Instance.PlayerID, position.x, position.y, Mathf.RoundToInt(_player.Visual.transform.eulerAngles.z)));
    }
}
