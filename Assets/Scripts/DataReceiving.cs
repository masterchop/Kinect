/* DataReceiving
 * 
 * The DataRecieving script must live in an object (usually an empty object) in the scene. The Server IP address in 
 *  the Unity editor must be filled out as the IP address of the current device. THIS device is the Server, so put
 *  in THIS device's IP address. 
 * This script handles connecting to devices, and recieving data from them in the form of packages. The packages
 *  are deconstructed and acted on in this script. These include the initial mesh push from the hololens, creation
 *  of new objects, amoung others. 
 */

using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using System.Text;
using HoloToolkit.Unity;

#if UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
#endif

public class DataReceiving : SpatialMappingSource
{
    // Server IP and Connection. 
    // Note 1: This file is being ran FROM the server (with the Kinect) attatched. Therefore, this information
    //  is the connection information of the current computer. E.g. This is YOUR IP address.
    // Note 2: This information appears hardcoded here, but the unity editor will overwrite these values.
    public string ServerIP;
    public int ConnectionPort = 45000;

    // Listens for network connections over TCP.
    private TcpListener networkListener;

    // Keeps client information when a connection happens.
    private TcpClient networkClient;

    // Tracks if a client is connected.
    private bool ClientConnected = false;

    // List of all meshes. This is NOT a list of the objects in the scene. This is
    //  a list of the meshes sent from the Hololens. The purpose of this list is for
    //  saving the meshes recieved from the hololens, and loading them again.
    List<Mesh> globalMeshes;

    // Use this for initialization.
    void Start()
    {
        // Setup the network listener.
        IPAddress localAddr = IPAddress.Parse(ServerIP.Trim());
        networkListener = new TcpListener(localAddr, ConnectionPort);
        networkListener.Start();

        //print out the IP adress and port number to see on the debug console
        UnityEngine.Debug.Log(networkListener.LocalEndpoint.ToString());

        // Request the network listener to wait for connections asynchronously.
        AsyncCallback callback = new AsyncCallback(OnClientConnect);
        networkListener.BeginAcceptTcpClient(callback, this);

        globalMeshes = new List<Mesh>();
    }

