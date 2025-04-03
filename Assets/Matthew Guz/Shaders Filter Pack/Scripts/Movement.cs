using UnityEngine;

namespace MatthewAssets
{

    public class Movement : MonoBehaviour
    {
        public float speed = 5f; // Movement speed
        public Vector3 direction = Vector3.forward; 
        public Vector3 InitialPosition;

        void Start()
        {
            InitialPosition = transform.position; // Save Position
        }

        void Update()
        {
            
            transform.Translate(direction * speed * Time.deltaTime);

            // Reset position, press D or A
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.A))
            {
                transform.position = InitialPosition;
            }
        }
    }
}