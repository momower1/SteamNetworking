using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MastersOfTempest.Networking
{
    public class Avatar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ulong steamID;

        public Image avatar;
        public Image imageNameBackground;
        public Text textName;

        void Start()
        {
            textName.text = Facepunch.Steamworks.Client.Instance.Friends.Get(steamID).Name;
            Facepunch.Steamworks.Client.Instance.Friends.GetAvatar(Facepunch.Steamworks.Friends.AvatarSize.Large, steamID, OnImage);
        }

        public void OnImage(Facepunch.Steamworks.Image image)
        {
            if (image.IsError)
            {
                //Debug.Log("Failed to load avatar of user " + steamID + ". Trying again...");
                Facepunch.Steamworks.Client.Instance.Friends.GetAvatar(Facepunch.Steamworks.Friends.AvatarSize.Large, steamID, OnImage);
            }
            else
            {
                Texture2D texture = new Texture2D(image.Width, image.Height);

                for (int x = 0; x < image.Width; x++)
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        Facepunch.Steamworks.Color c = image.GetPixel(x, y);
                        texture.SetPixel(x, image.Height - y, new Color(c.r / 255.0f, c.g / 255.0f, c.b / 255.0f, c.a / 255.0f));
                    }
                }

                texture.Apply();

                avatar.sprite = Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height), Vector2.zero);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            imageNameBackground.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            imageNameBackground.gameObject.SetActive(false);
        }
    }
}
