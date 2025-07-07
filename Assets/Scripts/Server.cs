using NanoSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class Server : MonoBehaviour
{
    private Socket _serverSocket;
    private Address _address;
    private readonly byte[] _buffer = new byte[1024];
    private const int _port = 22044;

    private const float _intervalUpdate = 2f;
    private readonly double _timeoutHeartbeat = 15D;

    private readonly Dictionary<Address, Vector3> _clientPositions = new();
    private readonly Dictionary<Address, DateTime> _lastReceivedHeartbeat = new();

    private void Start()
    {
        UDP.Initialize();

        _serverSocket = UDP.Create(256 * 1024, 256 * 1024);
        _address = new Address
        {
            port = _port
        };

        if (UDP.SetIP(ref _address, "::0") == Status.OK && UDP.Bind(_serverSocket, ref _address) == 0)
        {
            Debug.Log("Server started and bound to address.");
        }

        UDP.SetNonBlocking(_serverSocket, true);

        // Start the asynchronous processes
        Task.Run(() => HandleNetworkTrafficAsync());
        Task.Run(() => RemoveInactiveClientsAsync());

        // Do the periodic update of the client positions
        InvokeRepeating(nameof(SendPeriodicUpdates), 0, _intervalUpdate);
    }

    private void OnDestroy()
    {
        UDP.Destroy(ref _serverSocket);
        UDP.Deinitialize();
    }

    private async Task HandleNetworkTrafficAsync()
    {
        while (true)
        {
            if (UDP.Poll(_serverSocket, 0) > 0)
            {
                int dataLength = 0;
                while ((dataLength = UDP.Receive(_serverSocket, ref _address, _buffer, _buffer.Length)) > 0)
                {
                    string message = Encoding.ASCII.GetString(_buffer, 0, dataLength);
                    Debug.Log($"Received: {message}");

                    HandleClientRequest(_address, message);
                }
            }
            await Task.Delay(10);
        }
    }

    private void HandleClientRequest(Address clientAddress, string message)
    {
        if (message == "connect")
        {
            Debug.Log($"Client connected: {clientAddress}");

            Vector3 initialPosition = GetRandomStartPosition();
            _clientPositions.Add(clientAddress, initialPosition); // Add the client to the dictionary

            string response = SerializeVector3(initialPosition);
            byte[] responseData = Encoding.ASCII.GetBytes(response);
            UDP.Send(_serverSocket, ref clientAddress, responseData, responseData.Length);
        }
        if (message == "heartbeat")
        {
            // Update the last received time for the client
            _lastReceivedHeartbeat[clientAddress] = DateTime.UtcNow;
        }
    }

    private async Task RemoveInactiveClientsAsync()
    {
        while (true)
        {
            await Task.Delay((int)(_intervalUpdate * 1000)); // Convert seconds to milliseconds
            RemoveInactiveClients();
        }
    }

    private void SendPeriodicUpdates()
    {
        foreach (var clientEntry in _clientPositions.ToList())
        {
            Address clientAddress = clientEntry.Key; // Create a temporary copy
            Vector3 newPosition = GetNextPosition(clientEntry.Value);
            _clientPositions[clientAddress] = newPosition;

            string positionString = SerializeVector3(newPosition);
            byte[] positionData = Encoding.ASCII.GetBytes(positionString);
            UDP.Send(_serverSocket, ref clientAddress, positionData, positionData.Length);

            Debug.Log($"Sent new position {positionString} to client at {clientAddress}");
        }
    }

    private void RemoveInactiveClients()
    {
        var inactiveClients = _lastReceivedHeartbeat
            .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds > _timeoutHeartbeat)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var client in inactiveClients)
        {
            Debug.Log($"Removing inactive client: {client}");
            _clientPositions.Remove(client);
            _lastReceivedHeartbeat.Remove(client);
        }
    }

    private Vector3 GetRandomStartPosition()
    {
        return new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f));
    }

    private Vector3 GetNextPosition(Vector3 currentPosition)
    {
        Vector3 randomDirection = Random.insideUnitSphere;
        return currentPosition + randomDirection;
    }

    private string SerializeVector3(Vector3 position)
    {
        return $"{position.x},{position.y},{position.z}";
    }
}