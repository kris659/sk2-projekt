using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private TMP_InputField _adressInputField;
    [SerializeField] private TMP_InputField _portInputField;

    [SerializeField] private Button _joinButton;

    private GameObject _content;

    private void Awake()
    {
        _content = transform.GetChild(0).gameObject;
        _joinButton.onClick.AddListener(OnJoinButton);
    }

    private void OnJoinButton()
    {
        if(!int.TryParse(_portInputField.text, out int port)) {
            Debug.Log("Incorrect port!");
            return;
        }
        NetworkManager.Instance.ConnectToServer(_adressInputField.text, port);
        _content.SetActive(false);
    }
}
