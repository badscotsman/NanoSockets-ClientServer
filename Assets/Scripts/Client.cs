using NanoSockets;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Client : MonoBehaviour
{
    [SerializeField, Tooltip("Connect on Start with default values.")]
    private bool _connectOnStart = false;

    private Socket _clientSocket;
    private Address _serverAddress;
    private string _ipAddress = "127.0.0.1";
    private int _port = 22044;

    private readonly int _intervalHeartbeat = 5000; // ms

    private readonly byte[] _buffer = new byte[1024];
    private GameObject _primitive;

    private void Start()
    {
        if (_connectOnStart)
        {
            ConnectToServer();
        }
    }

    private void OnDestroy()
    {
        UDP.Destroy(ref _clientSocket);
        UDP.Deinitialize();
    }

    public void ConnectToServer()
    {
        UnityMainThreadDispatcher.Instance();
        UDP.Initialize();

        _clientSocket = UDP.Create(256 * 1024, 256 * 1024);
        _serverAddress = new Address
        {
            port = (ushort)_port
        };

        if (UDP.SetIP(ref _serverAddress, _ipAddress) == Status.OK
            && UDP.Connect(_clientSocket, ref _serverAddress) == 0)
        {
            Debug.Log("Connected to server.");
        }

        UDP.SetNonBlocking(_clientSocket, true);

        SendConnectMessage();

        // Start the asynchronous processes.
        Task.Run(() => ReceiveDataAsync());
        Task.Run(() => SendHeartbeatAsync());
    }

    private async Task ReceiveDataAsync()
    {
        while (true)
        {
            if (UDP.Poll(_clientSocket, 0) > 0)
            {
                int dataLength = 0;
                while ((dataLength = UDP.Receive(_clientSocket, IntPtr.Zero, _buffer, _buffer.Length)) > 0)
                {
                    string receivedMessage = Encoding.ASCII.GetString(_buffer, 0, dataLength);
                    Debug.Log($"Received from server: {receivedMessage}");

                    // Update the primitive position on the main thread.
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        UpdatePrimitivePosition(DeserializeVector3(receivedMessage))
                    );
                }
            }

            // Small delay to prevent tight looping.
            await Task.Delay(10);
        }
    }

    private async Task SendHeartbeatAsync()
    {
        while (true)
        {
            string heartbeatMessage = "heartbeat";
            byte[] messageData = Encoding.ASCII.GetBytes(heartbeatMessage);
            UDP.Send(_clientSocket, ref _serverAddress, messageData, messageData.Length);

            await Task.Delay(_intervalHeartbeat);
        }
    }

    private void SendConnectMessage()
    {
        string message = "connect";
        byte[] messageData = Encoding.ASCII.GetBytes(message);
        UDP.Send(_clientSocket, ref _serverAddress, messageData, messageData.Length);
    }

    private void UpdatePrimitivePosition(Vector3 position)
    {
        if (_primitive == null)
        {
            Debug.Log("Creating primitive network object.");
            _primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        Debug.Log($"Updating primitive object position: {position}");
        _primitive.transform.position = position;
    }

    private Vector3 DeserializeVector3(string data)
    {
        string[] parts = data.Split(',');
        return new Vector3(
            float.Parse(parts[0]),
            float.Parse(parts[1]),
            float.Parse(parts[2]));
    }

    public void SetIPAddress(string ip) => _ipAddress = ip;

    public void SetPort(string port) => _port = int.Parse(port);
}