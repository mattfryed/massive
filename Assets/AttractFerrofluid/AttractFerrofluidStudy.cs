using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Rewired;

namespace Massive.AttractStudy
{
    /// <summary>Opt-in visual replacement for the title sphere. The source object
    /// continues to own its particles, grid influence and menu behavior.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer),typeof(MeshFilter))]
    public sealed class AttractFerrofluidStudy : MonoBehaviour
    {
        [Tooltip("Switch between the original sphere and the ferrofluid surface. Updates live in Play Mode.")]
        public bool useFerrofluid=true;
        public Shader surfaceShader;
        [Range(64,192)] public int faceResolution=128;
        [Tooltip("Use a lighter body mesh while retaining the requested detail around the lettering. Applies on enable.")]
        public bool optimizeBodyGeometry=true;
        [Tooltip("Remove safely hidden rear geometry for the fixed orthographic title camera. Keeps a wide silhouette skirt. Applies on enable.")]
        public bool trimHiddenRear;
        public bool RearGeometryTrimmed {get;private set;}
        public int GeometryBuildCount {get;private set;}
        [Range(5,14)] public float surfaceDensity=9;
        [Range(.03f,.16f)] public float surfaceRelief=.10f;
        [Range(0,2)] public float motionSpeed=1;
        [InspectorName("Highlight Coverage"),Range(0,1)] public float wetness=.86f;
        [Range(0,.4f)] public float rimWidth=.20f;
        [Range(-180,180)] public float rimAngle=-40;
        [Header("Joystick attraction")]
        public bool joystickAttraction=true;
        [Range(0,.5f)] public float joystickDeadzone=.18f;
        [Min(.05f)] public float attractionSmoothTime=.7f;
        [Range(0,.25f)] public float attractionReach=.14f;
        public Camera attractionCamera;
        [Header("Embedded logo")]
        public bool embeddedLogo;
        public Texture2D logoDistanceField;
        public Texture2DArray logoGlyphDistanceFields;
        public Shader logoFieldShader;
        [Range(.25f,1.3f)] public float logoScale=1;
        [Tooltip("Extra space between letters, as a fraction of the original word width. Zero preserves the original spacing."),Range(-.008f,.06f)]
        public float logoLetterSpacing;
        [Range(1.5f,2.9f)] public float logoArcWidth=2.2f;
        [Range(-.4f,.4f)] public float logoElevation=.12f;
        [Range(0,.03f)] public float logoRecess=.016f;
        [Range(0,1)] public float logoSurfaceFlow=1;
        [Tooltip("Additional coupling of the outer recess lip to the existing mound heights and slopes. Zero retains the original ridge motion."),Range(0,1)]
        public float logoLipFlow;
        [Tooltip("How much the white letter floor follows the mounds. Zero keeps it stable; one follows the full surface motion."),Range(0,1)]
        public float logoTypeFlow=0;
        [Header("Lettering arrival")]
        public bool animateLogoArrival=true;
        [Min(0)] public float logoArrivalDelay=.25f;
        [Min(.1f)] public float logoArrivalDuration=3;
        public float LogoRevealProgress=>!animateLogoArrival?1:Mathf.Clamp01((logoArrivalElapsed-Mathf.Max(0,logoArrivalDelay))/Mathf.Max(.1f,logoArrivalDuration));
        public bool LogoIsEmbedded=>useFerrofluid && embeddedLogo && logoGlyphDistanceFields!=null;
        public Vector2 AttractionTarget {get;private set;}
        public Vector2 SmoothedAttraction=>attraction.Position;
        public Vector3 AttractionOffset {get;private set;}
        public int ActiveInputPlayers {get;private set;}
#if UNITY_EDITOR
        // Recording supplies the same per-player movement values as the input boundary.
        public System.Func<IReadOnlyList<Vector2>> PreviewInputProvider {get;set;}
        public float? PreviewArrivalDeltaTime {get;set;}
#endif
        public float AnimationTime {get;private set;}
        public Renderer SurfaceRenderer {get;private set;}
        public Mesh SurfaceMesh => mesh;
        Renderer original;
        bool originalEnabled;
        GameObject visual;
        Mesh mesh;
        int letteringResolution,bodyResolution;
        Vector3 retainedFront;
        float retainedRearCutoff;
        Material material;
        FerrofluidLogoField logoField;
        MaterialPropertyBlock properties;
        readonly FerrofluidAttraction attraction=new FerrofluidAttraction();
        readonly List<Vector2> playerSticks=new List<Vector2>(4);
        Camera inputCamera;
        int horizontalAction=-1,verticalAction=-1;
        Vector3 logoRight,logoUp,logoFront;
        Vector4 regionInputs,logoRegion;
        bool hasLogoRegion;
        float logoArrivalElapsed;
        bool logoWasEmbedded;
        static readonly int TimeId=Shader.PropertyToID("_FluidTime"),DensityId=Shader.PropertyToID("_Density"),ReliefId=Shader.PropertyToID("_Relief"),WetnessId=Shader.PropertyToID("_Wetness");
        static readonly int RimWidthId=Shader.PropertyToID("_RimWidth"),RimAngleId=Shader.PropertyToID("_RimAngle");
        static readonly int AttractionId=Shader.PropertyToID("_Attraction");
        static readonly int LogoFieldId=Shader.PropertyToID("_LogoSpacedField"),LogoSettingsId=Shader.PropertyToID("_LogoSettings");
        static readonly int LogoRegionId=Shader.PropertyToID("_LogoRegion");
        static readonly int LogoEnabledId=Shader.PropertyToID("_LogoEnabled");
        static readonly int LogoRevealId=Shader.PropertyToID("_LogoReveal");
        static readonly int LogoFlowId=Shader.PropertyToID("_LogoFlow");
        static readonly int LogoLipFlowId=Shader.PropertyToID("_LogoLipFlow");
        static readonly int LogoTypeFlowId=Shader.PropertyToID("_LogoTypeFlow");
        static readonly int LogoMeshGuardId=Shader.PropertyToID("_LogoMeshGuard");
        static readonly int LogoDetailId=Shader.PropertyToID("_LogoDetail");
        static readonly int LogoRightId=Shader.PropertyToID("_LogoRight"),LogoUpId=Shader.PropertyToID("_LogoUp"),LogoFrontId=Shader.PropertyToID("_LogoFront");

