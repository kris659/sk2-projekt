
public class UIManager : MonoBehaviourSingleton<UIManager>
{
    public LobbyUI LobbyUI { get; private set; }
    public PlayerTypeSelectionUI PlayerTypeSelectionUI { get; private set; }
    public InfoUI InfoUI { get; private set; }


    protected override void Awake()
    {
        base.Awake();
        LobbyUI = GetComponentInChildren<LobbyUI>(true);
        PlayerTypeSelectionUI = GetComponentInChildren<PlayerTypeSelectionUI>(true);
        InfoUI = GetComponentInChildren<InfoUI>(true);
    }
}

