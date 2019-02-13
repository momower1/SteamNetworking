## Features
- Steam networking in Unity with a dedicated host using the Facepunch.Steamworks API
- Synchronization of transforms (Position, Rotation, Scale, Parent, Instantiate, Destroy)
- The host runs the server scene and the client scene at the same time, all other players only run the client scene
- Use one single script to send and handle custom messages between the client and the server
- Set different behaviour for the object when it is on the client and when it is on the server

## General setup
- Steam has to be running in the background for the networking to work
- The server scene needs a GameServer object and the client scene a GameClient object
- Server scene should do everything (movement, physics, ...), the client scene just automatically spawns all the objects and sends player input to the server
- All objects in the client scene have to be on the layer _Client_ and all objects in the server scene have to be on the layer _Server_
- Display1 shows the game from the client camera, Display2 shows the game from the server camera and the SceneView shows both perspectives
- You have to load the lobby scene first in order to initialize the networking and click ready to load the server and client scene. You can change the _LobbyManager_ attributes _string:serverSceneName_ and _string:clientSceneName_ to load your own scenes for testing purposes

## How to use
- **Synchronize the transform of an object:**
  - Add _NetworkObject_ script to the object
  - Set the layer of the object to _Server_
  - Save the object as a Prefab in the _Resources/NetworkObjects/_ folder
  - Create instances of this prefab only in the server scene
  - The prefab should be spawned and synchronized for all instances in the client scene
  - You can add NetworkObject scripts to children of the prefab, then these children will be synchronized as well
  - You should not have physics on the client object (you can check the client properties to remove colliders and rigidbodies on the client)
- **Synchronized instantiation**
  - Instantiating a prefab from the _Resources/NetworkObjects/_ folder in the server scene will also instantiate and synchronize it with the client (only NetworkObject prefabs can be instantiated/duplicated, e.g it won't work with children)
- **Custom messages over script:**
  - Right click in a folder in the inspector and select _Create/C# Script NetworkBehaviour_
  - The template script should be created and opened
  - Look at the example from the script and change it accordingly
  - Add the script to a server object prefab in the _Resources/NetworkObjects/_ folder
  - GameObjects can have multiple NetworkBehaviours attached to them
  - You cannot add or remove a NetworkBehaviour at runtime
  - You can override the OnClientMessageReceivedRaw(...) function if you want to save data by sending only the bytes instead of a JSON string. You can use the ByteSerializer class to convert a struct into a byte array and the other way around but keep in mind that the struct has to be fixed size and also below the maximum transmission size of the send type.
- **Different behaviour for client / server:**
  - The NetworkBehaviour methods StartServer(), UpdateServer() and OnDestroyServer() are basically Start(), Update(), OnDestroy() methods of the MonoBehaviour when the object is on the server. Otherwise StartClient(), UpdateClient() and OnDestroyClient() will be called. If you want to add e.g. OnCollisionEnter(...) with different behaviour on the server/client you can check the _bool:onServer_ attribute of the NetworkObject in an if-statement.
  - The GameServer and GameClient class have UnityEvents that are triggered when they are initialized. This is basically the same as StartServer() and StartClient() from the NetworkBehaviour. Use this instead of a NetworkBehaviour when you don't have to send messages.

## Issues and possible improvements
- Loading between different scenes should be implemented with a custom messages
- If the server leaves the game, everything crashes
- The lobby scene crashes for a lot of people (maybe the fix is to add the app id game "Spacewar" to the steam library)
