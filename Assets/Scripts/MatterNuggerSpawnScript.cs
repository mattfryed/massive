using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatterNuggerSpawnScript : MonoBehaviour
{
    public GameObject matterNuggetPrefab;
    public float respawnBaseTime = 15f;
    public float variance = 5f;

    // Start is called before the first frame update
    void Start()
    {
        Invoke("SpawnSoon", respawnBaseTime);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void SpawnSoon()
    {
        GameObject mn = Instantiate(matterNuggetPrefab);
        mn.transform.position = new Vector3(0f, 0f, 0f);

        mn.GetComponent<Rigidbody>().AddForce(Random.Range(-100.0f, 100.0f), 0f, Random.Range(-100f,100f));
        Invoke("SpawnSoon", Random.Range(respawnBaseTime, respawnBaseTime + variance));
    }
}
