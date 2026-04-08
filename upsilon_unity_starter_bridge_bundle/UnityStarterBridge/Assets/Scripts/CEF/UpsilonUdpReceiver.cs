using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Upsilon.CEF
{
    public class UpsilonUdpReceiver : MonoBehaviour
    {
        [SerializeField] private int port = 7777;
        [SerializeField] private bool verboseLogging;

        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;
        private readonly object _lock = new object();
        private readonly System.Collections.Generic.Queue<CEFPacket> _pending = new System.Collections.Generic.Queue<CEFPacket>();

        private void Start()
        {
            _client = new UdpClient(port);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
            Debug.Log($"[UpsilonUdpReceiver] Listening on UDP {port}");
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_pending.Count > 0)
                {
                    UpsilonStateBus.Push(_pending.Dequeue());
                }
            }
        }

        private void ReceiveLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, port);
            while (_running)
            {
                try
                {
                    var bytes = _client.Receive(ref endpoint);
                    var json = Encoding.UTF8.GetString(bytes);
                    var packet = JsonUtility.FromJson<CEFPacket>(json);
                    if (packet == null) continue;
                    lock (_lock)
                    {
                        _pending.Enqueue(packet);
                    }
                    if (verboseLogging)
                    {
                        Debug.Log($"[UpsilonUdpReceiver] {json}");
                    }
                }
                catch (SocketException)
                {
                    if (_running == false) break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UpsilonUdpReceiver] Receive error: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            _running = false;
            try { _client?.Close(); } catch { }
            try { _thread?.Join(100); } catch { }
        }
    }
}
