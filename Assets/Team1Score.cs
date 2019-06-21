using UnityEngine;
using UnityEngine.UI;

public class Team1Score : MonoBehaviour
{

    public Transform ScoreSphere;
    public Text ScoreTeam1;

    // Update is called once per frame
    void Update()
    {
        ScoreTeam1.text = ScoreSphere.localScale.z.ToString("##0 # . 0 '0 / 1 0 0 0'");
        

    }
}
