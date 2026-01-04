using System;
using UnityEngine;

public class Player : MonoBehaviour
{
    public event Action HealthUpdated;
    public event Action Initialized;

    public bool IsInitialized { get; private set; }

    public PlayerTypeData PlayerTypeData { get; private set; }
    public string PlayerName { get; private set; }
    public int Health { get; private set; }


    private Vector3 _currentMovementDirection;

    public void Init(PlayerTypeData playerTypeData, string playerName)
    {
        PlayerTypeData = playerTypeData;
        Health = playerTypeData.StartingHealth;
        PlayerName = playerName;

        Initialized?.Invoke();
        HealthUpdated?.Invoke();
    }

    private void Update()
    {
        if (PlayerTypeData == null)
            return;
        transform.position += PlayerTypeData.MovementSpeed * Time.deltaTime * _currentMovementDirection;
    }

    public void SetPositionAndRotation(Vector3 position, float rotation, Vector3 currentMovementDirection)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(new Vector3(0,0,rotation)));
    }

    public void ReceiveDamage(int damage)
    {
        Health -= damage;
        HealthUpdated?.Invoke();
    }
}
