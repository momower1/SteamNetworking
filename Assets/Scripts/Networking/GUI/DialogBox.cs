using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SteamNetworking.GUI
{
    // Creates simple dialog boxes at runtime with yes/no buttons that invoke functions when they are pressed
    public class DialogBox : MonoBehaviour
    {
        public delegate void OnButtonClick();

        public Text text;
        public Button buttonYes;
        public Button buttonNo;
        public OnButtonClick onYes;
        public OnButtonClick onNo;

        private CursorLockMode cursorLockMode;
        private bool cursorVisible;

        public static void Show(string text, bool showYesButton, bool showNoButton, OnButtonClick onYes, OnButtonClick onNo)
        {
            GameObject dialogBoxPrefab = Resources.Load<GameObject>("Dialog Box");
            DialogBox dialogBox = Instantiate(dialogBoxPrefab).GetComponent<DialogBox>();
            dialogBox.text.text = text;
            dialogBox.cursorVisible = Cursor.visible;
            dialogBox.cursorLockMode = Cursor.lockState;
            dialogBox.buttonYes.gameObject.SetActive(showYesButton);
            dialogBox.buttonNo.gameObject.SetActive(showNoButton);
            dialogBox.onYes = onYes;
            dialogBox.onNo = onNo;

            // Unlock and show cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Yes()
        {
            if (onYes != null)
            {
                onYes.Invoke();
            }

            Close();
        }

        public void No()
        {
            if (onNo != null)
            {
                onNo.Invoke();
            }

            Close();
        }

        public void Close()
        {
            // Only reset the cursor if this is the only DialogBox
            // Otherwise the last one in the stack should reset it
            if (FindObjectsOfType<DialogBox>().Length == 1)
            {
                ResetCursor();
            }

            Destroy(gameObject);
        }

        private void ResetCursor ()
        {
            Cursor.visible = cursorVisible;
            Cursor.lockState = cursorLockMode;
        }
    }
}
