using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnomalyUISequencer : MonoBehaviour
{
    public BoxIntroOutro topBannerBox;
    public BoxIntroOutro[] lowerBoxes;
    public float stagger = 0.06f;

    public IEnumerator PlayIn()
    {
        if (topBannerBox != null) yield return topBannerBox.PlayIn();
        if (lowerBoxes != null)
        {
            for (int i = 0; i < lowerBoxes.Length; i++)
            {
                if (lowerBoxes[i] != null) StartCoroutine(lowerBoxes[i].PlayIn());
                yield return new WaitForSeconds(stagger);
            }
        }
    }

    public IEnumerator PlayOut()
    {
        if (lowerBoxes != null)
        {
            for (int i = lowerBoxes.Length - 1; i >= 0; i--)
            {
                if (lowerBoxes[i] != null) StartCoroutine(lowerBoxes[i].PlayOut());
                yield return new WaitForSeconds(stagger);
            }
        }
        if (topBannerBox != null) yield return topBannerBox.PlayOut();
    }
}