        void OnEnable()
        {
            if(Application.isPlaying && useFerrofluid)CreateSurface();
        }
        void CreateSurface()
        {
            if(SurfaceRenderer!=null)return;
            original=GetComponent<MeshRenderer>();originalEnabled=original.enabled;
            if(surfaceShader==null)surfaceShader=Shader.Find("MASSIVE/Study/Attract Ferrofluid");
            if(surfaceShader==null){Debug.LogError("[Attract Ferrofluid] Missing surface shader.",this);enabled=false;return;}
            if(logoFieldShader==null)logoFieldShader=Shader.Find("Hidden/MASSIVE/Spaced Letter Field");
            if(logoFieldShader!=null)logoField=new FerrofluidLogoField(logoFieldShader);
            AnimationTime=0;
            ReplayLogoArrival();logoWasEmbedded=LogoIsEmbedded;
            attraction.Reset();AttractionTarget=Vector2.zero;AttractionOffset=Vector3.zero;ActiveInputPlayers=0;
            inputCamera=attractionCamera!=null?attractionCamera:Camera.main;
            // Capture the orientation once: the imprint belongs to the surface,
            // rather than following the viewing camera like a billboard.
            logoRight=transform.InverseTransformDirection(inputCamera!=null?inputCamera.transform.right:Vector3.right);
            logoUp=transform.InverseTransformDirection(inputCamera!=null?inputCamera.transform.up:Vector3.forward);
            logoFront=transform.InverseTransformDirection(inputCamera!=null?-inputCamera.transform.forward:Vector3.up);
            letteringResolution=Mathf.Clamp(faceResolution,64,192);
            bodyResolution=optimizeBodyGeometry?Mathf.Min(letteringResolution,128):letteringResolution;
            RearGeometryTrimmed=trimHiddenRear && inputCamera!=null && inputCamera.orthographic;
            retainedFront=inputCamera!=null?transform.InverseTransformVector(-inputCamera.transform.forward).normalized:Vector3.up;
            retainedRearCutoff=RearGeometryTrimmed?SafeRearCutoff(surfaceRelief,logoRecess,logoTypeFlow):-1;
            mesh=BuildSphere(bodyResolution,retainedFront,retainedRearCutoff);GeometryBuildCount++;
            material=new Material(surfaceShader){name="Attract ferrofluid — transient",hideFlags=HideFlags.DontSave};
            material.SetVector("_LogoSourceSize",new Vector4(FerrofluidLogoLayout.Width,FerrofluidLogoLayout.Height,0,0));
            material.SetVector("_LogoFieldDomain",FerrofluidLogoField.Domain);
            visual=new GameObject("Ferrofluid surface — visual only"){hideFlags=HideFlags.DontSave,layer=gameObject.layer};
            visual.transform.SetParent(transform,false);
            // Keep the extra front-surface relief behind the world-space mode
            // labels. The source transform, colliders and particle origins stay put.
            visual.transform.localPosition=new Vector3(0,-.08f,0);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            SurfaceRenderer=visual.AddComponent<MeshRenderer>();SurfaceRenderer.sharedMaterial=material;
            SurfaceRenderer.shadowCastingMode=ShadowCastingMode.Off;SurfaceRenderer.receiveShadows=false;
            properties=new MaterialPropertyBlock();Apply();original.enabled=false;
        }
        void Update()
        {
            if(!useFerrofluid){if(SurfaceRenderer!=null)ReleaseSurface();return;}
            if(SurfaceRenderer==null)CreateSurface();
            if(SurfaceRenderer==null)return;
            AnimationTime+=Time.deltaTime*Mathf.Max(0,motionSpeed);
            // Arrival uses menu time, independently of surface speed or a time
            // scale inherited from gameplay. Cap stalls so the reveal stays visible.
            float arrivalDelta=Time.unscaledDeltaTime;
#if UNITY_EDITOR
            if(PreviewArrivalDeltaTime.HasValue)arrivalDelta=PreviewArrivalDeltaTime.Value;
#endif
            if(LogoIsEmbedded!=logoWasEmbedded){ReplayLogoArrival();logoWasEmbedded=LogoIsEmbedded;}
            if(LogoIsEmbedded)logoArrivalElapsed+=Mathf.Clamp(arrivalDelta,0,.05f);
            UpdateAttraction(Time.deltaTime);Apply();
        }
        public void ReplayLogoArrival(){logoArrivalElapsed=0;}
        IReadOnlyList<Vector2> ReadPlayerSticks()
        {
#if UNITY_EDITOR
            if(PreviewInputProvider!=null)return PreviewInputProvider();
#endif
            playerSticks.Clear();
            if(!ReInput.isReady){horizontalAction=-1;verticalAction=-1;return playerSticks;}
            if(horizontalAction<0 || verticalAction<0)
            {
                horizontalAction=ReInput.mapping.GetActionId("MoveH");
                verticalAction=ReInput.mapping.GetActionId("MoveV");
            }
            if(horizontalAction<0 || verticalAction<0)return playerSticks;
            var players=ReInput.players.Players;
            for(int i=0;i<players.Count;i++)
            {
                var player=players[i];
                if(player!=null)playerSticks.Add(new Vector2(player.GetAxisRaw(horizontalAction),player.GetAxisRaw(verticalAction)));
            }
            return playerSticks;
        }
        void UpdateAttraction(float deltaTime)
        {
            int active=0;
            AttractionTarget=joystickAttraction?FerrofluidAttraction.Average(ReadPlayerSticks(),joystickDeadzone,out active):Vector2.zero;
            ActiveInputPlayers=active;
            Vector2 point=attraction.Step(AttractionTarget,deltaTime,attractionSmoothTime);
            if(inputCamera==null)inputCamera=attractionCamera!=null?attractionCamera:Camera.main;
            Vector3 world=inputCamera!=null?inputCamera.transform.right*point.x+inputCamera.transform.up*point.y:new Vector3(point.x,0,point.y);
            AttractionOffset=transform.InverseTransformDirection(world)*Mathf.Clamp(attractionReach,0,.25f);
        }
        void Apply()
        {
            EnsureRearCoverage();
            properties.SetFloat(TimeId,AnimationTime);properties.SetFloat(DensityId,Mathf.Clamp(surfaceDensity,5,14));
            properties.SetFloat(ReliefId,Mathf.Clamp(surfaceRelief,.03f,.16f));properties.SetFloat(WetnessId,Mathf.Clamp01(wetness));
            properties.SetFloat(RimWidthId,Mathf.Clamp(rimWidth,0,.4f));properties.SetFloat(RimAngleId,rimAngle);
            properties.SetVector(AttractionId,new Vector4(AttractionOffset.x,AttractionOffset.y,AttractionOffset.z,0));
            var field=LogoIsEmbedded && logoField!=null?logoField.Update(logoGlyphDistanceFields,Mathf.Clamp(logoLetterSpacing,-.008f,.06f)):null;
            if(field!=null)properties.SetTexture(LogoFieldId,field);
            float width=Mathf.Clamp(Mathf.Clamp(logoArcWidth,1.5f,2.9f)*Mathf.Clamp(logoScale,.25f,1.3f),.375f,2.9f);
            float height=logoDistanceField!=null?width*logoDistanceField.height/logoDistanceField.width:.5f;
            var inputs=new Vector4(width,height,logoElevation,Mathf.Clamp(logoLetterSpacing,-.008f,.06f));
            if(!hasLogoRegion || !regionInputs.Equals(inputs))
            {
                logoRegion=FerrofluidLogoLayout.ConservativeRegion(inputs.x,inputs.y,inputs.z,inputs.w);
                regionInputs=inputs;hasLogoRegion=true;
            }
            properties.SetVector(LogoRegionId,logoRegion);
            properties.SetFloat(LogoEnabledId,field!=null?1:0);
            properties.SetFloat(LogoRevealId,LogoRevealProgress);
            properties.SetFloat(LogoFlowId,Mathf.Clamp01(logoSurfaceFlow));
            properties.SetFloat(LogoLipFlowId,Mathf.Clamp01(logoLipFlow));
            properties.SetFloat(LogoTypeFlowId,Mathf.Clamp01(logoTypeFlow));
            // Use the actual mesh, since changing resolution takes effect on enable.
            // A cropped mesh has fewer vertices at the same sampling density.
            // Do not infer resolution from its vertex count: that would change lettering.
            int meshResolution=bodyResolution;
            properties.SetFloat(LogoMeshGuardId,3.2f/letteringResolution);
            properties.SetFloat(LogoDetailId,4f*letteringResolution/meshResolution);
            properties.SetVector(LogoSettingsId,new Vector4(width,height,logoElevation,LogoIsEmbedded?Mathf.Clamp(logoRecess,0,.03f):0));
            properties.SetVector(LogoRightId,logoRight);properties.SetVector(LogoUpId,logoUp);properties.SetVector(LogoFrontId,logoFront);
            SurfaceRenderer.SetPropertyBlock(properties);
        }
        // Bounds of the radial field in AttractFerrofluidField.hlsl: each
        // gradient-noise corner is in [-2,2], and the positive mound term is
        // bounded by relief*.72. The letter floor can only lower the inner bound.
        // Rear points project inside this opaque inner sphere before being cut.
        public static float SafeRearCutoff(float relief,float recess,float typeFlow)
        {
            relief=Mathf.Clamp(relief,.03f,.16f);
            const float bodyMinimum=.455f-.018f*1.2f;
            float maximum=.455f+.018f*1.2f+relief*.72f;
            float floor=Mathf.Lerp(.455f+relief*.42f,bodyMinimum,Mathf.Clamp01(typeFlow))-Mathf.Clamp(recess,0,.03f);
            // Include triangle chord error (body resolution >=64) and a rim margin.
            float minimum=Mathf.Min(bodyMinimum,floor)*.995f;
            float ratio=minimum/maximum;
            return Mathf.Max(-1,-Mathf.Sqrt(1-ratio*ratio)-.025f);
        }
        void EnsureRearCoverage()
        {
            if(!RearGeometryTrimmed || mesh==null)return;
            float cutoff=SafeRearCutoff(surfaceRelief,logoRecess,logoTypeFlow);
            // A moved/rotated camera or sphere can expose the cut. Restore the
            // full shell once, rather than rebuilding a camera-facing mesh every frame.
            if(inputCamera==null || !inputCamera.orthographic ||
                Vector3.Dot(retainedFront,transform.InverseTransformVector(-inputCamera.transform.forward).normalized)<.999999f)
            { cutoff=-1;RearGeometryTrimmed=false; }
            if(cutoff>=retainedRearCutoff)return;
            var previous=mesh;
            mesh=BuildSphere(bodyResolution,retainedFront,cutoff);
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            retainedRearCutoff=cutoff;GeometryBuildCount++;
            Dispose(previous);
        }
        void OnDisable()
        {
            ReleaseSurface();
        }
        void ReleaseSurface()
        {
            if(logoField!=null){logoField.Dispose();logoField=null;}
            if(original!=null)original.enabled=originalEnabled;
            Dispose(visual);Dispose(mesh);Dispose(material);
            visual=null;mesh=null;material=null;SurfaceRenderer=null;properties=null;AnimationTime=0;
            original=null;RearGeometryTrimmed=false;
            logoArrivalElapsed=0;logoWasEmbedded=false;
            attraction.Reset();AttractionTarget=Vector2.zero;AttractionOffset=Vector3.zero;ActiveInputPlayers=0;
            playerSticks.Clear();horizontalAction=-1;verticalAction=-1;inputCamera=null;
#if UNITY_EDITOR
            PreviewInputProvider=null;
            PreviewArrivalDeltaTime=null;
#endif
        }
        static void Dispose(Object item){if(item==null)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}

