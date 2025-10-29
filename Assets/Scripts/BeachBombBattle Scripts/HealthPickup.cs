using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    public int healAmount;
    public bool isFullHeal;

    public GameObject pickupEffect;
    private GameObject clone;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            GameObject clone = Instantiate(pickupEffect, transform.position, transform.rotation);
            ParticleSystem.MainModule particle = clone.GetComponent<ParticleSystem>().main;
            Destroy(clone, 1.2f);
            Destroy(gameObject);
            GameObject effect = Instantiate(pickupEffect, transform.position, transform.rotation);
            Destroy(effect, 1.2f);

            if (isFullHeal)
            {
                HealthManager.instance.RespawnCo();
            }
            else
            {
                HealthManager.instance.HealPlayer(healAmount);
            }
        }
    }
}