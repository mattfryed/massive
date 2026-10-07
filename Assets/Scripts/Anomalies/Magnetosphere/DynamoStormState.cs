using UnityEngine;

namespace Massive.Dynamo
{
    /// <summary>Controller-owned storm timing, independent of any renderer or particle lifetime.</summary>
    public struct DynamoStormState
    {
        public bool Active;
        public int Revision;
        public Vector3 Flow;
        public float Elapsed, DirectionAge, Strength, Opacity, Travel;
        public float Front, Feather;
        public DynamoStormFootprint Footprint;

        public float VisibilityAt(Vector3 position)
        {
            if (!Active) return 0f;
            float t = Mathf.Clamp01((Front - Vector3.Dot(position, Flow)) / Mathf.Max(.001f, Feather));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>The actual camera footprint on the XZ gameplay plane; supports oblique cameras.</summary>
    public struct DynamoStormFootprint
    {
        public Vector3 BottomLeft, BottomRight, TopRight, TopLeft;
        public Vector3 Corner(int index) => index == 0 ? BottomLeft : index == 1 ? BottomRight : index == 2 ? TopRight : TopLeft;

        public static DynamoStormFootprint FromCamera(Camera camera, float planeY, Vector3 center, Vector2 fallbackSize)
        {
            var result = new DynamoStormFootprint();
            var plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            for (int i = 0; i < 4; i++)
            {
                float x = i == 1 || i == 2 ? 1 : 0;
                float y = i >= 2 ? 1 : 0;
                Vector3 p = new Vector3(center.x + (x - .5f) * fallbackSize.x, planeY, center.z + (y - .5f) * fallbackSize.y);
                if (camera)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(x, y, 0));
                    if (plane.Raycast(ray, out float distance)) p = ray.GetPoint(distance);
                }
                if (i == 0) result.BottomLeft = p;
                else if (i == 1) result.BottomRight = p;
                else if (i == 2) result.TopRight = p;
                else result.TopLeft = p;
            }
            return result;
        }

        public void Project(Vector3 axis, out float min, out float max)
        {
            min = max = Vector3.Dot(BottomLeft, axis);
            for (int i = 1; i < 4; i++)
            {
                float s = Vector3.Dot(Corner(i), axis);
                min = Mathf.Min(min, s); max = Mathf.Max(max, s);
            }
        }

        public Bounds Bounds
        {
            get
            {
                var bounds = new Bounds(BottomLeft, Vector3.zero);
                bounds.Encapsulate(BottomRight); bounds.Encapsulate(TopRight); bounds.Encapsulate(TopLeft);
                bounds.Expand(new Vector3(2, 4, 2));
                return bounds;
            }
        }

        public Vector3 FlowFromScreenSource(Vector2 source)
        {
            if (source.sqrMagnitude < .0001f) source = Vector2.left;
            Vector3 d = -(BottomRight - BottomLeft).normalized * source.x - (TopLeft - BottomLeft).normalized * source.y;
            d.y = 0;
            return d.sqrMagnitude > .0001f ? d.normalized : Vector3.right;
        }
    }
}
