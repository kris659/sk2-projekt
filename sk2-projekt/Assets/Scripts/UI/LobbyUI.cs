using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : WindowUI
{
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private TMP_InputField _adressInputField;
    [SerializeField] private TMP_InputField _portInputField;

    [SerializeField] private Button _joinButton;

    public string PlayerName => _nameInputField.text;   

    protected override void Awake()
    {
        base.Awake();
        _joinButton.onClick.AddListener(OnJoinButton);
        _nameInputField.onValueChanged.AddListener(OnPlayerNameValueChanged);
    }

    private void OnJoinButton()
    {
        if(!int.TryParse(_portInputField.text, out int port)) {
            UIManager.Instance.InfoUI.Open("Incorrect port!", "Ok", null);
            Debug.Log("Incorrect port!");
            return;
        }
        if(_nameInputField.text.Length == 0) {
            UIManager.Instance.InfoUI.Open("Enter your name!", "Ok", null);
            Debug.Log("Please enter your name!");
            return;
        }

        if (!IPAddress.TryParse(_adressInputField.text, out IPAddress ipAddr)) {
            UIManager.Instance.InfoUI.Open("Incorrect IP adress!", "Ok", null);
            Debug.Log("Incorrect IP adress");
            return;
        }

        _nameInputField.text.Replace('~', '-');
        _nameInputField.text.Replace(':', '.');
        _nameInputField.text.Replace(';', '.');

        NetworkManager.Instance.ConnectToServer(ipAddr, port, _nameInputField.text);
        Close();
    }

    private void OnPlayerNameValueChanged(string value)
    {
        _nameInputField.text = value.Replace('~', '.').Replace(':', '.').Replace(';', '.').Replace('!', '.');
    }
}
