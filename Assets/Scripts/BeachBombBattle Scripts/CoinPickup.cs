using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinPickup : MonoBehaviour 
{
    public int value;

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
            FindObjectOfType<GameManager>().AddCoin(value);

            GameObject clone = Instantiate(pickupEffect, transform.position, transform.rotation);
            ParticleSystem.MainModule particle = clone.GetComponent<ParticleSystem>().main;
            Destroy(clone, 1.2f);
            Destroy(gameObject);
            GameObject effect = Instantiate(pickupEffect, transform.position, transform.rotation);
            Destroy(effect, 1.2f);
            

        }
    }
}