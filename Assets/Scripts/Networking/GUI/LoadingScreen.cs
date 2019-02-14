using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamNetworking.GUI
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField]
        protected Transform loadingIcon;
        [SerializeField]
        protected float rotationSpeed;

        protected void Update()
        {
            loadingIcon.Rotate(0, 0, Time.deltaTime * rotationSpeed);
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }

        public static void Instantiate()
        {
            Instantiate(Resources.Load<GameObject>("Loading Screen Canvas"));
        }
    }
}
