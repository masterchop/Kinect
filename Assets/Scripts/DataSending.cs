/* DataSending
 * 
 * The DataSending script must live in an object (usually an empty object) in the scene. The Client IP address in 
 *  the Unity editor must be filled out as the IP address of the current device.
 * This script manages the sending of data to the other devices. If you need to send data to the other devices, use
 *  the cooresponding method in Global. For example, if you need to send a newly spawned object, call the Global method
 *  Global.SendObject(newObj); This will call the needed methods between DataSending and Global for you.
 */

using System.Collections.Generic;
using System;
using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;

#if !UNITY_EDITOR
using Windows.Networking;
#endif

public class DataSending : MonoBehaviour
{
    // Client IP and Connection. 
    // Note 1: This client information is the Hololens connection information
    // Note 2: This information appears hardcoded here, but the unity editor will overwrite these values.
    public string ClientIP;
    public int ConnectionPort = 46000;

    // The socket and IP EndPoint connecting to the Hololens
    private Socket _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private IPEndPoint _clientEndpoint;

    // Continously reused GameObject
    private GameObject reusedObj;
    private int reusedInt;

    // Update is called once per frame.
    void Update()
    {

        #region Keyboard Commands
        if (Input.GetKeyDown("c")) // Create connection
        {
            attemptConnection();
        }

        if(Input.GetKeyDown("g")) // Send a string message as a test
        {
            if (Global.clientStatus == Global.netStatus.Connected)
            {
                String message = "GREETINGS FROM THE COMPUTER";

                // Create a buffer
                List<byte> messageBuffer = new List<byte>();
                // Add our flag
                messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.STRING_FLAG));
                // Add our message
                messageBuffer.AddRange(Encoding.ASCII.GetBytes(message));
                // Add our size
                int size = messageBuffer.Count + 4;
                messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

                //the byte[] to send
                byte[] theDataPackage = messageBuffer.ToArray();

                _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
            }
        }
        if (Input.GetKeyDown("h")) // Another string message, as a test
        {
            if (Global.clientStatus == Global.netStatus.Connected)
            {
                String message = "Hello";

                // Create a buffer
                List<byte> messageBuffer = new List<byte>();
                // Add our flag
                messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.STRING_FLAG));
                // Add our message
                messageBuffer.AddRange(Encoding.ASCII.GetBytes(message));
                // Add our size
                int size = messageBuffer.Count + 4;
                messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

                //the byte[] to send
                byte[] theDataPackage = messageBuffer.ToArray();

                _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
            }
        }
        if (Input.GetKeyDown("f")) // A string message stress test
        {
            if (Global.clientStatus == Global.netStatus.Connected)
            {
                String message = "This is the really long message that I intended to send you to make sure that your text box can fit this many characters and also to make you try and read this out loud in one really long breath.";

                // Create a buffer
                List<byte> messageBuffer = new List<byte>();
                // Add our flag
                messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.STRING_FLAG));
                // Add our message
                messageBuffer.AddRange(Encoding.ASCII.GetBytes(message));
                // Add our size
                int size = messageBuffer.Count + 4;
                messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

                //the byte[] to send
                byte[] theDataPackage = messageBuffer.ToArray();

                _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
            }
        }
        if (Input.GetKeyDown("1")) // Send a cube willy-nilly
        {
            if (Global.clientStatus == Global.netStatus.Connected)
            {
                GameObject newCube = GameObject.Instantiate(Resources.Load("Prefabs/Cube")) as GameObject;
                SendObject(newCube);
            }
        }
        else if (Input.GetKeyDown("2")) // Send a sphere willy-nilly
        {
            if (Global.clientStatus == Global.netStatus.Connected)
            {
                GameObject newSphere = GameObject.Instantiate(Resources.Load("Prefabs/Sphere")) as GameObject;
                SendObject(newSphere);
            }
        }
