using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class LeaderboardUI : WindowUI
{
    [SerializeField] private Transform _leaderboardListParent;
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _localPlayerColor;
    private GameObject[] _children;

    private void Start()
    {
        _children = new GameObject[_leaderboardListParent.childCount];
        for (int i = 0; i < _leaderboardListParent.childCount; i++)
        {
            _children[i] = _leaderboardListParent.GetChild(i).gameObject;
            _children[i].SetActive(false);
        }

        GameManager.Instance.PlayerScoresUpdated += UpdateLeaderboard;
        UpdateLeaderboard();
    }

    private void UpdateLeaderboard()
    {
        List<Player> players = GameManager.Instance.Players.Values.ToList();

        players.Sort((p1, p2) => p2.Score.CompareTo(p1.Score));

        for(int i = 0; i < _children.Length; i++)
        {
            _children[i].SetActive(false);
        }

        for (int i = 0; i < _children.Length &&  i < players.Count; i++)
        {
            _children[i].transform.GetChild(0).GetComponent<TMP_Text>().text = (i + 1) + " " + players[i].PlayerName;
            _children[i].transform.GetChild(0).GetComponent<TMP_Text>().color = GameManager.Instance.LocalPlayer == players[i] ? _localPlayerColor : _normalColor;
            _children[i].transform.GetChild(1).GetComponent<TMP_Text>().text = players[i].Score.ToString();
            _children[i].SetActive(true);
        }
    }
}
