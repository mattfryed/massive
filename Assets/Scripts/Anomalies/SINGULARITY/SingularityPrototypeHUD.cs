using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Small standalone navigation legend. No match, score or save-state dependencies.</summary>
    public sealed class SingularityPrototypeHUD : MonoBehaviour
    {
        public SingularitySurface surface;
        public SingularityPlayerMotor player;
        private GUIStyle title, small, status;

        private void OnGUI()
        {
            if (!surface || !player) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                status = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(25, 12, 420, 34), "SINGULARITY", title);
            GUI.color = new Color(.65f, .78f, .86f);
            GUI.Label(new Rect(27, 46, 600, 25), "SURFACE LOOP PROTOTYPE  /  PLAYER + GRID", small);
            string region = surface.Region(player.SurfacePosition.y);
            GUI.Label(new Rect(Screen.width - 290, 20, 260, 32), region, status);
            GUI.Label(new Rect(25, Screen.height - 40, Screen.width - 50, 28),
                "MOVE: player 1 stick / WASD / arrows     HOLD UP: front → top turn → rear → bottom turn     No combat or black hole yet", small);
            GUI.color = Color.white;
        }
    }
}
