using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>
{
    public event Action ConnectionEstablished;
    public event Action<string> TcpMessageReceived;
    public event Action<string> UdpMessageReceived;

    public int PlayerID { get; private set; }   
    public bool IsConnectionEstablished { get; private set; }
    public int UdpLocalPort { get; private set; }

    private Socket _tcpSocket;
    private UdpClient _udpClient;

    private bool _isRunning = true;

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

        await _tcpSocket.ConnectAsync(ipEndPoint);
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

        TcpReceiveLoop();
        UdpReceiveLoop();

        IPEndPoint localEndPoint = (IPEndPoint)_udpClient.Client.LocalEndPoint;
        UdpLocalPort = localEndPoint.Port;

        IsConnectionEstablished = true;
        ConnectionEstablished?.Invoke();
    }

    private async void UdpReceiveLoop()
    {
        Debug.Log("[UDP] Started read loop...");
        while (_isRunning) {
            try {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(result.Buffer);
                msg = msg.TrimEnd('~');
                //Debug.Log("[UDP] Received: " + msg);
                UdpMessageReceived?.Invoke(msg);
            }
            catch (SocketException ex) {
                Debug.LogError("[UDP] Socket exception: " + ex.Message);
            }
        }
    }

    private async void TcpReceiveLoop()
    {
        Debug.Log("[TCP] Started read loop...");
        string message = "";
        while (_isRunning) {
            try {
                while (message.IndexOf('~') == -1) {
                    string part = await TcpReceiveMessage();
                    Debug.Log("[TCP] Received part: " + part);
                    if (part == string.Empty) {
                        Disconect();
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
        catch (SocketException ex) {
            if(_isRunning)
                Debug.LogError("[TCP] Socket exception: " + ex.Message);
            return string.Empty;
        }
    }

    public void Disconect()
    {
        Debug.Log("Disconnecting from server...");
        _isRunning = false;
        _udpClient?.Close();
        _tcpSocket?.Shutdown(SocketShutdown.Both);
        _tcpSocket?.Close();
        _tcpSocket?.Dispose();
    }

    private void OnApplicationQuit()
    {
        Disconect();
    }
}