using System;
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;

public class EOSOnlineTransport : MonoBehaviour
{
    public static EOSOnlineTransport Instance;

    [SerializeField]
    private string socketName = "PvZEcoPavo";

    private P2PInterface p2pInterface;

    private ulong connectionRequestNotificationId;
    private ulong connectionEstablishedNotificationId;
    private ulong connectionInterruptedNotificationId;
    private ulong connectionClosedNotificationId;

    public bool IsInitialized { get; private set; }
    public bool IsHost { get; private set; }
    public bool IsClient { get; private set; }
    public bool IsConnected { get; private set; }

    public ProductUserId LocalUserId
    {
        get
        {
            if (EOSAutoLogin.Instance == null)
            {
                return null;
            }

            return EOSAutoLogin.Instance.LocalProductUserId;
        }
    }

    public string SocketName => socketName;

    public event Action<ProductUserId> OnPeerConnected;
    public event Action<ProductUserId> OnPeerDisconnected;
    public event Action<ProductUserId, byte[]> OnPacketReceived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

        ReceivePackets();
    }

    public bool Initialize()
    {
        if (IsInitialized)
        {
            return true;
        }

        if (EOSManager.Instance == null)
        {
            Debug.LogError(
                "[EOS P2P] EOSManager no existe."
            );

            return false;
        }

        if (EOSAutoLogin.Instance == null ||
            !EOSAutoLogin.Instance.IsLoggedIn)
        {
            return false;
        }

        if (LocalUserId == null ||
            !LocalUserId.IsValid())
        {
            Debug.LogError(
                "[EOS P2P] ProductUserId local inválido."
            );

            return false;
        }

        p2pInterface =
            EOSManager.Instance.GetEOSP2PInterface();

        if (p2pInterface == null)
        {
            Debug.LogError(
                "[EOS P2P] P2PInterface no disponible."
            );

            return false;
        }

        RegisterNotifications();

        IsInitialized = true;

        Debug.Log(
            "[EOS P2P] Transporte inicializado."
        );

        Debug.Log(
            "[EOS P2P] ProductUserId local: " +
            LocalUserId
        );

        return true;
    }

    private void RegisterNotifications()
    {
        RegisterConnectionRequest();
        RegisterConnectionEstablished();
        RegisterConnectionInterrupted();
        RegisterConnectionClosed();
    }

    private void RegisterConnectionRequest()
    {
        if (connectionRequestNotificationId != 0)
        {
            return;
        }

        AddNotifyPeerConnectionRequestOptions options =
            new AddNotifyPeerConnectionRequestOptions
            {
                LocalUserId = LocalUserId,
                SocketId = new SocketId
                {
                    SocketName = socketName
                }
            };

        connectionRequestNotificationId =
            p2pInterface.AddNotifyPeerConnectionRequest(
                ref options,
                null,
                OnIncomingConnectionRequest
            );
    }

    private void RegisterConnectionEstablished()
    {
        if (connectionEstablishedNotificationId != 0)
        {
            return;
        }

        AddNotifyPeerConnectionEstablishedOptions options =
            new AddNotifyPeerConnectionEstablishedOptions
            {
                LocalUserId = LocalUserId,
                SocketId = new SocketId
                {
                    SocketName = socketName
                }
            };

        connectionEstablishedNotificationId =
            p2pInterface.AddNotifyPeerConnectionEstablished(
                ref options,
                null,
                OnPeerConnectionEstablished
            );
    }

    private void RegisterConnectionInterrupted()
    {
        if (connectionInterruptedNotificationId != 0)
        {
            return;
        }

        AddNotifyPeerConnectionInterruptedOptions options =
            new AddNotifyPeerConnectionInterruptedOptions
            {
                LocalUserId = LocalUserId,
                SocketId = new SocketId
                {
                    SocketName = socketName
                }
            };

        connectionInterruptedNotificationId =
            p2pInterface.AddNotifyPeerConnectionInterrupted(
                ref options,
                null,
                OnPeerConnectionInterrupted
            );
    }

    private void RegisterConnectionClosed()
    {
        if (connectionClosedNotificationId != 0)
        {
            return;
        }

        AddNotifyPeerConnectionClosedOptions options =
            new AddNotifyPeerConnectionClosedOptions
            {
                LocalUserId = LocalUserId,
                SocketId = new SocketId
                {
                    SocketName = socketName
                }
            };

        connectionClosedNotificationId =
            p2pInterface.AddNotifyPeerConnectionClosed(
                ref options,
                null,
                OnRemoteConnectionClosed
            );
    }

    public bool StartHost()
    {
        if (!Initialize())
        {
            return false;
        }

        IsHost = true;
        IsClient = false;
        IsConnected = false;

        Debug.Log(
            "[EOS P2P] HOST ONLINE."
        );

        Debug.Log(
            "[EOS P2P] Esperando conexiones..."
        );

        return true;
    }

    public bool StartClient(
        ProductUserId hostUserId)
    {
        if (!Initialize())
        {
            return false;
        }

        if (hostUserId == null ||
            !hostUserId.IsValid())
        {
            Debug.LogError(
                "[EOS P2P] ProductUserId del host inválido."
            );

            return false;
        }

        if (hostUserId == LocalUserId)
        {
            Debug.LogError(
                "[EOS P2P] El host y el cliente son el mismo usuario."
            );

            return false;
        }

        IsHost = false;
        IsClient = true;
        IsConnected = false;

        Debug.Log(
            "[EOS P2P] CLIENTE ONLINE."
        );

        Debug.Log(
            "[EOS P2P] Conectando con host: " +
            hostUserId
        );

        return RequestConnection(
            hostUserId
        );
    }

    private bool RequestConnection(
        ProductUserId remoteUserId)
    {
        if (!IsInitialized)
        {
            return false;
        }

        SocketId socketId =
            new SocketId
            {
                SocketName = socketName
            };

        byte[] connectionData =
            System.Text.Encoding.ASCII.GetBytes(
                "EOS_CONNECT"
            );

        SendPacketOptions options =
            new SendPacketOptions
            {
                LocalUserId = LocalUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId,
                Channel = 0,
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableOrdered,
                Data = new ArraySegment<byte>(
                    connectionData
                )
            };

        Result result =
            p2pInterface.SendPacket(
                ref options
            );

        if (result != Result.Success)
        {
            Debug.LogError(
                "[EOS P2P] SendPacket conexión: " +
                result
            );

            return false;
        }

        Debug.Log(
            "[EOS P2P] Solicitud de conexión enviada."
        );

        return true;
    }

    private void OnIncomingConnectionRequest(
        ref OnIncomingConnectionRequestInfo data)
    {
        if (data.RemoteUserId == null ||
            !data.RemoteUserId.IsValid())
        {
            return;
        }

        if (!data.SocketId.HasValue)
        {
            return;
        }

        SocketId socketId =
            data.SocketId.Value;

        if (socketId.SocketName != socketName)
        {
            return;
        }

        Debug.Log(
            "[EOS P2P] Solicitud de conexión recibida de: " +
            data.RemoteUserId
        );

        AcceptConnection(
            data.RemoteUserId,
            socketId
        );
    }

    private void AcceptConnection(
        ProductUserId remoteUserId,
        SocketId socketId)
    {
        AcceptConnectionOptions options =
            new AcceptConnectionOptions
            {
                LocalUserId = LocalUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId
            };

        Result result =
            p2pInterface.AcceptConnection(
                ref options
            );

        if (result != Result.Success &&
            result != Result.AlreadyConfigured)
        {
            Debug.LogError(
                "[EOS P2P] AcceptConnection: " +
                result
            );

            return;
        }

        Debug.Log(
            "[EOS P2P] Conexión aceptada: " +
            remoteUserId
        );
    }

    private void OnPeerConnectionEstablished(
        ref OnPeerConnectionEstablishedInfo data)
    {
        if (data.RemoteUserId == null ||
            !data.RemoteUserId.IsValid())
        {
            return;
        }

        if (!data.SocketId.HasValue)
        {
            return;
        }

        SocketId socketId =
            data.SocketId.Value;

        if (socketId.SocketName != socketName)
        {
            return;
        }

        IsConnected = true;

        Debug.Log(
            "[EOS P2P] CONEXIÓN ESTABLECIDA: " +
            data.RemoteUserId
        );

        OnPeerConnected?.Invoke(
            data.RemoteUserId
        );
    }

    private void OnPeerConnectionInterrupted(
        ref OnPeerConnectionInterruptedInfo data)
    {
        if (data.RemoteUserId == null ||
            !data.RemoteUserId.IsValid())
        {
            return;
        }

        if (!data.SocketId.HasValue)
        {
            return;
        }

        SocketId socketId =
            data.SocketId.Value;

        if (socketId.SocketName != socketName)
        {
            return;
        }

        IsConnected = false;

        Debug.LogWarning(
            "[EOS P2P] CONEXIÓN INTERRUMPIDA: " +
            data.RemoteUserId
        );
    }

    private void OnRemoteConnectionClosed(
        ref OnRemoteConnectionClosedInfo data)
    {
        if (data.RemoteUserId == null ||
            !data.RemoteUserId.IsValid())
        {
            return;
        }

        if (!data.SocketId.HasValue)
        {
            return;
        }

        SocketId socketId =
            data.SocketId.Value;

        if (socketId.SocketName != socketName)
        {
            return;
        }

        IsConnected = false;

        Debug.Log(
            "[EOS P2P] CONEXIÓN CERRADA: " +
            data.RemoteUserId
        );

        OnPeerDisconnected?.Invoke(
            data.RemoteUserId
        );
    }

    private void ReceivePackets()
    {
        if (p2pInterface == null ||
            LocalUserId == null ||
            !LocalUserId.IsValid())
        {
            return;
        }

        while (true)
        {
            GetNextReceivedPacketSizeOptions sizeOptions =
                new GetNextReceivedPacketSizeOptions
                {
                    LocalUserId = LocalUserId
                };

            uint packetSize;

            Result sizeResult =
                p2pInterface.GetNextReceivedPacketSize(
                    ref sizeOptions,
                    out packetSize
                );

            if (sizeResult != Result.Success)
            {
                break;
            }

            if (packetSize == 0)
            {
                break;
            }

            byte[] data =
                new byte[packetSize];

            ArraySegment<byte> dataSegment =
                new ArraySegment<byte>(
                    data
                );

            ReceivePacketOptions receiveOptions =
                new ReceivePacketOptions
                {
                    LocalUserId = LocalUserId,
                    MaxDataSizeBytes = packetSize
                };

            ProductUserId peerId =
                null;

            SocketId socketId =
                default;

            byte channel;

            uint bytesWritten;

            Result receiveResult =
                p2pInterface.ReceivePacket(
                    ref receiveOptions,
                    ref peerId,
                    ref socketId,
                    out channel,
                    dataSegment,
                    out bytesWritten
                );

            if (receiveResult != Result.Success)
            {
                Debug.LogError(
                    "[EOS P2P] ReceivePacket: " +
                    receiveResult
                );

                break;
            }

            if (peerId == null ||
                !peerId.IsValid())
            {
                continue;
            }

            if (socketId.SocketName != socketName)
            {
                continue;
            }

            if (bytesWritten == 0)
            {
                continue;
            }

            byte[] finalData =
                new byte[bytesWritten];

            Buffer.BlockCopy(
                data,
                0,
                finalData,
                0,
                (int)bytesWritten
            );

            OnPacketReceived?.Invoke(
                peerId,
                finalData
            );
        }
    }

    public void Send(
        ProductUserId remoteUserId,
        byte[] packet)
    {
        if (!IsInitialized ||
            p2pInterface == null)
        {
            return;
        }

        if (remoteUserId == null ||
            !remoteUserId.IsValid())
        {
            Debug.LogError(
                "[EOS P2P] Usuario remoto inválido."
            );

            return;
        }

        if (packet == null ||
            packet.Length == 0)
        {
            return;
        }

        SocketId socketId =
            new SocketId
            {
                SocketName = socketName
            };

        SendPacketOptions options =
            new SendPacketOptions
            {
                LocalUserId = LocalUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId,
                Channel = 0,
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableOrdered,
                Data = new ArraySegment<byte>(
                    packet
                )
            };

        Result result =
            p2pInterface.SendPacket(
                ref options
            );

        if (result != Result.Success)
        {
            Debug.LogError(
                "[EOS P2P] SendPacket: " +
                result
            );
        }
    }

    public void SendToHost(
        ProductUserId hostUserId,
        byte[] packet)
    {
        Send(
            hostUserId,
            packet
        );
    }

    public void DisconnectFrom(
        ProductUserId remoteUserId)
    {
        if (!IsInitialized ||
            p2pInterface == null ||
            remoteUserId == null ||
            !remoteUserId.IsValid())
        {
            return;
        }

        SocketId socketId =
            new SocketId
            {
                SocketName = socketName
            };

        CloseConnectionOptions options =
            new CloseConnectionOptions
            {
                LocalUserId = LocalUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId
            };

        Result result =
            p2pInterface.CloseConnection(
                ref options
            );

        if (result != Result.Success &&
            result != Result.NotFound)
        {
            Debug.LogWarning(
                "[EOS P2P] CloseConnection: " +
                result
            );
        }
    }

    public void Disconnect()
    {
        if (!IsInitialized ||
            p2pInterface == null)
        {
            return;
        }

        IsConnected = false;
        IsHost = false;
        IsClient = false;

        Debug.Log(
            "[EOS P2P] Transporte desconectado."
        );
    }

    private void RemoveNotifications()
    {
        if (p2pInterface == null)
        {
            return;
        }

        if (connectionRequestNotificationId != 0)
        {
            p2pInterface.RemoveNotifyPeerConnectionRequest(
                connectionRequestNotificationId
            );

            connectionRequestNotificationId = 0;
        }

        if (connectionEstablishedNotificationId != 0)
        {
            p2pInterface.RemoveNotifyPeerConnectionEstablished(
                connectionEstablishedNotificationId
            );

            connectionEstablishedNotificationId = 0;
        }

        if (connectionInterruptedNotificationId != 0)
        {
            p2pInterface.RemoveNotifyPeerConnectionInterrupted(
                connectionInterruptedNotificationId
            );

            connectionInterruptedNotificationId = 0;
        }

        if (connectionClosedNotificationId != 0)
        {
            p2pInterface.RemoveNotifyPeerConnectionClosed(
                connectionClosedNotificationId
            );

            connectionClosedNotificationId = 0;
        }
    }

    private void OnDestroy()
    {
        RemoveNotifications();

        p2pInterface = null;
        IsInitialized = false;
    }
}