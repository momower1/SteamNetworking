using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// TODO: Fix crackling noise by using streaming audio like here: https://forum.unity.com/threads/example-voicechat-with-unet-and-steamworks.482721/

namespace SteamNetworking
{
    [RequireComponent(typeof(AudioSource))]
    public class VoiceChat : NetworkBehaviour
    {
        [Header("Hold or press fast twice to toggle recording on/off")]
        public KeyCode recordKey = KeyCode.Tab;
        public Texture recordIcon;
        public bool recording = false;
        public bool mirror = false;

        private AudioSource audioSource;
        private bool toggleRecording = false;
        private float lastTimeKeyDown = -1;

        private ArrayList bufferSamples = new ArrayList();
        private const int minBufferLength = 6000;
        private const int sampleRate = 24000;

        protected override void StartClient()
        {
            audioSource = GetComponent<AudioSource>();

            Facepunch.Steamworks.Client.Instance.Voice.OnCompressedData += OnCompressedData;
        }

        protected override void UpdateClient()
        {
            if (Input.GetKey(recordKey))
            {
                Facepunch.Steamworks.Client.Instance.Voice.WantsRecording = true;
            }
            else
            {
                Facepunch.Steamworks.Client.Instance.Voice.WantsRecording = toggleRecording;
            }

            if (Input.GetKeyDown(recordKey))
            {
                if ((Time.unscaledTime - lastTimeKeyDown) < 0.5f)
                {
                    toggleRecording = !toggleRecording;
                }

                lastTimeKeyDown = Time.unscaledTime;
            }

            recording = Facepunch.Steamworks.Client.Instance.Voice.IsRecording;

            // Play received voice recording when it is long enough
            if (bufferSamples.Count > minBufferLength)
            {
                // Create a new clip from the buffer and play it
                AudioClip clip = AudioClip.Create("Voice", bufferSamples.Count, 1, sampleRate, false);
                clip.SetData((float[])bufferSamples.ToArray(typeof(float)), 0);
                audioSource.PlayOneShot(clip);

                bufferSamples.Clear();
            }
        }

        private void OnCompressedData(byte[] data, int dataLength)
        {
            byte[] compressedData = new byte[dataLength];
            Array.Copy(data, compressedData, dataLength);

            // Send to all clients except yourself (if you don't want to mirror yourself)
            ulong[] memberIDs = NetworkManager.Instance.GetLobbyMemberIDs();

            foreach (ulong steamID in memberIDs)
            {
                if (mirror || steamID != Facepunch.Steamworks.Client.Instance.SteamId)
                {
                    SendToClient(steamID, compressedData, SendType.Reliable);
                }
            }
        }

        protected override void OnClientReceivedMessageRaw(byte[] data, ulong steamID)
        {
            System.IO.MemoryStream stream = new System.IO.MemoryStream();

            if (Facepunch.Steamworks.Client.Instance.Voice.Decompress(data, stream, sampleRate))
            {
                // 16 bit signed PCM data
                byte[] uncompressedData = stream.ToArray();

                float[] samples = new float[uncompressedData.Length / 2];

                for (int i = 0; i < uncompressedData.Length; i += 2)
                {
                    samples[i / 2] = (BitConverter.ToInt16(uncompressedData, i) / (float)Int16.MaxValue);
                }

                // Add it to the buffer to play later
                bufferSamples.AddRange(samples);
            }
            else
            {
                Debug.LogWarning("Failed to decompress voice chat data.");
            }
        }

        private void OnGUI()
        {
            if (!networkObject.onServer && recording)
            {
                UnityEngine.GUI.Label(new Rect(Screen.width / 4, 2 * (Screen.height / 3), Screen.width / 20, Screen.width / 20), recordIcon);
            }
        }

        protected override void OnDestroyClient()
        {
            if (Facepunch.Steamworks.Client.Instance != null)
            {
                Facepunch.Steamworks.Client.Instance.Voice.OnCompressedData -= OnCompressedData;
            }
        }
    }
}
