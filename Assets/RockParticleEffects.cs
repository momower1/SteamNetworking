using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RockParticleEffects : MonoBehaviour
{
    public ParticleSystem rock;

    // Update is called once per frame
    void Update()
    {
        if(Input.anyKey)
        {
            rock.Play();
            Destroy(gameObject);
            
        }
    }

    
}
