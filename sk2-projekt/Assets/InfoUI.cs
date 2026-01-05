using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoUI : WindowUI
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Button _confirmButton;

    override protected void Awake()
    {
        base.Awake();
        _confirmButton.onClick.AddListener(OnConfirmButtonClicked);
    }

    public void Open(string text)
    {
        _text.text = text;
        Open();
    }

    private void OnConfirmButtonClicked()
    {
        _text.text = "";
        Close();
    }
}
