using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerWorldUI : MonoBehaviour
{
    [SerializeField] private Image _healthBar; 
    [SerializeField] private TMP_Text _playerName;

    private Player _player;

    private void Awake()
    {
        _player = GetComponentInParent<Player>();
        if( _player == null) {
            Debug.LogError("Player Script not found!");
            Destroy(this);
            return;
        }

        _playerName.text = "Not initialized";

        _player.HealthUpdated += OnPlayerHealthUpdated;
        _player.Initialized += OnPlayerHealthUpdated;

        if (_player.IsInitialized) {
            OnPlayerHealthUpdated();
            UpdatePlayerName();
        }
    }

    private void OnPlayerHealthUpdated()
    {
        float value = _player.Health / _player.PlayerTypeData.StartingHealth;
        value = Mathf.Clamp01(value);
        _healthBar.fillAmount = value;
    }

    private void UpdatePlayerName()
    {
        _playerName.text = _player.PlayerName;
    }
}
