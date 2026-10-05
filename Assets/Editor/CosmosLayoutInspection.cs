#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CosmosLayoutInspection
{
    [MenuItem("MASSIVE/COSMOS/Inspect current layout %#&F9")]
    public static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        var text = new StringBuilder(scene.path + " dirty=" + scene.isDirty + " playing=" + Application.isPlaying + "\n");
        foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
        {
            string path = AnimationUtility.CalculateTransformPath(t, null);
            text.AppendLine(path + " active=" + t.gameObject.activeSelf + " pos=" + t.position + " scale=" + t.lossyScale + " [" + string.Join(",", t.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name)) + "]");
            if (path.Contains("Goals") || path.Contains("VectorGridGPU") || t.name.Contains("Respawn"))
                foreach (var c in t.GetComponents<Component>().Where(c => c && !(c is Transform) && !(c is MeshRenderer) && !(c is MeshFilter)))
                    text.AppendLine(c.GetType().Name + " " + EditorJsonUtility.ToJson(c));
        }
        Directory.CreateDirectory("Library/CosmosLayout");
        File.WriteAllText("Library/CosmosLayout/inspection.txt", text.ToString());
        Debug.Log("COSMOS layout inspection saved.");
    }
}
#endif
