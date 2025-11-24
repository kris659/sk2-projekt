using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class NetworkManager : MonoBehaviourSingleton<NetworkManager>
{

    public event Action ConnectedToServer;
    public event Action<string> MessageReceived;

    private Socket _serverConnection;
    private Socket _serverConnectionUDP;
    public async void ConnectToServer(string adress, int port)
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        if (!IPAddress.TryParse(adress, out IPAddress ipAddr)){
            Debug.Log("Incorrect IP adress");
            return;
        }
        IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, port);

        //_connection = 
        _serverConnection = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        _serverConnectionUDP = new(
            ipEndPoint.AddressFamily,
            SocketType.Dgram,
            ProtocolType.Udp);

        MessageReceived?.Invoke("Trying to connect...");
        await _serverConnection.ConnectAsync(ipEndPoint);
        MessageReceived?.Invoke("Connected to server...");
        // Receive messages
        while (_serverConnection != null && _serverConnection.Connected) {
            var buffer = new byte[1_024];
            var received = await _serverConnection.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            Debug.Log($"Recived: {response}");
            MessageReceived?.Invoke(response);
        }
    }

    public async void UDPSendMessageToServer(string message)
    {
        Debug.Log("Sending message...");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _serverConnection.SendToAsync(new ArraySegment<byte>(messageBytes), SocketFlags.None, _serverConnectionUDP.RemoteEndPoint);
        Debug.Log($"Socket client sent message: \"{message}\"");
    }

    public async void TCPSendMessageToServer(string message)
    {
        Debug.Log("Sending message...");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _serverConnection.SendAsync(messageBytes, SocketFlags.None);
        Debug.Log($"Socket client sent message: \"{message}\"");
    }

    private void OnDestroy()
    {
        if (_serverConnection != null) {
            if(_serverConnection.Connected)
                _serverConnection.Shutdown(SocketShutdown.Both);
            _serverConnection.Dispose();
        }
    }
}