#endregion

        // Do this once per update, in case of overload
        if (Global.clientStatus == Global.netStatus.Connected)
        {
            // These are not nested because the below get methods remove an object
            //  from their respective lists in Global-- We don't want to remove
            //  items from the list if the client is not connected
            if (Global.GetSpawnedObject(out reusedObj))
            {
                SendObject(reusedObj);
            }

            if(Global.GetMovedObject(out reusedObj))
            {
                MoveObject(reusedObj);
            }

            if(Global.GetNewBody(out reusedObj))
            {
                if (reusedObj != null)
                {
                    SendNewBody(reusedObj);
                }
            }

            if (Global.GetMovedBody(out reusedObj))
            {
                SendBody(reusedObj);
            }

            reusedInt = Global.GetDeletedObj();
            if(reusedInt > 0)
            {
                DeleteObject(reusedInt);
            }
        }
        else if(Global.clientStatus == Global.netStatus.Ready)
        {
            // If the client is connected to us on DataReceiving, attempt to make 
            //  a connection going the other way. This is necessary for Hololens 
            //  asych silliness.
            Global.clientStatus = Global.netStatus.Attempting;
            attemptConnection();
        }
    }

    // Creates a unique ID, adds that to the objects Dictionary in Global, and sends the object over the network
    // This method is called automatically from the Update loop for any object added to Global.newSpawns. You do 
    //  NOT need to call this method yourself. It also creates a unique ID for that object in this method.
    private void SendObject(GameObject obj)
    {
        // Generate and save a unique Object ID
        int objId = Global.AddObject(obj);

        // Create a buffer
        List<byte> messageBuffer = new List<byte>();
        // Add our flag
        messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.OBJECT_CREATE_FLAG));
        // Add our Position
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.z));
        // Add our Rotation
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.z));
        // Get our ObjectType
        Global.ObjType objT = Global.ToEnum<Global.ObjType>(obj.name.Substring(0, obj.name.Length-7)); // Name - "(Clone)"
        // Add our Object Type
        messageBuffer.AddRange(BitConverter.GetBytes((int)objT));
        // Add our Object ID
        messageBuffer.AddRange(BitConverter.GetBytes(objId));
        // Add our size
        int size = messageBuffer.Count + 4;
        messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

        //the byte[] to send
        byte[] theDataPackage = messageBuffer.ToArray();

        // Set object name to be ID
        obj.name = objId.ToString();

        _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
    }

    // Sends new object coords over the network
    // This method is called automatically from the Update loop for any object added to Global.moveObjs. You do 
    //  NOT need to call this method yourself.
    private void MoveObject(GameObject obj)
    {
        // Find our object ID
        int objId = Int32.Parse(obj.name);

        // Create a buffer
        List<byte> messageBuffer = new List<byte>();
        // Add our flag
        messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.OBJECT_MOVE_FLAG));
        // Add our Position
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.z));
        // Add our Rotation
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.z));
        // Add our Object ID
        messageBuffer.AddRange(BitConverter.GetBytes(objId));
        // Add our size
        int size = messageBuffer.Count + 4;
        messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

        //the byte[] to send
        byte[] theDataPackage = messageBuffer.ToArray();

        _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
    }

    // This method is called automatically from the Update loop for any object added to Global.moveObjs. You do 
    //  NOT need to call this method yourself.
    private void DeleteObject(int objId)
    {
        // Create a buffer
        List<byte> messageBuffer = new List<byte>();
        // Add our flag
        messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.DELETE_FLAG));
        // Add our Object ID
        messageBuffer.AddRange(BitConverter.GetBytes(objId));
        // Add our size
        int size = messageBuffer.Count + 4;
        messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

        //the byte[] to send
        byte[] theDataPackage = messageBuffer.ToArray();

        _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);

        Debug.Log("Sent deleted object: " + objId);
    }

    private void SendNewBody(GameObject obj)
    {
        // Find our object ID
        int objId = Int32.Parse(obj.name.Substring(obj.name.Length - 5, 5));

        // Create a buffer
        List<byte> messageBuffer = new List<byte>();
        // Add our flag
        messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.AVATAR_CREATION_FLAG));
        // Add our Position
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.position.z));
        // Add our Rotation
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.x));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.y));
        messageBuffer.AddRange(BitConverter.GetBytes(obj.transform.rotation.eulerAngles.z));
        // Add our Object ID
        messageBuffer.AddRange(BitConverter.GetBytes(objId));
        // Add our size
        int size = messageBuffer.Count + 4;
        messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

        //the byte[] to send
        byte[] theDataPackage = messageBuffer.ToArray();

        _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
    }

    private void SendBody(GameObject obj)
    {
        // Find our object ID
        int objId = Int32.Parse(obj.name.Substring(obj.name.Length - 5, 5));

        GameObject child = obj.transform.FindChild("SpineBase").gameObject;

        // Create a buffer
        List<byte> messageBuffer = new List<byte>();
        // Add our flag
        messageBuffer.AddRange(BitConverter.GetBytes((int)Global.NetFlag.OBJECT_MOVE_FLAG));
        // Add our Position
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.position.x));
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.position.y));
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.position.z));
        // Add our Rotation
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.rotation.eulerAngles.x));
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.rotation.eulerAngles.y));
        messageBuffer.AddRange(BitConverter.GetBytes(child.transform.rotation.eulerAngles.z));
        // Add our Object ID
        messageBuffer.AddRange(BitConverter.GetBytes(objId));
        // Add our size
        int size = messageBuffer.Count + 4;
        messageBuffer.InsertRange(0, BitConverter.GetBytes(size));

        //the byte[] to send
        byte[] theDataPackage = messageBuffer.ToArray();

        _clientSocket.BeginSend(theDataPackage, 0, theDataPackage.Length, SocketFlags.None, new AsyncCallback(SendCallback), _clientSocket);
    }

    // Method to attempt a connection with the Hololens for sending data. At this point, we are
    //  already connected to the Hololens for receiving data. On failure, this method will set the
    //  ClientStatus to Error, and an error will be printed in console, but nothing further will be
    //  done.
    private void attemptConnection()
    {
        try
        {
            // Setup the network sender.
            Debug.Log("Attempting Send Connection..");
            IPAddress localAddr = IPAddress.Parse(ClientIP.Trim());
            _clientEndpoint = new IPEndPoint(localAddr, ConnectionPort);
            _clientSocket.Connect(_clientEndpoint);
            Global.clientStatus = Global.netStatus.Connected;
            Debug.Log("Send Connection Established");
        }
        catch (SocketException ex)
        {
            Debug.Log("Send Connection FAILED");
            Debug.Log(ex.Message);
            Global.clientStatus = Global.netStatus.Error;
        }
    }

    // This callback occurs every time a packet is sent to the hololens.
    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = client.EndSend(ar);
            //Debug.Log("Sent bytes to server: " +  bytesSent + " bytes");

            // Signal that all bytes have been sent.
            //sendDone.Set();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}