using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float _speed;
    private Vector3 _movementDirection;

    public void Init(float size, float speed, Vector2 movementDirection)
    {
        transform.GetChild(0).transform.localScale = Vector3.one * size;
        _speed = speed;
        _movementDirection = movementDirection;
    }

    private void Update()
    {
        transform.position += Time.deltaTime * _speed * _movementDirection;
    }
}