        public static Mesh BuildSphere(int resolution)=>BuildSphere(resolution,Vector3.up,-1);

        public static Mesh BuildSphere(int resolution,Vector3 retainedFront,float rearCutoff)
        {
            resolution=Mathf.Clamp(resolution,8,192);
            int side=resolution+1;
            var vertices=new Vector3[6*side*side];var normals=new Vector3[vertices.Length];var indices=new int[36*resolution*resolution];
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            for(int face=0;face<6;face++)
            {
                Vector3 axis=axes[face],u=Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.Cross(Vector3.up,axis);
                Vector3 v=Vector3.Cross(axis,u);
                int first=face*side*side;
                for(int y=0;y<side;y++)for(int x=0;x<side;x++)
                {
                    int i=first+x+y*side;Vector3 n=(axis+u*(2f*x/resolution-1)+v*(2f*y/resolution-1)).normalized;
                    vertices[i]=n*.5f;normals[i]=n;
                    if(x==resolution || y==resolution)continue;
                    int t=(face*resolution*resolution+x+y*resolution)*6;
                    indices[t]=i;indices[t+1]=i+1;indices[t+2]=i+side;
                    indices[t+3]=i+1;indices[t+4]=i+side+1;indices[t+5]=i+side;
                }
            }
            if(rearCutoff>-1 && retainedFront.sqrMagnitude>.0001f)
            {
                retainedFront.Normalize();rearCutoff=Mathf.Clamp(rearCutoff,-1,0);
                var compactVertices=new List<Vector3>(vertices.Length);
                var compactNormals=new List<Vector3>(vertices.Length);
                var compactIndices=new List<int>(indices.Length);
                var remap=new int[vertices.Length];
                for(int i=0;i<remap.Length;i++)remap[i]=-1;
                for(int t=0;t<indices.Length;t+=3)
                {
                    // Keep boundary triangles whole, preserving positions, winding
                    // and shared tessellation edges on every retained triangle.
                    if(Vector3.Dot(normals[indices[t]],retainedFront)<rearCutoff &&
                       Vector3.Dot(normals[indices[t+1]],retainedFront)<rearCutoff &&
                       Vector3.Dot(normals[indices[t+2]],retainedFront)<rearCutoff)continue;
                    for(int corner=0;corner<3;corner++)
                    {
                        int old=indices[t+corner];
                        if(remap[old]<0)
                        { remap[old]=compactVertices.Count;compactVertices.Add(vertices[old]);compactNormals.Add(normals[old]); }
                        compactIndices.Add(remap[old]);
                    }
                }
                vertices=compactVertices.ToArray();normals=compactNormals.ToArray();indices=compactIndices.ToArray();
            }
            var result=new Mesh{name=rearCutoff>-1?"Ferrofluid shell with hidden rear trimmed":"Dense continuous ferrofluid shell",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};
            result.vertices=vertices;result.normals=normals;result.triangles=indices;
            // Includes every displacement within the exposed parameter ranges.
            result.bounds=new Bounds(Vector3.zero,Vector3.one*1.5f);
            result.UploadMeshData(false);return result;
        }
    }
}
