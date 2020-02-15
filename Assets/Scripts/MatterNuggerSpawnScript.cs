using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatterNuggerSpawnScript : MonoBehaviour
{
    public GameObject matterNuggetPrefab;
    public float respawnBaseTime = 15f;
    public float variance = 5f;
    public bool spawnFromParentPosition = false;
    private GameObject gameplayObjects; 

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
        GameObject mn = Instantiate(matterNuggetPrefab, gameplayObjects.transform);
        
        if (!spawnFromParentPosition)
        {
            mn.transform.position = new Vector3(0f, 0f, 0f);
        }
        else
        {
            mn.transform.position = transform.parent.position;
        }
        float ejectPowerX = Random.Range(-50f, 50f);
        float ejectPowerY = Random.Range(-50f, 50f);
        if (ejectPowerX >= 0f)
        {
            ejectPowerX += 50f;
        }
        else
        {
            ejectPowerX -= 50f;
        }
        if (ejectPowerY >= 0f)
        {
            ejectPowerY += 50f;
        }
        else
        {
            ejectPowerY -= 50f;
        }
        mn.GetComponent<Rigidbody>().AddForce(ejectPowerX, 0f, ejectPowerY);
        Invoke("SpawnSoon", Random.Range(respawnBaseTime, respawnBaseTime + variance));
    }
}
