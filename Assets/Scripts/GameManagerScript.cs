using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    public GameObject Score_1;
    public GameObject Score_2;
    public float finalScore_1;
    public float finalScore_2;
    public float winScale = 10.9f;

    public string levelName = "NOVA";
    public string levelNumber = "003";

    private bool isGameOver = false;

    private GameObject gmm;
    public GameObject sss;
    private GameObject gameplayObjects;
    private GameObject deathSphere;

    private PlayerRosterController roster;

    private PlayerControllerScript p1c, p2c, p3c, p4c;

    void Start()
    {
        GameFlowContext.EnsureExists();

        gmm = GameObject.FindWithTag("GameMusicManager");
        deathSphere = GameObject.FindWithTag("DeathSphere");
        gameplayObjects = GameObject.FindWithTag("GameplayObjects");

        roster = FindFirstObjectByType<PlayerRosterController>();
        CachePlayerControllers();
    }

    private void CachePlayerControllers()
    {
        if (roster == null)
        {
            Debug.LogWarning("[GameManagerScript] No PlayerRosterController found. Falling back to scene find once.");
            var p1 = GameObject.Find("Players/Player 1");
            var p2 = GameObject.Find("Players/Player 2");
            var p3 = GameObject.Find("Players/Player 3");
            var p4 = GameObject.Find("Players/Player 4");

            p1c = p1 != null ? p1.GetComponent<PlayerControllerScript>() : null;
            p2c = p2 != null ? p2.GetComponent<PlayerControllerScript>() : null;
            p3c = p3 != null ? p3.GetComponent<PlayerControllerScript>() : null;
            p4c = p4 != null ? p4.GetComponent<PlayerControllerScript>() : null;
            return;
        }

        p1c = roster.P1 != null ? roster.P1.GetComponent<PlayerControllerScript>() : null;
        p2c = roster.P2 != null ? roster.P2.GetComponent<PlayerControllerScript>() : null;
        p3c = roster.P3 != null ? roster.P3.GetComponent<PlayerControllerScript>() : null;
        p4c = roster.P4 != null ? roster.P4.GetComponent<PlayerControllerScript>() : null;
    }

    void Update()
    {
        if (isGameOver) return;

        CheckForInactivePlayers();

        if (Score_1.transform.localScale.x >= winScale ||
            Score_1.transform.localScale.x <= 0f ||
            Score_2.transform.localScale.x >= winScale ||
            Score_2.transform.localScale.x <= 0f)
        {
            Debug.Log("Game is now over");
            isGameOver = true;
            StoreFinalScores();
        }
    }

    private void CheckForInactivePlayers()
    {
        bool is2v2 = GameFlowContext.Instance.IsTwoVTwo;

        if (!is2v2)
        {
            if (p1c != null && p3c != null && !p1c.isActive && !p3c.isActive)
                EndGamePrematurely();
        }
        else
        {
            if (p1c != null && p2c != null && p3c != null && p4c != null &&
                !p1c.isActive && !p2c.isActive && !p3c.isActive && !p4c.isActive)
                EndGamePrematurely();
        }
    }

    public void EndGame()
    {
        if (deathSphere != null)
            deathSphere.GetComponent<DSScript>().Engage();
    }

    public void ShowEndScreen()
    {
        if (gameplayObjects != null) gameplayObjects.SetActive(false);
        if (sss != null) sss.SetActive(true);

        if (deathSphere != null)
            deathSphere.GetComponent<DSScript>().ScrollOff();
    }

    public void EndGamePrematurely()
    {
        // (kept empty as your original)
    }

    public void StoreFinalScores()
    {
        isGameOver = true;

        finalScore_1 = Score_1.transform.localScale.x;
        finalScore_2 = Score_2.transform.localScale.x;

        if (gmm != null)
            gmm.GetComponent<MusicManagerScript>()?.StopMusic();

        if (finalScore_1 < 0f) finalScore_1 = 0f;
        if (finalScore_2 < 0f) finalScore_2 = 0f;

        EndGame();
    }
}
