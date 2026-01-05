using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>
{
    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> TcpMessageReceived;
    public event Action<string> UdpMessageReceived;

    public int PlayerID { get; private set; }   
    public bool IsConnected { get; private set; }
    public int UdpLocalPort { get; private set; }

    [SerializeField] private Button _disconnectButton;

    private Socket _tcpSocket;
    private UdpClient _udpClient;

    private CancellationTokenSource _connectCancelToken;

    private const int TIMEOUT_SECONDS = 3;
    private float _lastReceiveTime;

    private void Start()
    {
        _disconnectButton.onClick.AddListener(() => Disconect());

        TcpMessageReceived += (_) => _lastReceiveTime = Time.time;
        UdpMessageReceived += (_) => _lastReceiveTime = Time.time;
    }

    private void Update()
    {
        if (IsConnected && Time.time - _lastReceiveTime > TIMEOUT_SECONDS) {
            Debug.Log("Connection timed out due to inactivity.");
            Disconect();
            if(UIManager.Instance != null && UIManager.Instance.InfoUI != null)
                UIManager.Instance.InfoUI.Open("Connection timed out.", "OK", null);
        }
    }

    public async void ConnectToServer(string adress, int port, string playerName)
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        if (!IPAddress.TryParse(adress, out IPAddress ipAddr)){
            Debug.Log("Incorrect IP adress");
            return;
        }
        IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, port);

        _tcpSocket = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        _connectCancelToken = new CancellationTokenSource(); 
        var userToken = _connectCancelToken.Token;

        var connectTask = _tcpSocket.ConnectAsync(ipEndPoint);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var cancelTask = Task.Delay(Timeout.InfiniteTimeSpan, userToken);

        UIManager.Instance.InfoUI.Open($"Hello {playerName}!\nConnecting to {adress}:{port}...", "Cancel", _connectCancelToken.Cancel);

        try {
            var finished = await Task.WhenAny(connectTask, timeoutTask, cancelTask);
            UIManager.Instance.InfoUI.Close();

            if (finished == timeoutTask) {
                Disconect();
                UIManager.Instance.InfoUI.Open("Connection timed out.", "OK", null);
                return;
            }
            if (finished == cancelTask) {
                Disconect();
                Debug.Log("Connection attempt cancelled");
                return;
            }
            await connectTask;
        }
        catch (Exception ex) {
            Disconect();
            UIManager.Instance.InfoUI.Open($"Failed to connect to server:\n{ex.Message}", "OK", null);
            return;
        }

        if (!_tcpSocket.Connected) {
            Disconect();
            Debug.Log("Failed to connect to server");
            return;
        }

        UIManager.Instance.LobbyUI.Close();
        UIManager.Instance.PlayerTypeSelectionUI.Open();

        Debug.Log("Connected to server via TCP");
        string message = await TcpReceiveMessage();
        Debug.Log("Received TCP init message: " + message);
        string[] messageParts = message.Trim('~').Split(';');

        if (!int.TryParse(messageParts[0], out int udpPort)) {
            Debug.LogError("Failed parsing a udp port: " +  messageParts[0]);
            return;
        }
        if (!int.TryParse(messageParts[1], out int playerID)) {
            Debug.LogError("Failed parsing a playerID: " + messageParts[1]);
            return;
        }

        PlayerID = playerID;

        Debug.Log($"Player ID: {playerID}, udp port: {udpPort}");

        _udpClient = new UdpClient();
        _udpClient.Connect(adress, udpPort);

        IPEndPoint localEndPoint = (IPEndPoint)_udpClient.Client.LocalEndPoint;
        UdpLocalPort = localEndPoint.Port;

        _lastReceiveTime = Time.time;
        IsConnected = true;

        TcpReceiveLoop();
        UdpReceiveLoop();
        Connected?.Invoke();
        _disconnectButton.gameObject.SetActive(true);
    }

    private async void UdpReceiveLoop()
    {
        Debug.Log("[UDP] Started read loop...");
        while (IsConnected) {
            try {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(result.Buffer);
                msg = msg.TrimEnd('~');
                //Debug.Log("[UDP] Received: " + msg);
                UdpMessageReceived?.Invoke(msg);
            }
            catch (Exception ex) {
                if(IsConnected)
                    Debug.LogError("[UDP] Read exception: " + ex.Message);
            }
        }
    }

    private async void TcpReceiveLoop()
    {
        Debug.Log("[TCP] Started read loop...");
        string message = "";
        while (IsConnected) {
            try {
                while (message.IndexOf('~') == -1) {
                    string part = await TcpReceiveMessage();
                    Debug.Log("[TCP] Received part: " + part);
                    if (part == string.Empty) {
                        Disconect();
                        if(UIManager.Instance != null && UIManager.Instance.InfoUI != null)
                            UIManager.Instance.InfoUI.Open("Disconnected from server.", "OK", null);
                        return;
                    }
                    message += part;
                }
                string messageToProcess = message.Substring(0, message.IndexOf('~'));
                if(message.IndexOf('~') + 1 >= message.Length)
                    message = "";
                else
                    message = message.Substring(message.IndexOf('~') + 1);
                Debug.Log("[TCP] Received: " + messageToProcess);
                TcpMessageReceived?.Invoke(messageToProcess);
            }
            catch (SocketException ex) {
                Debug.LogError("[TCP] Socket exception: " + ex.Message);
            }
        }
    }

    public async void UdpSendMessageToServer(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await _udpClient.SendAsync(data, data.Length);
        //Debug.Log($"[UDP] Sent message: {message}");
    }

    public async void TcpSendMessageToServer(string message)
    {
        message += '~';
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _tcpSocket.SendAsync(messageBytes, SocketFlags.None);
        Debug.Log($"[TCP] Sent message: \"{message}\"");
    }

    private async Task<string> TcpReceiveMessage()
    {
        try {
            var buffer = new byte[1_024];
            var received = await _tcpSocket.ReceiveAsync(buffer, SocketFlags.None);
            return Encoding.UTF8.GetString(buffer, 0, received);
        }
        catch (Exception ex) {
            if(IsConnected)
                Debug.LogError("[TCP] Read exception: " + ex.Message);
        }
        return string.Empty;
    }

    public void Disconect(bool openUI = true)
    {
        if(_disconnectButton != null)
            _disconnectButton.gameObject.SetActive(false);
        Debug.Log("Disconnecting from server...");
        Disconnected?.Invoke();
        IsConnected = false;
        _connectCancelToken?.Cancel();
        _udpClient?.Close();
        if(_tcpSocket != null && _tcpSocket.Connected)
            _tcpSocket?.Shutdown(SocketShutdown.Both);
        _tcpSocket?.Close();
        _tcpSocket?.Dispose();

        if(UIManager.Instance != null) {
            if(UIManager.Instance.InfoUI != null)
                UIManager.Instance.InfoUI.Close();
            if(UIManager.Instance.PlayerTypeSelectionUI != null)
                UIManager.Instance.PlayerTypeSelectionUI.Close();
            if(UIManager.Instance.LobbyUI != null)
                UIManager.Instance.LobbyUI.Close();
            if (openUI && UIManager.Instance.LobbyUI != null)
                UIManager.Instance.LobbyUI.Open();
        }
    }

    private void OnDestroy()
    {
        Disconect(false);
    }
}