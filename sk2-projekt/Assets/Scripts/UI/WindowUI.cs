using UnityEngine;

public class WindowUI : MonoBehaviour
{
    private GameObject _content;

    protected virtual void Awake()
    {
        _content = transform.GetChild(0).gameObject;
        Close();
    }

    public void Open()
    {
        _content.SetActive(true);
    }

    public void Close()
    {
        _content.SetActive(false);
    }
}
