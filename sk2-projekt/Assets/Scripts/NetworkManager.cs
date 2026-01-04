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

    private Socket _tcpServerConnection;
    private Socket _udpServerConnection;
    private EndPoint _udpRemoteEndpoint;
    private UdpClient _udpClient;

    public async void ConnectToServer(string adress, int port)
    {
        return;


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
        Debug.Log("Received TCP init message: " + message);
        string[] messageParts = message.Split(':');

        if (!int.TryParse(messageParts[0], out int udpPort)) {
            Debug.LogError("Failed parsing a udp port: " +  messageParts[0]);
            return;
        }
        if (!int.TryParse(messageParts[1], out int playerID)) {
            Debug.LogError("Failed parsing a playerID: " + messageParts[1]);
            return;
        }
        PlayerID = playerID;

        //Creates a UdpClient for reading incoming data.
        UdpClient receivingUdpClient = new UdpClient(11000);

        //Creates an IPEndPoint to record the IP Address and port number of the sender.
        // The IPEndPoint will allow you to read datagrams sent from any source.
        IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
        try {

            // Blocks until a message returns on this socket from a remote host.
            Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);

            string returnData = Encoding.ASCII.GetString(receiveBytes);

            Console.WriteLine("This is the message you received " +
                                      returnData.ToString());
            Console.WriteLine("This message was sent from " +
                                        RemoteIpEndPoint.Address.ToString() +
                                        " on their port number " +
                                        RemoteIpEndPoint.Port.ToString());
        }
        catch (Exception e) {
            Console.WriteLine(e.ToString());
        }


        //_udpRemoteEndpoint = new IPEndPoint(((IPEndPoint)_tcpServerConnection.RemoteEndPoint).Address, udpPort);
        //_udpServerConnection = new(
        //    _udpRemoteEndpoint.AddressFamily,
        //    SocketType.Dgram,
        //    ProtocolType.Udp);


        //await _udpServerConnection.ConnectAsync(_udpRemoteEndpoint);

        //Debug.Log($"Player ID: {playerID}, udp port: {udpPort}");



        //_udpClient = new UdpClient(0);
        //Debug.Log("Local UDP endpoint: " + _udpClient.Client.LocalEndPoint);
        //await _udpClient.ReceiveAsync();
        Debug.Log("Received");


        //_udpClient.Connect(ipAddr, udpPort);

        //Debug.Log("Connection TCP and UDP");

        //IsConnectionEstablished = true;
        //ConnectionEstablished?.Invoke();

        //Debug.Log("Local UDP endpoint: " + _udpClient.Client.LocalEndPoint);
        //Debug.Log("Local UDP endpoint: " + _udpServerConnection.LocalEndPoint);
        //Debug.Log($"Sending UDP to {ipAddr}:{udpPort}");

        //ReceiveMessagesLoop(_tcpServerConnection, TcpMessageReceived);
        //ReceiveMessagesLoop(_updServerConnection, UdpMessageReceived);
        //StartUdpReceiveLoop();
    }

    private async void ReceiveMessagesLoop(Socket socket, Action<string> receivedEvent)
    {
        while (socket != null && socket.Connected) {
            string message = await ReceiveMessage(socket);

            Debug.Log($"Recived: {message}");
            receivedEvent?.Invoke(message);
        }
    }

    //private async void StartUdpReceiveLoop()
    //{
    //    while (true) {
    //        var result = await _udpClient.ReceiveAsync();
    //        string msg = Encoding.UTF8.GetString(result.Buffer);

    //        Debug.Log($"UDP received: {msg}");
    //        UdpMessageReceived?.Invoke(msg);
    //    }
    //}

    public async void UdpSendMessageToServer(string message)
    {
        //var messageBytes = Encoding.UTF8.GetBytes(message);
        //_ = await _updServerConnection.SendToAsync(new ArraySegment<byte>(messageBytes), SocketFlags.None, _udpRemoteEndpoint);
        //Debug.Log($"Sent UDP message: \"{message}\"");

        //UdpClient udpClient = new UdpClient();
        //Byte[] sendBytes = Encoding.ASCII.GetBytes("Is anybody there");

        //Debug.Log("Sending message...");
        //Debug.Log($"Local UDP port: {((IPEndPoint)_udpClient.Client.LocalEndPoint).Port}");
        //Debug.Log($"Remote UDP port: {((IPEndPoint)_udpClient.Client.RemoteEndPoint).Port}");

        
        //_ = await _udpClient.SendAsync(sendBytes, sendBytes.Length);

        //Debug.Log("Sent");
    }

    public async void TcpSendMessageToServer(string message)
    {
        Debug.Log("Sending message...");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = await _tcpServerConnection.SendAsync(messageBytes, SocketFlags.None);
        Debug.Log($"Sent TCP message: \"{message}\"");
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