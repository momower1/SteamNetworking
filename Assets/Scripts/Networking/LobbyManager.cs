using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Facepunch.Steamworks;

namespace SteamNetworking
{
    public class LobbyManager : MonoBehaviour
    {
        public GameObject friendAvatarPrefab;
        public GameObject lobbyAvatarPrefab;

        public Transform layoutLobby;
        public Transform layoutFriends;

        public UnityEngine.UI.Text textLobby;
        public UnityEngine.UI.Text textFriends;
        public UnityEngine.UI.Button readyButton;

        [Header("Change this to your development scene(s)")]
        public string serverSceneName = "Server";
        public string clientSceneName = "Client";

        private Dictionary<ulong, LobbyAvatar> lobbyAvatars = new Dictionary<ulong, LobbyAvatar>();

        private ulong lobbyIDToJoin;
        private bool gameStarted = false;
        private bool ready = false;

        void Start()
        {
            if (Client.Instance != null)
            {
                Client.Instance.Lobby.OnLobbyCreated += OnLobbyCreatedOrJoined;
                Client.Instance.Lobby.OnLobbyJoined += OnLobbyCreatedOrJoined;
                Client.Instance.Lobby.OnUserInvitedToLobby += OnUserInvitedToLobby;
                Client.Instance.Lobby.OnLobbyStateChanged += OnLobbyStateChanged;
                Client.Instance.Lobby.OnLobbyMemberDataUpdated += OnLobbyMemberDataUpdated;

                NetworkManager.Instance.clientMessageEvents[NetworkMessageType.StartGame] += OnMessageLobbyStartGame;

                // Create a lobby that the player is in when the game starts
                CreateDefaultLobby();
                StartCoroutine(RefreshFriendAvatars());
            }
        }

        // Called when someone joins/leaves the lobby
        private void OnLobbyStateChanged (Lobby.MemberStateChange stateChange, ulong steamID, ulong affectedSteamID)
        {
            if (stateChange == Lobby.MemberStateChange.Entered)
            {
                // Create avatar for this user
                lobbyAvatars[steamID] = InstantiateLobbyAvatar(Client.Instance.Friends.Get(steamID));
            }
            else
            {
                // Destroy the avatar of this user
                Destroy(lobbyAvatars[steamID].gameObject);
                lobbyAvatars.Remove(steamID);
            }
        }

        // Called when the data of a member in the lobby is updated
        private void OnLobbyMemberDataUpdated (ulong steamID)
        {
            lobbyAvatars[steamID].Refresh();
            CheckForEveryoneReady();
        }

        private void OnMessageLobbyStartGame(byte[] data, ulong steamID)
        {
            LoadingScreen.Instantiate();

            // Load client scene
            SceneManager.LoadScene(clientSceneName);

            // Also load server scene if you are the owner of the lobby
            if (Client.Instance.Lobby.Owner == Client.Instance.SteamId)
            {
                SceneManager.LoadScene(serverSceneName, LoadSceneMode.Additive);
            }
        }

        private void OnLobbyCreatedOrJoined(bool success)
        {
            if (success && Client.Instance.Lobby.IsValid)
            {
                Debug.Log("Created/joined lobby \"" + Client.Instance.Lobby.Name + "\"");
                InitializeLobby();
            }
            else
            {
                Debug.LogError("Failed to create/join lobby. Trying to recreate the default lobby...");
                CreateDefaultLobby();
            }
        }

        private void OnUserInvitedToLobby(ulong lobbyID, ulong otherUserID)
        {
            Debug.Log("Got invitation to the lobby " + lobbyID + " from user " + otherUserID);
            lobbyIDToJoin = lobbyID;
            string message = "Player " + Client.Instance.Friends.Get(otherUserID).Name + " invited you to a lobby.\nDo you want to join?";
            DialogBox.Show(message, true, true, AcceptLobbyInvitation, null);
        }

        private void CreateDefaultLobby()
        {
            Client.Instance.Lobby.Create(Lobby.Type.FriendsOnly, 4);
            Client.Instance.Lobby.Name = Client.Instance.Username + "'s Lobby";
        }

        private void AcceptLobbyInvitation()
        {
            Client.Instance.Lobby.Leave();
            Client.Instance.Lobby.Join(lobbyIDToJoin);
        }

