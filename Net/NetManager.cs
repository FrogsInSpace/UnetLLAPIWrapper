﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// The NetManager is responsible for creating our client and server sockets and housing our messaging queue/delegate system.
/// </summary>
public class NetManager : MonoBehaviour {

	// Connection config vars
	static public ConnectionConfig mConnectionConfig;
	static public byte mChannelReliable;
	static public byte mChannelUnreliable;

	// True if Init has ran.
	static public bool mIsInitialized = false;

	// Lists to hold our clients
	static public List<NetServer> mServers = new List<NetServer>();
	static public List<NetClient> mClients = new List<NetClient>();

	// Delegates for polling
	public delegate void NetEventHandler( int connectionId , int channelId , byte[] buffer , int datasize );
	public static NetEventHandler OnClientConnection = null;
	public static NetEventHandler OnClientData = null;
	public static NetEventHandler OnClientDisconnect = null;

	/// <summary>
	/// Initialize our low level network APIs.
	/// </summary>
	public static void Init (){

		// Set up NetworkTransport
		GlobalConfig gc = new GlobalConfig();
		gc.ReactorModel = ReactorModel.FixRateReactor;
		gc.ThreadAwakeTimeout = 10;
		NetworkTransport.Init (gc);

		// Set up our channel configuration
		mConnectionConfig = new ConnectionConfig();
		mChannelReliable = mConnectionConfig.AddChannel (QosType.ReliableSequenced);
		mChannelUnreliable = mConnectionConfig.AddChannel(QosType.UnreliableSequenced);

		mIsInitialized = true;

	}

	public static void Shutdown (){
		NetworkTransport.Shutdown ();
		mIsInitialized = false;
	}
	
	/// <summary>
	/// Creates a server that listens on a given port.
	/// </summary>
	/// <returns>The server object.</returns>
	/// <param name="maxConnections">Max connections.</param>
	/// <param name="port">Port.</param>
	public static NetServer CreateServer ( int maxConnections , int port ){

		NetServer s = new NetServer( maxConnections , port );

		// If we were successful in creating our server and it is unique
		if(s.mIsRunning && mServers.Contains (s) != true ){
			mServers.Add (s);
		}

		return s;
	}


	/// <summary>
	/// Destroys the server.
	/// </summary>
	/// <returns><c>true</c>, if server was destroyed, <c>false</c> otherwise.</returns>
	/// <param name="s">NetServer to destroy</param>
	public static bool DestroyServer( NetServer s ){

		if( mServers.Contains (s) == false){
			Debug.Log ("NetManager::DestroyServer( " + s.mSocket.ToString() + ") - Server does not exist!");
			return false;
		}

		NetworkTransport.RemoveHost( s.mSocket );

		mServers.Remove( s );
		
		return true;
	}


	/// <summary>
	/// Create a client that is ready to connect with a server.
	/// </summary>
	/// <returns>The client.</returns>
	public static NetClient CreateClient (){

		if(!mIsInitialized){
			Debug.Log ("NetManager::CreateServer( ... ) - NetManager was not initialized. Did you forget to call NetManager.Init()?");
			return null;
		}

		NetClient c = new NetClient();

		if(mClients.Contains(c) != true ){
			mClients.Add (c);
		}

		return c;
	}

	/// <summary>
	/// Destroys specified client.
	/// </summary>
	/// <returns><c>true</c>, if client was destroyed, <c>false</c> otherwise.</returns>
	/// <param name="c">NetClient object to destroy</param>
	public static bool DestroyClient( NetClient c ){

		if( mClients.Contains(c) == false ){
			Debug.Log ("NetManager::DestroyClient( " + c.mSocket.ToString () + ") - Client does not exist!" );
			return false;
		}

		NetworkTransport.RemoveHost( c.mSocket );

		mClients.Remove (c);

		return true;
	}

	/// <summary>
	/// Reads network events and delegates how they are used
	/// </summary>
	public static void PollEvents(){

		// If nothing is running, why bother
		if( mServers.Count < 1 && mClients.Count < 1 ){
			return;
		}
		
		int recHostId; 
		int connectionId;
		int channelId;
		int dataSize;
		byte[] buffer = new byte[1024];
		byte error;
		
		NetworkEventType networkEvent = NetworkEventType.DataEvent;

		// Process network events for n clients and n servers
		while( mIsInitialized && networkEvent != NetworkEventType.Nothing )
		{
			int i = -1; // Index for netserver in mservers

			networkEvent = NetworkTransport.Receive( out recHostId , out connectionId , out channelId , buffer , 1024 , out dataSize , out error );

			// Route message to our server delegate
			i = mServers.FindIndex ( x => x.mSocket == recHostId );
			if( i != -1 ){
				mServers[i].OnMessage( networkEvent , connectionId , channelId , buffer , dataSize );
			}

			// Route message to our client delegate
			// Client Connect Event
			i = mClients.FindIndex ( c => c.mSocket.Equals (recHostId) );
			if( i != -1 ){
				mClients[i].OnMessage( networkEvent , connectionId , channelId , buffer, dataSize );
			}
			
			switch(networkEvent){

			// Nothing
			case NetworkEventType.Nothing:
				break;

			// Connect
			case NetworkEventType.ConnectEvent:

				// Server Connect Event
				i = mServers.FindIndex ( s => s.mSocket.Equals (recHostId) );

				if( i != -1 ){
					mServers[i].AddClient ( connectionId );
				}

				// Client Connect Event
				i = mClients.FindIndex ( c => c.mSocket.Equals (recHostId) );
				if( i != -1 ){
					mClients[i].mIsConnected = true; // Set client connected to true
				}
				
				break;

			// Data 
			case NetworkEventType.DataEvent:

				// Server received data
				i = mServers.FindIndex ( x => x.mSocket == recHostId );
				if( i != -1 ){
					// Empty
				}

				// Client Received Data
				i = mClients.FindIndex ( c => c.mSocket.Equals (recHostId) );
				if( i != -1 ){
					// Empty
				}
				break;

			// Disconnect
			case NetworkEventType.DisconnectEvent:

				// Server Disconnect Event
				i = mServers.FindIndex ( x => x.mSocket == recHostId );
				if( i != -1 ){
						mServers[i].RemoveClient ( connectionId );
				}

				// Client Disconnect Event
				i = mClients.FindIndex ( c => c.mSocket.Equals (recHostId) );
				if( i != -1 ){
					mClients[i].mIsConnected = false; // Set client connected to true
				}
				

				break;
			}
			
		}
	}

}
