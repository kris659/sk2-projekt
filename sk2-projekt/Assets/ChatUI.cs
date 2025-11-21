using TMPro;
using UnityEngine;

public class ChatUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _messageInputField;
    [SerializeField] private Transform _listParent;
    [SerializeField] private GameObject _messagePrefab;

    private void Start()
    {
        NetworkManager.Instance.MessageReceived += AddNewMessage;
        _messageInputField.onSubmit.AddListener(Send);
        _messagePrefab.SetActive(false);
    }

    private void AddNewMessage(string message)
    {
        GameObject messageGO = Instantiate(_messagePrefab, _listParent);
        messageGO.SetActive(true);
        messageGO.GetComponentInChildren<TMP_Text>().text = message;
    }

    private void Send(string message)
    {
        if (message == string.Empty)
            return;
        NetworkManager.Instance.SendMessageToServer(message);
        AddNewMessage("Ty: " + message);
    }
}
