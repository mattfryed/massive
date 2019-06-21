using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class PlaneMask : MonoBehaviour {


	public Renderer[] renderers;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {

		Vector3 pos = transform.position;
		Vector3 normal = transform.up;
		for (int i = 0; i < renderers.Length; i++) {
			renderers[i].material.SetVector ("pV", pos);
			renderers[i].material.SetVector ("pN", normal);
		}


	}
}
