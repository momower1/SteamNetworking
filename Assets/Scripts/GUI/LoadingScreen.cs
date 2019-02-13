using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    public Transform loadingIcon;
    public float rotationSpeed;

	void Update ()
    {
        loadingIcon.Rotate(0, 0, Time.deltaTime * rotationSpeed);
	}

    public void Destroy ()
    {
        Destroy(gameObject);
    }

    public static void Instantiate ()
    {
        Instantiate(Resources.Load<GameObject>("Loading Screen Canvas"));
    }
}
