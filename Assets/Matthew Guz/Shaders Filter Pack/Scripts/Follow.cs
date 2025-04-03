using UnityEngine;


namespace MatthewAssets
{

    public class VignetteFollowScript : MonoBehaviour
    {
        public Transform targetObject; // target
        public Material wallMaterial;  // Shader's material
        public Vector2 effectOffset = Vector2.zero; // Movement
        private Camera mainCamera;

        void Start()
        {
            mainCamera = Camera.main;
        }

        void Update()
        {
            if (targetObject != null && wallMaterial != null)
            {

                Vector3 screenPos = mainCamera.WorldToViewportPoint(targetObject.position);


                if (screenPos.z > 0)
                {
                    wallMaterial.SetVector("_ObjectScreenPos", new Vector4(screenPos.x, screenPos.y, 0, 0));
                    wallMaterial.SetVector("_Offset", new Vector4(effectOffset.x, effectOffset.y, 0, 0));
                }
            }
        }
    }
}