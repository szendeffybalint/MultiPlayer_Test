using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetwork : NetworkBehaviour
{
    //type of object to spawn
    [SerializeField] private Transform spawnedObjectPrefab;
    //Spawned objects list
    private List<Transform> spawneds = new List<Transform>();

    // To store and use "global" values,
    // write / read permissions
    //Basic  synchronize 
    // https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/serialization/inetworkserializable/index.html
    private NetworkVariable<customData> randomNumber = new NetworkVariable<customData>(new customData("ONLY VALUE TYPES", true, 1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    //Old Movespeed (using if no ThirdPersonController
    [SerializeField] private float moveSpeed = 3f;

    #region Struct_test - customData
    public struct customData : INetworkSerializeByMemcpy
    {

        //it s always allocate 128 bytes in the memory so be carefull with strings
        public FixedString128Bytes _string;
        public bool _bool;
        public int _int;


        //Test it,s not necessary
        //Older versions used it 
        //public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        //{
        //    serializer.SerializeValue(ref _string);
        //    serializer.SerializeValue(ref _bool);
        //    serializer.SerializeValue(ref _int);
        //}

        public customData(string str = "CAN'T PASS NONE VALUE TYPE", bool boolean = false, int integer = 1)
        {
            _string = str;
            _bool = boolean;
            _int = integer;
        }
    };
    #endregion

    //Void on init --> can't use methods in awake that effect variables, SO HAVE TO USE IT
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        //Have to enable Player input from here otherwise not using the correct controls at client app
        // https://stackoverflow.com/questions/74767055/network-client-use-wrong-input-devices-when-using-unity-netcode-with-starter-ass
        PlayerInput player = GetComponent<PlayerInput>();
        //Reinit PlayerInput, otherwise no input system
        if (IsOwner)
        {
            player = GetComponent<PlayerInput>();
            player.enabled = false;
            player.enabled = true;
        }

        //Test Onvalue change event "init"
        randomNumber.OnValueChanged += (customData previousValue, customData newCalue) =>
        {
            Debug.Log("test_ID " + OwnerClientId + "  randomnumber.str: " + randomNumber.Value._string);
            Debug.Log("test_ID " + OwnerClientId + "  randomnumber.bool: " + randomNumber.Value._bool);
            Debug.Log("test_ID " + OwnerClientId + "  randomnumber.int: " + randomNumber.Value._int);
        };
    }

    #region ObjectSpawn
    [ServerRpc]
    private void Spawn_ServerRpc()
    {
        //It s only run on host, NOT ON CLIENT
        //BUT CAN SEND STRINGS
        Transform spawned = Instantiate(spawnedObjectPrefab);
        spawneds.Add(spawned);
        spawned.GetComponent<NetworkObject>().Spawn(true);

    }
    [ServerRpc]
    private void Destroy_ServerRpc()
    {
        if (spawneds.Count == 0) return;
        Transform spawned = spawneds[spawneds.Count - 1];
        spawneds.Remove(spawned);
        Destroy(spawned.gameObject);
    }
    #endregion

    //Server RPC synchronize
    //Have to end "ServerRpc"
    // https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/message-system/serverrpc
    [ServerRpc]
    private void Test_ServerRpc(string msg, ServerRpcParams rpcParams)
    {
        //It s only run on host, NOT ON CLIENT
        //BUT CAN SEND STRINGS
        Debug.Log("test_ServerRpc " + OwnerClientId + " sent msg: " + msg + " , " + rpcParams.Receive.SenderClientId);
    }


    //Client RPC synchronize   CLIENT CAN'T CALL OT, ONLY THE HOST CAN
    //Have to end ClientRpc 
    // https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/message-system/clientrpc
    [ClientRpc]
    private void Test_ClientRpc(string msg, ClientRpcParams rpcParams)
    {
        //It s run for client BUT ONLY HOST CAN CALL
        //CAN SEND STRINGS
        Debug.Log("test_ServerRpc " + OwnerClientId + " sent msg: " + msg + " , TargetID=" + rpcParams.Send.TargetClientIds[0]);
    }


    void Update()
    {
        //Return if not the correct client/ host
        if (!IsOwner) return;

        //with keycode inputs
        TestMethods();

        //WASD Movement if no Third Person Controller
        BasicMovements();

    }

    private void BasicMovements()
    {
        //Base "Third Person Controller"
        if (GetComponent<StarterAssets.ThirdPersonController>() == null)
        {
            Vector3 moveDir = new Vector3(0, 0, 0);

            if (Input.GetKey(KeyCode.W)) moveDir.z = 1f;
            if (Input.GetKey(KeyCode.S)) moveDir.z = -1f;
            if (Input.GetKey(KeyCode.A)) moveDir.x = -1f;
            if (Input.GetKey(KeyCode.D)) moveDir.x = 1f;

            transform.position += moveDir * moveSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Basic usage of serverRpc and sending values between server and clint
    /// <br> Space == Struct change </br>
    /// <br> LShift == ServerRpc command (only on HOST) </br>
    /// <br> Tab == Spawn Object (ServerRpc) </br>
    /// <br> CapsLock == Destroy Object (ServerRpc) </br>
    /// </summary>
    private void TestMethods()
    {
        //Test value change
        if (Input.GetKeyDown(KeyCode.Space))        
            randomNumber.Value = new customData(
                Random.Range(0, 100) > 50 ? "Test_ number is big" : "Test_ number is small",
                Random.Range(0, 100) > 50,
                Random.Range(0, 50)
                );
        

        //Test ServerRpc
        if (Input.GetKeyDown(KeyCode.LeftShift))        
            Test_ServerRpc("BASIC MESSAGE", new ServerRpcParams());
        

        //Test ClientRpc
        if (Input.GetKeyDown(KeyCode.LeftControl))        
            //Send basic message but only for clientID 1
            Test_ClientRpc("BASIC MESSAGE", new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { 1 } } });
        

        //Test Spawn
        if (Input.GetKeyDown(KeyCode.Tab))        
            //Only HOST CAN CALL --> Have to use SERVER RPC
            Spawn_ServerRpc();
        

        //Test DeSpawnW
        if (Input.GetKeyDown(KeyCode.CapsLock))
            //bool at networkObject to destroy when owner left or not
            Destroy_ServerRpc();
        
    }
}

