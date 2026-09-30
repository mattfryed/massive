using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

// Created only by an opted-in Windows player. No scene/prefab wiring or Editor work.
public sealed class CabinetHeartbeat : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private UdpClient sender;
    private readonly byte[] packet = new byte[24];
    private long sequence;
    private double nextSend;
    private bool sendWarning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        string portText = Environment.GetEnvironmentVariable("MASSIVE_WATCHDOG_PORT");
        string tokenText = Environment.GetEnvironmentVariable("MASSIVE_WATCHDOG_TOKEN");
        if (string.IsNullOrEmpty(portText) && string.IsNullOrEmpty(tokenText)) return;
        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535 ||
            !Guid.TryParseExact(tokenText, "N", out Guid token))
        {
            Debug.LogWarning("[CabinetHeartbeat] Invalid watchdog launch environment.");
            return;
        }

        var host = new GameObject("Cabinet Heartbeat");
        DontDestroyOnLoad(host);
        var heartbeat = host.AddComponent<CabinetHeartbeat>();
        try
        {
            Array.Copy(token.ToByteArray(), heartbeat.packet, 16);
            heartbeat.sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            heartbeat.sender.Connect(IPAddress.Loopback, port);
            heartbeat.sender.Client.Blocking = false;
            Debug.Log("[CabinetHeartbeat] Main-thread heartbeat enabled (protocol 1).");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[CabinetHeartbeat] Could not initialize: " + exception.Message);
            Destroy(host);
        }
    }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (sender == null || now < nextSend) return;
        nextSend = now + 1.0;
        ++sequence;
        for (int i = 0; i < 8; ++i)
            packet[16 + i] = (byte)((ulong)sequence >> (8 * i));
        try
        {
            // Nonblocking, localhost only; deliberately on the Unity main thread.
            sender.Send(packet, packet.Length);
            sendWarning = false;
        }
        catch (SocketException exception)
        {
            if (!sendWarning)
                Debug.LogWarning("[CabinetHeartbeat] Send failed: " + exception.SocketErrorCode);
            sendWarning = true;
        }
    }

    private void OnDestroy()
    {
        sender?.Close();
        sender = null;
    }
#endif
}
