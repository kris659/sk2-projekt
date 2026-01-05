using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    public event Action PlayerScoresUpdated;

    [SerializeField] private List<PlayerTypeData> _playerTypes;
    [SerializeField] private CinemachineCamera _playerCamera;
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Transform _playersParent;

    public IReadOnlyDictionary<int, Player> Players => _players;
    private Dictionary<int, Player> _players = new ();
    private Dictionary<int, Bullet> _bullets = new ();
    public Player LocalPlayer { get; private set; }

    private void Start()
    {
        Application.targetFrameRate = 60;

        NetworkManager.Instance.Connected += Connected;
        NetworkManager.Instance.Disconnected += Disconected;
        NetworkManager.Instance.TcpMessageReceived += TcpMessageReceived;
        NetworkManager.Instance.UdpMessageReceived += UdpMessageReceived;
        UIManager.Instance.LobbyUI.Open();
    }

    private void OnDestroy()
    {
        if(NetworkManager.Instance == null)
            return;
        NetworkManager.Instance.Connected -= Connected;
        NetworkManager.Instance.Disconnected -= Disconected;
        NetworkManager.Instance.TcpMessageReceived -= TcpMessageReceived;
        NetworkManager.Instance.UdpMessageReceived -= UdpMessageReceived;
    }

    private void Connected()
    {
        //UIManager.Instance.LeaderboardUI.Open();
    }

    private void Disconected()
    {
        foreach(var player in _players.Values) {
            if(player == null)
                continue;
            Destroy(player.gameObject);
        }
        foreach(var bullet in _bullets.Values) {
            if (bullet == null)
                continue;            
            Destroy(bullet.gameObject);
        }
        _players.Clear();
        _bullets.Clear();
        LocalPlayer = null;

        if(UIManager.Instance != null && UIManager.Instance.LeaderboardUI) 
            UIManager.Instance.LeaderboardUI.Close();
    }

    public void SpawnLocalPlayer(int playerTypeIndex)
    {
        UIManager.Instance.LeaderboardUI.Open();
        PlayerTypeData playerTypeData = _playerTypes[playerTypeIndex];
        NetworkManager.Instance.TcpSendMessageToServer($"I;" +
            $"{NetworkManager.Instance.UdpLocalPort};" +
            $"{UIManager.Instance.LobbyUI.PlayerName};" +
            $"{playerTypeIndex};{playerTypeData.BulletDamage};" +
            $"{playerTypeData.BulletSpeed};" +
            $"{playerTypeData.StartingHealth};");
    }

    private void TcpMessageReceived(string message)
    {
        string[] parts = message.Split(";");

        if (parts.Length < 1) {
            Debug.LogWarning("Incorrect TCP message format!");
            return;
        }

        string trimmedMessage = message.Substring(1).Trim(';');
        switch (parts[0]) {
            case "I":
                InitLocalPlayer(trimmedMessage);
                break;
            case "M":
                InitPlayers(trimmedMessage);
                break;
            case "C":
                AddRemotePlayer(trimmedMessage);
                break;
            case "D":
                RemovePlayer(trimmedMessage);
                break;
            case "S":
                SpawnBullet(trimmedMessage);
                break;
            case "H":
                PlayerHit(trimmedMessage);
                break;
            case "E":
                RemoveBullet(trimmedMessage);
                break;
            case "P":
                UpdatePlayerScores(trimmedMessage);
                break;
        }
    }

    private void UpdatePlayerScores(string message)
    {
        //for (const auto&i : players){
        //    scoreUpdate = scoreUpdate + std::to_string(i.second.playerId) + ";" + std::to_string(i.second.points) + "!";
        //}
        //scoreUpdate += "~";

        string[] playersData = message.Split("!");

        foreach (string playerData in playersData) {
            if(playerData.Trim() == string.Empty)
                continue;

            string[] parts = playerData.Split(";");

            if(parts.Length < 2) {
                Debug.LogWarning("[GM] Incorrect player score update format!: " + string.Join(',', parts));
                continue;
            }
            if (!int.TryParse(parts[0], out int playerId)) {
                Debug.LogWarning("[GM] Failed to parse player ID: " + parts[0]);
                continue;
            }
            if (!int.TryParse(parts[1], out int score)) {
                Debug.LogWarning("[GM] Failed to parse player score: " + parts[1]);
                continue;
            }

            if (!_players.ContainsKey(playerId)) {
                Debug.LogWarning($"Player with ID:{playerId} doesn't exist");
                continue;
            }

            Player player = _players[playerId];
            player.SetScore(score);
        }
        PlayerScoresUpdated?.Invoke();
    }

    private void RemoveBullet(string message)
    {
        string[] parts = message.Split(";");
        if (parts.Length > 0 && int.TryParse(parts[0], out int bulletId)) {
            if(_bullets.TryGetValue(bulletId, out Bullet bullet)) {
                Destroy(bullet.gameObject);
                _bullets.Remove(bulletId);
            }
        }
    }

    private void PlayerHit(string message)
    {
        string[] parts = message.Split(";");
        if (parts.Length < 3) {
            Debug.LogWarning("Incorrect player hit message format! " + string.Join(',', parts));
            return;
        }
        if (!int.TryParse(parts[0], out int playerId)) {
            Debug.LogWarning("[GM] Failed to parse player ID: " + parts[0]);
            return;
        }
        if (!int.TryParse(parts[1], out int bulletId)) {
            Debug.LogWarning("[GM] Failed to parse bullet ID: " + parts[1]);
            return;
        }
        if (!int.TryParse(parts[2], out int health)) {
            Debug.LogWarning("[GM] Failed to parse health: " + parts[2]);
            return;
        }
        if (!int.TryParse(parts[3], out int isAliveInt)) {
            Debug.LogWarning("[GM] Failed to parse isAlive: " + parts[3]);
            return;
        }

        if(!_bullets.ContainsKey(bulletId)) {
            Debug.LogWarning($"Bullet with ID:{bulletId} doesn't exist");
            return;
        }

        Bullet bullet = _bullets[bulletId];
        Destroy(bullet.gameObject);

        if (!_players.ContainsKey(playerId)) {
            Debug.LogWarning($"Player with ID:{playerId} doesn't exist");
            return;
        }

        Player player = _players[playerId];
        player.SetHealth(health);

        if (isAliveInt == 0) {
            PlayerDeath(playerId);
            return;
        }
    }

    private void PlayerDeath(int playerId)
    {
        if(!_players.ContainsKey(playerId)) {
            Debug.LogWarning($"Player with ID:{playerId} doesn't exist");
            return;
        }
        Player player = _players[playerId];
        Destroy(player.gameObject);

        if(playerId == NetworkManager.Instance.PlayerID) {
            UIManager.Instance.InfoUI.Open("You died", "Play again", UIManager.Instance.PlayerTypeSelectionUI.Open);
        } else {
            _players.Remove(playerId);
        }
    }

    private void SpawnBullet(string message)
    {
        string[] parts = message.Split(";");
        if (parts.Length < 5) {
            Debug.LogWarning("Incorrect bullet spawn message format! " + string.Join(',', parts));
            return;
        }
        if (!int.TryParse(parts[0], out int bulletId)) {
            Debug.LogWarning("[GM] Failed to parse bullet ID: " + parts[0]);
            return;
        }
        if (!int.TryParse(parts[1], out int shooterId)) {
            Debug.LogWarning("[GM] Failed to parse shooter ID: " + parts[1]);
            return;
        }

        if (!int.TryParse(parts[2], out int positionX)) {
            Debug.LogWarning("[GM] Failed to parse position X: " + parts[2]);
            return;
        }
        if (!int.TryParse(parts[3], out int positionY)) {
            Debug.LogWarning("[GM] Failed to parse position Y: " + parts[3]);
            return;
        }
        if (!int.TryParse(parts[4], out int rotation)) {
            Debug.LogWarning("[GM] Failed to parse rotation: " + parts[4]);
            return;
        }

        GameObject bulletObject = Instantiate(_bulletPrefab);
        Bullet bullet = bulletObject.GetComponent<Bullet>();
        _bullets[bulletId] = bullet;

        Vector2Int position = new Vector2Int(positionX, positionY);

        if(!_players.ContainsKey(shooterId)) {
            Debug.LogWarning($"Player with ID:{shooterId} doesn't exist");
            return;
        }

        float speed = _players[shooterId].PlayerTypeData.BulletSpeed;
        bullet.Init(position.ToLocal(), rotation, bulletId, speed);
    }

    private void RemovePlayer(string message)
    {
        Debug.Log("Remove player from message: " + message);
        if (!int.TryParse(message, out int playerId)) {
            Debug.LogWarning("[GM] Failed to parse player ID: " + message);
            return;
        }
        if (!_players.ContainsKey(playerId)) {
            Debug.LogWarning($"Player with ID:{playerId} doesn't exist");
            return;
        }
        Player player = _players[playerId];
        Destroy(player.gameObject);
        _players.Remove(playerId);
    }

    private void UdpMessageReceived(string message)
    {
        string[] playersData = message.TrimStart('M', ';').Split("!");

        foreach (string playerData in playersData) {
            if(playerData == string.Empty)
                continue;

            string[] parts = playerData.Split(";");
            if (parts.Length < 4) {
                Debug.LogWarning("[GM] Incorrect udp message format!: " + string.Join(',', parts));
                continue;
            }
            if (!int.TryParse(parts[0], out int playerId)) {
                Debug.LogWarning("[GM] Failed to parse player ID: " + parts[0]);
                continue;
            }
            if (!int.TryParse(parts[1], out int positionX)) {
                Debug.LogWarning("[GM] Failed to parse positionX");
                continue;
            }
            if (!int.TryParse(parts[2], out int positionY)) {
                Debug.LogWarning("[GM] Failed to parse position Y");
                continue;
            }
            if (!int.TryParse(parts[3], out int rotation)) {
                Debug.LogWarning("[GM] Failed to parse rotation");
                continue;
            }

            if (!_players.ContainsKey(playerId)) {
                Debug.LogWarning($"Player with ID:{playerId} doesn't exist");
                continue;
            }

            Player player = _players[playerId];

            if (LocalPlayer == player)
                continue;

            Vector2Int position = new Vector2Int(positionX, positionY);
            player.SetPositionAndRotation(position.ToLocal(), rotation);
        }
    }

    private void InitLocalPlayer(string message)
    {
        Debug.Log("Init local player from message: " + message);
        string[] parts = message.Split(";");

        if(parts.Length < 2) {
            Debug.LogWarning("[GM] Incorrect local player init message format!");
            return;
        }

        Vector2Int position = new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        SpawnLocalPlayer(position.ToLocal(), UIManager.Instance.PlayerTypeSelectionUI.SelectedPlayerType);
    }

    private void SpawnLocalPlayer(Vector3 position, int playerTypeIndex)
    {
        GameObject playerObject = Instantiate(_playerPrefab, _playersParent);
        playerObject.AddComponent<PlayerController>();

        LocalPlayer = playerObject.GetComponent<Player>();
        LocalPlayer.Init(_playerTypes[playerTypeIndex], UIManager.Instance.LobbyUI.PlayerName);

        LocalPlayer.SetPositionAndRotation(position, 0f);
        _players[NetworkManager.Instance.PlayerID] = LocalPlayer;
        _playerCamera.Target.TrackingTarget = LocalPlayer.transform;
    }

    private void SpawnRemotePlayer(Vector3 position, int playerTypeIndex, int playerId, string playerName)
    {
        GameObject playerObject = Instantiate(_playerPrefab, _playersParent);

        Player player = playerObject.GetComponent<Player>();
        player.Init(_playerTypes[playerTypeIndex], playerName);
        player.SetPositionAndRotation(position, 0f);
        _players[playerId] = player;
    }

    private void AddRemotePlayer(string message)
    {
        if (message == string.Empty)
            return;
        string[] parts = message.Split(";");
        if (!int.TryParse(parts[0], out int playerId)) {
            Debug.LogWarning("[GM] Failed to parse player ID: " + parts[0]);
            return;
        }
        string playerName = parts[1];

        if (!int.TryParse(parts[2], out int playerTypeIndex)) {
            Debug.LogWarning("[GM] Failed to parse playerType: " + parts[2]);
            return;
        }

        if (!int.TryParse(parts[3], out int positionX)) {
            Debug.LogWarning("[GM] Failed to parse position X: " + parts[3]);
            return;
        }

        if (!int.TryParse(parts[4], out int positionY)) {
            Debug.LogWarning("[GM] Failed to parse position Y");
            return;
        }
        if (!int.TryParse(parts[5], out int rotation)) {
            Debug.LogWarning("[GM] Failed to parse rotation");
            return;
        }

        if (NetworkManager.Instance.PlayerID == playerId)
            return;

        if (_players.ContainsKey(playerId)) {
            Debug.LogWarning($"Player with ID:{playerId} aleardy exist");
            return;
        }

        Vector2Int position = new Vector2Int(positionX, positionY);
        SpawnRemotePlayer(position.ToLocal(), playerTypeIndex, playerId, playerName);
    }

    private void InitPlayers(string message)
    {
        Debug.Log("Init players from message: " + message);
        string[] playersData = message.Split("!");

        foreach (string playerData in playersData) {
            AddRemotePlayer(playerData);
        }
    }
}
