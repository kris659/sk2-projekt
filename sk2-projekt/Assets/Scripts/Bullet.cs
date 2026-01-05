using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int PlayerId { get; private set; }   

    private float _speed = 1;

    public void Init(Vector3 position, float movementDirection, int playerId)
    {
        PlayerId = playerId;
        transform.GetChild(0).transform.localScale = Vector3.one;
        transform.position = position;
        transform.eulerAngles = new Vector3(0, 0, movementDirection);
    }

    private void Update()
    {
        transform.position += transform.right * Time.deltaTime * _speed;
    }
}
