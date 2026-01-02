namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using System;
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.U2D;
    using SplineUtility = UnityEngine.Splines.SplineUtility;

    /// <summary>
    /// Data to define a OSU spline.
    /// </summary>
    [Serializable]
    public struct SpriteShapeSplineData
    {
        [Tooltip("The Spline size")]
        [SerializeField] private float m_Size;
        [Tooltip("The spline renderer")]
        [SerializeField] private SpriteShapeRenderer m_ShapeRenderer;
        [Tooltip("The spline controller")]
        [SerializeField] private SpriteShapeController m_ShapeController;

        public float Size { get => m_Size; set => m_Size = value; }
        public SpriteShapeRenderer ShapeRenderer { get => m_ShapeRenderer; set => m_ShapeRenderer = value; }
        public SpriteShapeController ShapeController { get => m_ShapeController; set => m_ShapeController = value; }

        public SpriteShapeSplineData(float size = 1f)
        {
            m_Size = size;
            m_ShapeController = null;
            m_ShapeRenderer = null;
        }
    }

    [ExecuteInEditMode]
    public class SpriteShapeControllerSynchronizer : MonoBehaviour
    {
        
        
        [FormerlySerializedAs("m_KeepFirstPointAtOrigin")]
        [Header("Start Point")]
        [Tooltip("Keep first point at 0,0")]
        [SerializeField] private bool m_KeepStartPointAtOrigin = true;
        [FormerlySerializedAs("m_ObjectToStart")]
        [FormerlySerializedAs("m_ObjectToOrigin")]
        [Tooltip("The object to place at the start Point point")]
        [SerializeField] private Transform m_ObjectToStartPoint;
        [FormerlySerializedAs("m_StartObjectMatchSplineRotation")]
        [FormerlySerializedAs("m_RotateOriginObjectToSpline")]
        [Tooltip("Rotate Origin object to spline direction")]
        [SerializeField] private bool m_StartObjectMatchSplineOrientation = true;
        
        [Header("End Point")]
        [Tooltip("Keep first point at 0,0")]
        [SerializeField] private Transform m_ObjectToEndPoint;
        [FormerlySerializedAs("m_EndObjectMatchSplineRotation")]
        [FormerlySerializedAs("m_RotateEndObjectToSpline")]
        [Tooltip("Rotate end object to spline direction")]
        [SerializeField] private bool m_EndObjectMatchSplineOrientation;
        
        [Header("Prefab spawn")]
        [Tooltip("Spawn on start point")]
        [SerializeField] private bool m_SpawnPrefabOnStartPoint = false;
        [Tooltip("Spawn on start point")]
        [SerializeField] private bool m_SpawnPrefabOnStartEndPoint = false;
        [Tooltip("Keep first point at 0,0")]
        [SerializeField] private Transform m_InstanceContainer;
        [Tooltip("Keep first point at 0,0")]
        [SerializeField] private GameObject m_PrefabToSpawnAtEachPoint;
        [FormerlySerializedAs("m_PrefabInstanceMatchSplineRotation")]
        [Tooltip("Keep first point at 0,0")]
        [SerializeField] private bool m_PrefabInstanceMatchSplineOrientation;
        
        [Header("Sprite Shape Splines")]
        [Tooltip("The main spline that controls the additional splines")]
        [SerializeField] protected SpriteShapeSplineData m_MainSpline = new SpriteShapeSplineData(1f);
        
        [Tooltip("Other spline that follow the main spline control")]
        [SerializeField] protected SpriteShapeSplineData[] m_AdditionalSplines = new []{new SpriteShapeSplineData(1.2f)};
        
        private void LateUpdate()
        {
            SychronizeWithMainController();
        }
        
        public void SychronizeWithMainController()
        {
            if(m_MainSpline.ShapeController == null) {
                return;
            }

            if (m_KeepStartPointAtOrigin) {
                m_MainSpline.ShapeController.spline.SetPosition(0, Vector3.zero);
            }
        
            var sourceSpline = m_MainSpline.ShapeController.spline;
            var pointCount = sourceSpline.GetPointCount();
        
            for (int i = 0; i < pointCount; i++) {
                sourceSpline.SetHeight(i, m_MainSpline.Size); 
            }

            var splineScale = m_MainSpline.ShapeController.transform.localScale;
            if (pointCount > 0 && m_ObjectToStartPoint != null) {
                var firstPointPos = m_MainSpline.ShapeController.spline.GetPosition(0);
            
                m_ObjectToStartPoint.position = m_MainSpline.ShapeController.transform.position + Vector3.Scale(firstPointPos, splineScale);

                if (m_StartObjectMatchSplineOrientation) {
                    m_ObjectToStartPoint.right = m_MainSpline.ShapeController.spline.GetRightTangent(0);
                }
            }
        
            if (pointCount > 0 && m_ObjectToEndPoint != null) {
                var lastPointPos = m_MainSpline.ShapeController.spline.GetPosition(pointCount-1);
            
                m_ObjectToEndPoint.position = m_MainSpline.ShapeController.transform.position + Vector3.Scale(lastPointPos, splineScale);
                
                if (m_EndObjectMatchSplineOrientation) {
                    m_ObjectToEndPoint.right = -m_MainSpline.ShapeController.spline.GetLeftTangent(pointCount-1);
                }
            }

            if (m_PrefabToSpawnAtEachPoint != null) {
                var prefabCount = m_MainSpline.ShapeController.spline.GetPointCount() - (m_SpawnPrefabOnStartPoint ? 1 : 0) -(m_SpawnPrefabOnStartEndPoint ? 1 : 0);
                var transformChildCount = m_InstanceContainer.transform.childCount;
                if (prefabCount != transformChildCount) {
                    
                    //Cleanup.
                    for (int i = transformChildCount - 1; i >= 0; i--) {
                        if (Application.isPlaying) {
                            Destroy(m_InstanceContainer.transform.gameObject);
                        } else {
                            DestroyImmediate(m_InstanceContainer.transform.gameObject);
                        }
                    }
                    
                    //Create new instances
                    for (int i = 0; i < prefabCount; i++) {
                        
                        var splineIndex = i + (m_SpawnPrefabOnStartPoint ? 1 : 0);
                        
                        var instance = Instantiate(m_PrefabToSpawnAtEachPoint, m_InstanceContainer);
                        instance.name = m_PrefabToSpawnAtEachPoint.name;
                        var midPointPos = sourceSpline.GetPosition(splineIndex);
                        instance.transform.position = m_MainSpline.ShapeController.transform.position + Vector3.Scale(midPointPos, splineScale);
                        
                        if (m_PrefabInstanceMatchSplineOrientation) {
                            instance.transform.right = -m_MainSpline.ShapeController.spline.GetLeftTangent(splineIndex);
                        }
                        
                    }
                }
            }
        
            if (m_AdditionalSplines == null || m_AdditionalSplines.Length == 0) { return; }

            // Synchronize spline data
            for (int i = 0; i < m_AdditionalSplines.Length; i++)
            {
                var otherSpline = m_AdditionalSplines[i];
                if (otherSpline.ShapeController != null)
                {
                    SyncSplineData(m_MainSpline, otherSpline);
                }
            }
        }
        
        public void ForceDraw()
        {
            foreach (var additionalSpline in m_AdditionalSplines) {
                var splineController = additionalSpline.ShapeController;
                //var spriteRenderer = additionalSpline.ShapeRenderer;
                if(splineController == null){ continue; }

                splineController.UpdateSpriteShapeParameters();
                splineController.BakeMesh();
            }
        }

        public static void SyncSplineData(SpriteShapeSplineData source, SpriteShapeSplineData target)
        {
            // Ensure the target has the same number of spline points
            var sourceSpline = source.ShapeController.spline;
            var targetSpline = target.ShapeController.spline;
            
            // Source requires at least 2 points.
            if (sourceSpline.GetPointCount() < 2) {
                sourceSpline.InsertPointAt(0, Vector3.zero);
            }
            
            if (sourceSpline.GetPointCount() < 2) {
                sourceSpline.InsertPointAt(0, Vector3.one);
            }

            if (targetSpline.GetPointCount() != sourceSpline.GetPointCount())
            {
                while (targetSpline.GetPointCount() > sourceSpline.GetPointCount())
                {
                    targetSpline.RemovePointAt(targetSpline.GetPointCount() - 1);
                }

                while (targetSpline.GetPointCount() < sourceSpline.GetPointCount())
                {
                    targetSpline.InsertPointAt(targetSpline.GetPointCount(), Vector3.zero);
                }
            }

            // Copy spline points
            for (int i = 0; i < sourceSpline.GetPointCount(); i++)
            {
                targetSpline.SetPosition(i, sourceSpline.GetPosition(i));
                targetSpline.SetTangentMode(i, sourceSpline.GetTangentMode(i));
                targetSpline.SetLeftTangent(i, sourceSpline.GetLeftTangent(i));
                targetSpline.SetRightTangent(i, sourceSpline.GetRightTangent(i));
                targetSpline.SetHeight(i,target.Size);
            }
        }
        
        public static void SyncSplineData(Spline sourceSpline, Spline targetSpline)
        {
            if(sourceSpline == targetSpline){ return; }

            // Source requires at least 2 points.
            if (sourceSpline.GetPointCount() < 2) {
                sourceSpline.InsertPointAt(0, Vector3.zero);
            }
            
            if (sourceSpline.GetPointCount() < 2) {
                sourceSpline.InsertPointAt(0, Vector3.one);
            }
            
            
            if (targetSpline.GetPointCount() != sourceSpline.GetPointCount())
            {
                while (targetSpline.GetPointCount() > sourceSpline.GetPointCount())
                {
                    targetSpline.RemovePointAt(targetSpline.GetPointCount() - 1);
                }

                while (targetSpline.GetPointCount() < sourceSpline.GetPointCount())
                {
                    var previousPoint = targetSpline.GetPosition(targetSpline.GetPointCount() - 1);
                    targetSpline.InsertPointAt(targetSpline.GetPointCount(), previousPoint + Vector3.one);
                }
            }

            // Copy spline points
            for (int i = 0; i < sourceSpline.GetPointCount(); i++)
            {
                if(IsPositionValid(targetSpline, i, sourceSpline.GetPosition(i), out var validPoint)){
                    targetSpline.SetPosition(i, validPoint);
                }
                targetSpline.SetTangentMode(i, sourceSpline.GetTangentMode(i));
                targetSpline.SetLeftTangent(i, sourceSpline.GetLeftTangent(i));
                targetSpline.SetRightTangent(i, sourceSpline.GetRightTangent(i));
                targetSpline.SetHeight(i,sourceSpline.GetHeight(i));
                targetSpline.SetCorner(i, sourceSpline.GetCorner(i));
                targetSpline.SetSpriteIndex(i,sourceSpline.GetSpriteIndex(i));
            }
        }
        
        public static bool IsPositionValid(Spline spline, int index, Vector3 point, out Vector3 validPoint)
        {
            float KEpsilon = 0.001f;
            var next = index + 1;
            int pointCount = spline.GetPointCount();
            if (spline.isOpenEnded && (index == 0 || index == pointCount)) {
                validPoint = point;
                return true;
            }
            
            int prev = (index == 0) ? (pointCount - 1) : (index - 1);
            if (prev >= 0)
            {
                Vector3 diff = spline.GetPosition(prev) - point;
                if (diff.magnitude < KEpsilon) {
                    validPoint = point + Vector3.one *KEpsilon*2;
                    return false;
                }
                    
            }
            next = (next >= pointCount) ? 0 : next;
            if (next < pointCount)
            {
                Vector3 diff = spline.GetPosition(next) - point;
                if (diff.magnitude < KEpsilon) {
                    validPoint = point + Vector3.one *KEpsilon*2;
                    return false;
                }
                    
            }
            validPoint = point;
            return true;
        }

       
    }
}