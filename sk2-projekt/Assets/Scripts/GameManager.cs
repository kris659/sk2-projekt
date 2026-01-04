using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private List<PlayerTypeData> _playerTypes;
    [SerializeField] private Transform _playersParent;

    private Dictionary<int, Player> _players = new Dictionary<int, Player>();

    private void Start()
    {
        NetworkManager.Instance.ConnectionEstablished += OnConnectionEstablished;
    }

    private void OnConnectionEstablished()
    {
        NetworkManager.Instance.ConnectionEstablished -= OnConnectionEstablished;
    }

    private void OnPlayerJoined(int playerId)
    {

    }

    private void SpawnNewPlayer(int playerType, Vector2Int position)
    {

    }
}
