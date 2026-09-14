using System;
using UnityEngine;

namespace Massive.Dynamo
{
    /// <summary>Demonstration controls; separate from competitive gameplay and its HUD.</summary>
    public sealed class ScientificDynamoDemo : MonoBehaviour
    {
        [SerializeField] private ScientificMagnetosphere source;
        [SerializeField] private Camera view;
        [SerializeField] private Transform orbitTarget;
        [SerializeField] private bool orbit;
        private float yaw = 20;
        private float elevation = 24;
        private float distance = 52;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) source.Playing = !source.Playing;
            if (Input.GetKeyDown(KeyCode.R)) { source.SeekNormalized(0); source.Playing = true; }
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 3;
                elevation = Mathf.Clamp(elevation - Input.GetAxis("Mouse Y") * 3, -80, 80);
            }
            if (orbit) yaw += Time.deltaTime * 6;
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 2, 15, 80);
        }

        private void LateUpdate()
        {
            if (!view || !orbitTarget) return;
            Vector3 direction = Quaternion.Euler(elevation, yaw, 0) * Vector3.back;
            Vector3 focus = source && source.Ready
                ? source.GsmToWorld.MultiplyPoint3x4(new Vector3(-6,0,0)) : orbitTarget.position;
            view.transform.position = focus + direction * distance;
            view.transform.LookAt(focus, Vector3.up);
        }

        private void OnGUI()
        {
            if (!source || !source.Ready) return;
            GUILayout.BeginArea(new Rect(20, 20, 390, 245), GUI.skin.box);
            GUILayout.Label("DYNAMO / OBSERVED SPACE WEATHER");
            GUILayout.Label(source.Episode.manifest.title);
            var date = DateTimeOffset.FromUnixTimeSeconds((long)source.UtcSeconds);
            GUILayout.Label(date.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") + "   |   " + source.UpstreamPressure.ToString("F2") + " nPa upstream");
            float position = (float)(source.Elapsed / source.Episode.Duration);
            float seek = GUILayout.HorizontalSlider(position, 0, 1);
            if (Mathf.Abs(seek-position) > .0001f) { source.Playing = false; source.SeekNormalized(seek); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(source.Playing ? "Pause" : "Play")) source.Playing = !source.Playing;
            if (GUILayout.Button("Restart")) { source.SeekNormalized(0); source.Playing = true; }
            orbit = GUILayout.Toggle(orbit, "Orbit view");
            GUILayout.EndHorizontal();
            GUILayout.Label("Pink: magnetic field   /   Cyan: local plasma flow");
            GUILayout.Label("Right-drag: orbit   /   Scroll: zoom   /   Space: pause");
            GUILayout.Label("NASA CCMC · University of Michigan SWMF · OMNI");
            GUILayout.Label("Pressure-pulse excerpt · model reconstruction · time compressed");
            GUILayout.EndArea();
        }
    }
}
