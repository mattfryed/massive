namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.U2D;
    using SplineUtility = UnityEngine.Splines.SplineUtility;

    public static class SpriteShapeSplineExtension
    {
        public const int k_splineDetail = 5;
        public static UnityEngine.Splines.Spline s_cachedSpline = new UnityEngine.Splines.Spline();
        
        
        public static void Evaluate(this Spline spline, float t, out Vector3 position, out Vector3 tangent, out Vector3 upVector)
        {
            s_cachedSpline.Clear();
            for (int i = 0; i < spline.GetPointCount(); i++)
            {
                s_cachedSpline.Add(new UnityEngine.Splines.BezierKnot(spline.GetPosition(i), spline.GetLeftTangent(i), spline.GetRightTangent(i)));
            }
            
            SplineUtility.Evaluate(s_cachedSpline, t, out var positionFloat3, out var tangentFloat3, out var upVectorFloat3);
            position = new Vector3(positionFloat3.x, positionFloat3.y, positionFloat3.z);
            tangent = new Vector3(tangentFloat3.x, tangentFloat3.y, tangentFloat3.z);
            upVector = new Vector3(upVectorFloat3.x, upVectorFloat3.y, upVectorFloat3.z);
        }
        
        
        /// <summary>
        /// Returns the local position along the spline based on progress 0 - 1.
        /// Good for lerping an object along the spline.
        /// <para></para>
        /// Example: transform.localPosition = spline.GetPoint(0.5f)
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="progress">Value from 0 - 1</param>
        /// <returns></returns>
        public static Vector2 EvaluatePosition(this Spline spline, float progress)
        {
            var length = spline.GetPointCount();
            var i = Mathf.Clamp(Mathf.CeilToInt((length - 1) * progress), 0, length - 1);
  
            var t = progress * (length - 1) % 1f;
            if (i == length - 1 && progress >= 1f)
                t = 1;

            var prevIndex = Mathf.Max(i - 1, 0);
  
            var _p0 = new Vector2(spline.GetPosition(prevIndex).x, spline.GetPosition(prevIndex).y);
            var _p1 = new Vector2(spline.GetPosition(i).x, spline.GetPosition(i).y);
            var _rt = _p0 + new Vector2(spline.GetRightTangent(prevIndex).x, spline.GetRightTangent(prevIndex).y);
            var _lt = _p1 + new Vector2(spline.GetLeftTangent(i).x, spline.GetLeftTangent(i).y);

            return BezierUtility.BezierPoint(
                new Vector2(_p0.x, _p0.y),
                new Vector2(_rt.x, _rt.y),
                new Vector2(_lt.x, _lt.y),
                new Vector2(_p1.x, _p1.y),
                t
            );
        }

        public static bool EvaluatePositionAndTangentNormalized(this Spline spline, float t, out Vector3 position, out Vector3 tangent)
        {
            // Ensure t is clamped between 0 and 1
            t = Mathf.Clamp01(t);

            int pointCount = spline.GetPointCount();
            if (pointCount < 2)
            {
                Debug.LogWarning("Spline must have at least 2 points to evaluate a position.");
                position = Vector3.zero;
                tangent = Vector3.zero;
                return false;
            }

            // Calculate total length of the spline using BezierUtility
            float totalLength = 0f;
            float[] segmentLengths = new float[pointCount - 1];

            for (int i = 0; i < pointCount - 1; i++)
            {
                Vector3 p0 = spline.GetPosition(i);
                Vector3 p1 = spline.GetPosition(i + 1);
                Vector3 t0 = spline.GetRightTangent(i);
                Vector3 t1 = spline.GetLeftTangent(i + 1);

                float segmentLength = BezierCurveLength(k_splineDetail, t0, p0, p1, t1);
                segmentLengths[i] = segmentLength;
                totalLength += segmentLength;
            }

            // Find the segment where the normalized position falls
            float targetLength = t * totalLength;
            float cumulativeLength = 0f;

            for (int i = 0; i < segmentLengths.Length; i++)
            {
                float segmentLength = segmentLengths[i];
                if (cumulativeLength + segmentLength >= targetLength)
                {
                    // Found the segment
                    float segmentT = (targetLength - cumulativeLength) / segmentLength;

                    // Evaluate Bezier curve position
                    Vector3 p0 = spline.GetPosition(i);
                    Vector3 p1 = spline.GetPosition(i + 1);
                    Vector3 t0 = spline.GetRightTangent(i);
                    Vector3 t1 = spline.GetLeftTangent(i + 1);

                    position = BezierUtility.BezierPoint(t0, p0, p1, t1, segmentT);
                    tangent = EvaluateTangentOnBezierWithPoints(t0, p0, p1, t1, segmentT);
                    return true;
                }

                cumulativeLength += segmentLength;
            }

            // If for some reason we didn't find the segment, return the last point
            position = spline.GetPosition(pointCount - 1);
            tangent = -spline.GetLeftTangent(pointCount - 1);
            return true;
        }
        
        static float SplineLength(this Spline spline, int splineDetail)
        {
            // Expand the Bezier.
            int controlPointContour = spline.GetPointCount() - 1;
            float splineLength = 0f;
            for (int i = 0; i < controlPointContour; ++i)
            {
                int j = i + 1;

                Vector3 p0 = spline.GetPosition(i);
                Vector3 p1 = spline.GetPosition(j);
                Vector3 rt = p0 + spline.GetRightTangent(i);
                Vector3 lt = p1 + spline.GetLeftTangent(j);

                splineLength += BezierCurveLength(splineDetail, rt, p0, p1, lt);
            }

            return splineLength;
        }

        private static float BezierCurveLength(int splineDetail, Vector3 rt, Vector3 p0, Vector3 p1, Vector3 lt)
        {
            Vector3 sp = p0;
            var spd = 0f;
            var fmax = (float)(splineDetail - 1);
            for (int n = 1; n < splineDetail; ++n)
            {
                float t = (float)n / fmax;
                Vector3 bp = BezierUtility.BezierPoint(rt, p0, p1, lt, t);
                float d = math.distance(bp, sp);
                spd += d;
                sp = bp;
            }

            return spd;
        }
        
        private static Vector3 EvaluateTangentOnBezier(Vector3 rt, Vector3 p0, Vector3 p1, Vector3 lt, float t)
        {
            return EvaluateTangentOnBezierWithPoints(p0, p0 + rt, p1 + lt, p1, t);
        }
        
        /// <summary>
        /// Computes the tangent vector at a given normalized position (0 to 1) on a cubic Bezier curve.
        /// </summary>
        /// <param name="p0">Start point of the curve.</param>
        /// <param name="p1">Control point 1.</param>
        /// <param name="p2">Control point 2.</param>
        /// <param name="p3">End point of the curve.</param>
        /// <param name="t">Normalized position (0 to 1).</param>
        /// <returns>The tangent vector at the given position.</returns>
        public static Vector3 EvaluateTangentOnBezierWithPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // Ensure t is clamped between 0 and 1
            t = Mathf.Clamp01(t);

            // First derivative of the cubic Bezier curve
            float u = 1 - t;

            // Compute tangent vector
            Vector3 tangent = 
                (3 * u * u * (p1 - p0)) +           // Derivative of (1 - t)^3 * P0
                (6 * u * t * (p2 - p1)) +           // Derivative of 3 * (1 - t)^2 * t * P1
                (3 * t * t * (p3 - p2));            // Derivative of 3 * (1 - t) * t^2 * P2

            return tangent.normalized; // Return the normalized tangent vector
        }
    }
}