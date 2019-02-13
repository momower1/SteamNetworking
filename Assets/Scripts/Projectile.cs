using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamNetworking;

public class Projectile : NetworkBehaviour
{
    public float speed = 10;
    public float timeUntilDestroy = 10;

    protected override void UpdateServer()
    {
        transform.position += speed * Time.deltaTime * transform.forward;

        if (timeUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }

        timeUntilDestroy -= Time.deltaTime;
    }
}
