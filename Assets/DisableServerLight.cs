using MastersOfTempest.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableServerLight : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (GetComponent<ServerObject>().onServer)
            GetComponent<Light>().enabled = false;
        else
            GetComponent<Light>().cullingMask = 1 << LayerMask.NameToLayer("Client");
    }
}
