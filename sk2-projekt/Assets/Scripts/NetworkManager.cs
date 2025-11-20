using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public async static void ConnectToServer(string adress, int port)
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        if (!IPAddress.TryParse(adress, out IPAddress ipAddr)){
            Debug.Log("Incorrect IP adress");
            return;
        }
        IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, port);

        using Socket client = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        await client.ConnectAsync(ipEndPoint);
        while (true) {
            // Send message.
            var message = "Hi friends !<|EOM|>";
            var messageBytes = Encoding.UTF8.GetBytes(message);
            _ = await client.SendAsync(messageBytes, SocketFlags.None);
            Console.WriteLine($"Socket client sent message: \"{message}\"");

            // Receive ack.
            var buffer = new byte[1_024];
            var received = await client.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            if (response == "<|ACK|>") {
                Debug.Log($"Socket client received acknowledgment: \"{response}\"");
                break;
            }
            // Sample output:
            //     Socket client sent message: "Hi friends !<|EOM|>"
            //     Socket client received acknowledgment: "<|ACK|>"
        }

        client.Shutdown(SocketShutdown.Both);
    }
}