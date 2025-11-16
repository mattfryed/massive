using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class PlaneMask : MonoBehaviour {


	public Renderer[] renderers;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
		void Update()
		{
			Vector3 pos = transform.position;
			Vector3 normal = transform.up;

			for (int i = 0; i < renderers.Length; i++)
			{
		#if UNITY_EDITOR
				// Use sharedMaterial in edit mode to avoid leaks
				renderers[i].sharedMaterial.SetVector("pV", pos);
				renderers[i].sharedMaterial.SetVector("pN", normal);
		#else
				// Use material in play mode to ensure each renderer has its own instance
				renderers[i].material.SetVector("pV", pos);
				renderers[i].material.SetVector("pN", normal);
		#endif
			}
		}

}
