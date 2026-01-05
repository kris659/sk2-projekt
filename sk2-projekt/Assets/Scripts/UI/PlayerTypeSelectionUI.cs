using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTypeSelectionUI : WindowUI
{
    [SerializeField] private List<Button> _buttons = new List<Button>();
    [SerializeField] private Button _disconnectButton;

    public int SelectedPlayerType { get; private set; } = -1;

    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < _buttons.Count - 1; i++)
        {
            int index = i;
            _buttons[i].onClick.AddListener(() => OnPlayerTypeSelected(index));
        }
        _disconnectButton.onClick.AddListener(OnDisconnectButtonPressed);
    }

    private void OnPlayerTypeSelected(int playerType)
    {
        SelectedPlayerType = playerType;
        GameManager.Instance.SpawnLocalPlayer(playerType);
        Close();
    }

    private void OnDisconnectButtonPressed()
    {
        Debug.Log("Disconnect button pressed");
        NetworkManager.Instance.Disconect();
    }
}