    // Update is called once per frame.
    void Update()
    {
        // If we have a connected client, presumably the client wants to send some meshes.
        if (ClientConnected)
        {
            if (Global.clientStatus == Global.netStatus.NotConnected)
            {
                Global.clientStatus = Global.netStatus.Ready;
            }

            // Get the clients stream.
            NetworkStream stream = networkClient.GetStream();

            // Make sure there is data in the stream.
            if (stream.DataAvailable)
            {
                // The first 4 bytes will be the size of the data containing the mesh(es).
                int datasize = ReadInt(stream);

                // Allocate a buffer to hold the data.  
                byte[] dataBuffer = new byte[datasize];

                // Read the data.
                // The data can come in chunks. 
                int readsize = 0;

                while (readsize != datasize)
                {
                    readsize += stream.Read(dataBuffer, readsize, datasize - readsize);
                }

                if (readsize != datasize)
                {
                    Debug.Log("reading data failed: " + readsize + " != " + datasize);
                }

                //print out the size of the packet for debugging purposes
                //UnityEngine.Debug.Log("size of packet: " + dataBuffer.Length);


                // DONE READING IN THE DATA AT THIS POINT -- 
                // NOW DO STUFF WITH THE DATA...
                //
                //dataBuffer variable holds the data to decode from byte[] 
                interpretIncomingPackage(dataBuffer,datasize);

                // Finally disconnect.
                ClientConnected = false;
                networkClient.Close();

                // And wait for the next connection asynchronously.
                AsyncCallback callback = new AsyncCallback(OnClientConnect);
                networkListener.BeginAcceptTcpClient(callback, this);
            }
        }

        if (Input.GetKeyDown("s")) // Save
        {
            MeshSaver.Save("MeshSave", globalMeshes);
        }
        else if(Input.GetKeyDown("l")) // Load
        {
            globalMeshes = (List<Mesh>)MeshSaver.Load("MeshSave");
            for (int index = 0; index < globalMeshes.Count; index++)
            {
                GameObject surface = AddSurfaceObject(globalMeshes[index], string.Format("Beamed-{0}", surfaceObjects.Count), transform);
                surface.transform.parent = SpatialMappingManager.Instance.transform;
                surface.GetComponent<MeshRenderer>().enabled = true;
                surface.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }

    // This is the meat of DataRecieving. Takes the full package and interprets
    //  the package by taking out the first 4 bytes which denotes the package flag.
    //  Types of package flags can be found in the Global.NetFlag enum. The method
    //  then correctly acts on the flag (such as creating a new object in the scene).
    void interpretIncomingPackage(byte[] thePackage, int sizeOfPackage)
    {
        //a byte[] to hold the actual data
        byte[] theActualData = new byte[sizeOfPackage - sizeof(int)];

        //a byte[] to hold the flag data
        byte[] flagData = new byte[sizeof(int)];

        //GET THE FLAG OF THE PACKET
        Array.Copy(thePackage, flagData, sizeof(int));

        //convert the flag data into the actual int flag
        int flagInt = BitConverter.ToInt32(flagData, 0);
        //convert the flagInt into the actual flag
        Global.NetFlag flag = (Global.NetFlag)flagInt;

        //GET THE ACTUAL DATA OF THE PACKET
        //copy only the actual data into the actual data byte array
        for (int i = 0; i < theActualData.Length; i++)
        {
            theActualData[i] = thePackage[i + 4]; //start at the place of the actual data and do not grab the flag data that was in the front of the package
        }

        //Do work based on the flag found
        if (flag == Global.NetFlag.MESH_FLAG)
        { //The packet was sending a mesh
            // Pass the data to the mesh serializer. 
            List<Mesh> meshes = new List<Mesh>(SimpleMeshSerializer.Deserialize(theActualData));
            globalMeshes.AddRange(meshes);
            
            // For each mesh, create a GameObject to render it.
            for (int index = 0; index < meshes.Count; index++)
            {
                GameObject surface = AddSurfaceObject(meshes[index], string.Format("Beamed-{0}", surfaceObjects.Count), transform);
                surface.transform.parent = SpatialMappingManager.Instance.transform;
                surface.GetComponent<MeshRenderer>().enabled = true;
                surface.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        else if (flag == Global.NetFlag.OBJECT_CREATE_FLAG)
        { //Receiving an object creation flag
            // Get the Position info
            float posX = BitConverter.ToSingle(theActualData, 0);
            float posY = BitConverter.ToSingle(theActualData, 4);
            float posZ = BitConverter.ToSingle(theActualData, 8);
            // Get the Rotation info
            float rotX = BitConverter.ToSingle(theActualData, 12);
            float rotY = BitConverter.ToSingle(theActualData, 16);
            float rotZ = BitConverter.ToSingle(theActualData, 20);
            // Create a new Position and Rotation for the object based on the passed info
            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            Vector3 newPosition = new Vector3(posX, posY, posZ);
            // Determine the prefab of the object (See: Global.ObjType)
            Global.ObjType objType = (Global.ObjType)BitConverter.ToInt32(theActualData, 24);
            // Get the Object ID info
            int objId = BitConverter.ToInt32(theActualData, 28);
            // Create the correct prefab
            GameObject newObj = GameObject.Instantiate(Resources.Load("Prefabs/" + objType.ToString())) as GameObject;
            // Set the position and rotation
            newObj.transform.position = newPosition;
            newObj.transform.rotation = newRotation;
            // Set the name equal to the object ID
            newObj.name = objId.ToString();
            // Add this to the hashmap
            Global.AddObject(objId, newObj);

            Debug.Log(objType.ToString() + " created");
        }
        else if (flag == Global.NetFlag.STRING_FLAG)
        {  //The packet was sending a string message

            //decode the message
            string something = Encoding.ASCII.GetString(theActualData);

            //print out the message
            UnityEngine.Debug.Log("Message: " + something);
        }
        else if (flag == Global.NetFlag.OBJECT_MOVE_FLAG)
        { //Receiving an object move flag
            float posX = BitConverter.ToSingle(theActualData, 0);
            float posY = BitConverter.ToSingle(theActualData, 4);
            float posZ = BitConverter.ToSingle(theActualData, 8);

            float rotX = BitConverter.ToSingle(theActualData, 12);
            float rotY = BitConverter.ToSingle(theActualData, 16);
            float rotZ = BitConverter.ToSingle(theActualData, 20);

            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            Vector3 newPosition = new Vector3(posX, posY, posZ);
            
            int objId = BitConverter.ToInt32(theActualData, 24);
            GameObject newObj = Global.GetObject(objId);
            newObj.transform.position = newPosition;
            newObj.transform.rotation = newRotation;

            //Debug.Log(newObj.name + " Moved");
        }
        else if(flag == Global.NetFlag.CAMERA_FLAG)
        { // The packet is sending the camera position
            // Set our camera position equal to the sent position
            float posX = BitConverter.ToSingle(theActualData, 0);
            float posY = BitConverter.ToSingle(theActualData, 4);
            float posZ = BitConverter.ToSingle(theActualData, 8);
            // Get the Rotation info
            float rotX = BitConverter.ToSingle(theActualData, 12);
            float rotY = BitConverter.ToSingle(theActualData, 16);
            float rotZ = BitConverter.ToSingle(theActualData, 20);
            // Create a new Position and Rotation for the object based on the passed info
            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            Vector3 newPosition = new Vector3(posX, posY, posZ);

            Camera projCamera = GameObject.Find("KinectReference").GetComponent<Camera>();
            Camera topCamera = GameObject.Find("Top-Down").GetComponent<Camera>();

            projCamera.transform.position = newPosition;
            projCamera.transform.rotation = newRotation;

            Quaternion topRotation = Quaternion.Euler(90, rotY, 0);
            topCamera.transform.rotation = topRotation;
        }
        else if(flag == Global.NetFlag.HOLO_HEAD_CREATION_FLAG)
        {
            float posX = BitConverter.ToSingle(theActualData, 0);
            float posY = BitConverter.ToSingle(theActualData, 4);
            float posZ = BitConverter.ToSingle(theActualData, 8);

            float rotX = BitConverter.ToSingle(theActualData, 12);
            float rotY = BitConverter.ToSingle(theActualData, 16);
            float rotZ = BitConverter.ToSingle(theActualData, 20);

            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            Vector3 newPosition = new Vector3(posX, posY, posZ);

            int objId = BitConverter.ToInt32(theActualData, 24);
            GameObject newObj = GameObject.Instantiate(Resources.Load("Prefabs/HoloHead")) as GameObject;

            newObj.transform.position = newPosition;
            newObj.transform.rotation = newRotation;

            newObj.name = objId.ToString();
            Global.AddObject(objId, newObj);

            UnityEngine.Debug.Log("HoloHead Created!");
        }
        else if(flag == Global.NetFlag.DELETE_FLAG)
        {
            int objId = BitConverter.ToInt32(theActualData, 0);

            Global.DeleteObject(objId);
        }
        else
        {
            //PACKAGE TYPE DID NOT HAVE A VALID FLAG IDENTIFIER IN THE FRONT OF PACKAGE
            UnityEngine.Debug.LogError("UNKNOWN PACKAGE RECIEVED");
        }

    }

    /// <summary>
    /// Reads an int from the next 4 bytes of the supplied stream.
    /// </summary>
    /// <param name="stream">The stream to read the bytes from.</param>
    /// <returns>An integer representing the bytes.</returns>
    int ReadInt(Stream stream)
    {
        // The bytes arrive in the wrong order, so swap them.
        byte[] bytes = new byte[4];
        stream.Read(bytes, 0, 4);
        byte t = bytes[0];
        bytes[0] = bytes[3];
        bytes[3] = t;

        t = bytes[1];
        bytes[1] = bytes[2];
        bytes[2] = t;

        // Then bitconverter can read the int32.
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Called when a client connects.
    /// </summary>
    /// <param name="result">The result of the connection.</param>
    void OnClientConnect(IAsyncResult result)
    {
        if (result.IsCompleted)
        {
            networkClient = networkListener.EndAcceptTcpClient(result);
            if (networkClient != null)
            {
                //Debug.Log("Connected for Recieve");
                ClientConnected = true;
            }
        }
    }


}