        private void InitializeLobby()
        {
            // Destroy the old lobby
            foreach (LobbyAvatar lobbyAvatar in lobbyAvatars.Values)
            {
                Destroy(lobbyAvatar.gameObject);
            }

            lobbyAvatars.Clear();

            ulong[] lobbyMemberIDs = Client.Instance.Lobby.GetMemberIDs();

            // Spawn all members of the new lobby
            foreach (ulong steamID in lobbyMemberIDs)
            {
                lobbyAvatars[steamID] = InstantiateLobbyAvatar(Client.Instance.Friends.Get(steamID));
                lobbyAvatars[steamID].Refresh();
            }

            textLobby.text = Client.Instance.Lobby.Name;
            Client.Instance.Lobby.SetMemberData("Ready", ready.ToString());
        }

        private void CheckForEveryoneReady()
        {
            // Only the lobby owner checks if everyone is ready and then sends a message to everyone to start the game
            if (!gameStarted && Client.Instance.Lobby.NumMembers > 0)
            {
                // Always check this because the lobby owner could have changed
                if (Client.Instance.SteamId == Client.Instance.Lobby.Owner)
                {
                    bool everyoneReady = true;

                    foreach (LobbyAvatar lobbyAvatar in lobbyAvatars.Values)
                    {
                        if (!lobbyAvatar.ready)
                        {
                            everyoneReady = false;
                            break;
                        }
                    }

                    if (everyoneReady)
                    {
                        // Send the game start message only once
                        byte[] data = System.Text.Encoding.UTF8.GetBytes("LobbyStartGame");
                        NetworkManager.Instance.SendToAllClients(data, NetworkMessageType.StartGame, Facepunch.Steamworks.Networking.SendType.Reliable);
                        gameStarted = true;
                    }
                }
            }
        }

        public void Ready()
        {
            ready = !ready;
            Client.Instance.Lobby.SetMemberData("Ready", ready.ToString());
            readyButton.GetComponent<UnityEngine.UI.Image>().color = ready ? new UnityEngine.Color(0, 0.25f, 0) : new UnityEngine.Color(0.25f, 0, 0);
            readyButton.GetComponentInChildren<UnityEngine.UI.Text>().text = ready ? "Ready" : "Click to Ready up";
        }

        private IEnumerator RefreshFriendAvatars()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.25f);

                FriendAvatar[] friendAvatars = layoutFriends.GetComponentsInChildren<FriendAvatar>();

                Dictionary<ulong, bool> friendsToStay = new Dictionary<ulong, bool>();

                // Mark all the friends for removal later
                foreach (FriendAvatar f in friendAvatars)
                {
                    friendsToStay[f.steamID] = false;
                }

                // Refresh all friends of this user
                Client.Instance.Friends.Refresh();
                IEnumerable<SteamFriend> friends = Client.Instance.Friends.All;

                foreach (SteamFriend friend in friends)
                {
                    if (friend.IsOnline)
                    {
                        if (!friendsToStay.ContainsKey(friend.Id))
                        {
                            // A new friend is now online
                            InstantiateFriendAvatar(friend);
                        }

                        // This friend should not be removed later
                        friendsToStay[friend.Id] = true;
                    }
                }

                // Remove all friends that are no longer online
                foreach (FriendAvatar f in friendAvatars)
                {
                    if (!friendsToStay[f.steamID])
                    {
                        Destroy(f.gameObject);
                    }
                }
            }
        }

        private FriendAvatar InstantiateFriendAvatar(SteamFriend friend)
        {
            FriendAvatar tmp = Instantiate(friendAvatarPrefab, layoutFriends, false).GetComponent<FriendAvatar>();
            tmp.gameObject.name = friend.Name;
            tmp.steamID = friend.Id;
            return tmp;
        }

        private LobbyAvatar InstantiateLobbyAvatar(SteamFriend friend)
        {
            LobbyAvatar tmp = Instantiate(lobbyAvatarPrefab, layoutLobby, false).GetComponent<LobbyAvatar>();
            tmp.gameObject.name = friend.Name;
            tmp.steamID = friend.Id;
            return tmp;
        }

        private void OnDestroy()
        {
            if (Client.Instance != null)
            {
                Client.Instance.Lobby.OnLobbyCreated -= OnLobbyCreatedOrJoined;
                Client.Instance.Lobby.OnLobbyJoined -= OnLobbyCreatedOrJoined;
                Client.Instance.Lobby.OnUserInvitedToLobby -= OnUserInvitedToLobby;
                Client.Instance.Lobby.OnLobbyMemberDataUpdated -= OnLobbyMemberDataUpdated;
                Client.Instance.Lobby.OnLobbyStateChanged -= OnLobbyStateChanged;

                NetworkManager.Instance.clientMessageEvents[NetworkMessageType.StartGame] -= OnMessageLobbyStartGame;
            }
        }
    }
}
