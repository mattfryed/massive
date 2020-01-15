using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelect : MonoBehaviour
{
    public void LoadLevel(string levelName)
    {
        GameObject dm = GameObject.FindWithTag("DataManager");
        dm.GetComponent<DataManagerScript>().levelToLoad = levelName;
        SceneManager.LoadScene("S-0_INSTRUCTIONS");
    }
}