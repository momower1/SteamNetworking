using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

public class Projectile : NetworkBehaviour
{
    [SerializeField]
    protected float speed = 10;
    [SerializeField]
    protected float timeUntilDestroy = 10;

    protected override void StartServer()
    {
        GetComponent<Rigidbody>().velocity = speed * transform.forward;
    }

    protected override void UpdateServer()
    {
        if (timeUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }

        timeUntilDestroy -= Time.deltaTime;
    }
}
