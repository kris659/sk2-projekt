using UnityEngine;
using UnityEngine.Rendering;

public class Bullet : MonoBehaviour
{
    public int PlayerId { get; private set; }   

    private float _speed = 1;

    public void Init(Vector3 position, float movementDirection, int playerId, float speed)
    {
        PlayerId = playerId;
        transform.position = position;
        transform.eulerAngles = new Vector3(0, 0, movementDirection);
        _speed = speed;
    }

    private void Update()
    {
        transform.position += transform.right * Time.deltaTime * _speed;
    }
}
