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

    private Socket _tcpServerConnection;
    private Socket _updServerConnection;

    public async void ConnectToServer(string adress, int port)
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        if (!IPAddress.TryParse(adress, out IPAddress ipAddr)){
            Debug.Log("Incorrect IP adress");
            return;
        }
        IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, port);

        _tcpServerConnection = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        await _tcpServerConnection.ConnectAsync(ipEndPoint);

        string message = await ReceiveMessage(_tcpServerConnection);
        string[] messageParts = message.Split(':');

        int udpPort = int.Parse(messageParts[0]);
        PlayerID = int.Parse(messageParts[1]);

        IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)_tcpServerConnection.RemoteEndPoint).Address, udpPort);
        _updServerConnection = new(
            ipEndPoint.AddressFamily,
            SocketType.Dgram,
            ProtocolType.Udp);

        await _updServerConnection.ConnectAsync(udpEndPoint);

        ConnectionEstablished?.Invoke();

        ReceiveMessagesLoop(_tcpServerConnection, TcpMessageReceived);
        ReceiveMessagesLoop(_updServerConnection, UdpMessageReceived);
    }

    private async void ReceiveMessagesLoop(Socket socket, Action<string> receivedEvent)
    {
        while (socket != null && socket.Connected) {
            string message = await ReceiveMessage(socket);

            Debug.Log($"Recived: {message}");
            receivedEvent?.Invoke(message);
        }
    }

    public async void UdpSendMessageToServer(string message)
    {
        Debug.Log("Sending message...");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _updServerConnection.SendToAsync(new ArraySegment<byte>(messageBytes), SocketFlags.None, _updServerConnection.RemoteEndPoint);
        Debug.Log($"Socket client sent message: \"{message}\"");
    }

    public async void TcpSendMessageToServer(string message)
    {
        Debug.Log("Sending message...");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _tcpServerConnection.SendAsync(messageBytes, SocketFlags.None);
        Debug.Log($"Socket client sent message: \"{message}\"");
    }

    private async Task<string> ReceiveMessage(Socket socket)
    {
        var buffer = new byte[1_024];
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
        return Encoding.UTF8.GetString(buffer, 0, received);
    }


    private void OnDestroy()
    {
        if (_tcpServerConnection != null) {
            if(_tcpServerConnection.Connected)
                _tcpServerConnection.Shutdown(SocketShutdown.Both);
            _tcpServerConnection.Dispose();
        }
    }
}