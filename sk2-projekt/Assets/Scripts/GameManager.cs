using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform _playersParent;
    [SerializeField] private GameManager _playerPrefab;

    private void Start()
    {
        NetworkManager.Instance.ConnectionEstablished += OnConnectionEstablished;
    }

    private void OnDestroy()
    {
        NetworkManager.Instance.ConnectionEstablished -= OnConnectionEstablished;
    }

    private void OnConnectionEstablished()
    {
        NetworkManager.Instance.ConnectionEstablished -= OnConnectionEstablished;

    }

    private void OnPlayerJoined(int playerId)
    {

    }

    private void SpawnNewPlayer()
    {

    }
}
