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
        [Header("Embedded logo alternate")]
        public bool embeddedLogo;
        public Texture2D logoDistanceField;
        public Transform originalLogo;
        [Range(.25f,1.3f)] public float logoScale=1;
        [Tooltip("Extra space between letters, as a fraction of the original word width. Zero preserves the original spacing."),Range(-.008f,.06f)]
        public float logoLetterSpacing;
        [Range(1.5f,2.9f)] public float logoArcWidth=2.2f;
        [Range(-.4f,.4f)] public float logoElevation=.12f;
        [Range(0,.03f)] public float logoRecess=.016f;
        [Range(0,1)] public float logoSurfaceFlow=1;
        [Tooltip("How much the white letter floor follows the mounds. Zero keeps it stable; one follows the full surface motion."),Range(0,1)]
        public float logoTypeFlow=0;
        [Header("Lettering arrival")]
        public bool animateLogoArrival=true;
        [Min(0)] public float logoArrivalDelay=.25f;
        [Min(.1f)] public float logoArrivalDuration=3;
        public float LogoRevealProgress=>!animateLogoArrival?1:Mathf.Clamp01((logoArrivalElapsed-Mathf.Max(0,logoArrivalDelay))/Mathf.Max(.1f,logoArrivalDuration));
        public bool LogoIsEmbedded=>useFerrofluid && embeddedLogo && logoDistanceField!=null && originalLogo!=null;
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
        Material material;
        MaterialPropertyBlock properties;
        readonly FerrofluidAttraction attraction=new FerrofluidAttraction();
        readonly List<Vector2> playerSticks=new List<Vector2>(4);
        Camera inputCamera;
        int horizontalAction=-1,verticalAction=-1;
        Renderer[] logoRenderers;
        bool[] logoRendererStates;
        Vector3 logoRight,logoUp,logoFront;
        float logoArrivalElapsed;
        bool logoWasEmbedded;
        static readonly int TimeId=Shader.PropertyToID("_FluidTime"),DensityId=Shader.PropertyToID("_Density"),ReliefId=Shader.PropertyToID("_Relief"),WetnessId=Shader.PropertyToID("_Wetness");
        static readonly int RimWidthId=Shader.PropertyToID("_RimWidth"),RimAngleId=Shader.PropertyToID("_RimAngle");
        static readonly int AttractionId=Shader.PropertyToID("_Attraction");
        static readonly int LogoFieldId=Shader.PropertyToID("_LogoField"),LogoSettingsId=Shader.PropertyToID("_LogoSettings");
        static readonly int LogoEnabledId=Shader.PropertyToID("_LogoEnabled");
        static readonly int LogoSpacingId=Shader.PropertyToID("_LogoSpacing");
        static readonly int LogoRevealId=Shader.PropertyToID("_LogoReveal");
        static readonly int LogoFlowId=Shader.PropertyToID("_LogoFlow");
        static readonly int LogoTypeFlowId=Shader.PropertyToID("_LogoTypeFlow");
        static readonly int LogoMeshGuardId=Shader.PropertyToID("_LogoMeshGuard");
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
            AnimationTime=0;
            ReplayLogoArrival();logoWasEmbedded=LogoIsEmbedded;
            attraction.Reset();AttractionTarget=Vector2.zero;AttractionOffset=Vector3.zero;ActiveInputPlayers=0;
            inputCamera=attractionCamera!=null?attractionCamera:Camera.main;
            // Capture the orientation once: the imprint belongs to the surface,
            // rather than following the viewing camera like a billboard.
            logoRight=transform.InverseTransformDirection(inputCamera!=null?inputCamera.transform.right:Vector3.right);
            logoUp=transform.InverseTransformDirection(inputCamera!=null?inputCamera.transform.up:Vector3.forward);
            logoFront=transform.InverseTransformDirection(inputCamera!=null?-inputCamera.transform.forward:Vector3.up);
            mesh=BuildSphere(Mathf.Clamp(faceResolution,64,192));
            material=new Material(surfaceShader){name="Attract ferrofluid — transient",hideFlags=HideFlags.DontSave};
            visual=new GameObject("Ferrofluid surface — visual only"){hideFlags=HideFlags.DontSave,layer=gameObject.layer};
            visual.transform.SetParent(transform,false);
            // Keep the extra front-surface relief behind the world-space mode
            // labels. The source transform, colliders and particle origins stay put.
            visual.transform.localPosition=new Vector3(0,-.08f,0);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            SurfaceRenderer=visual.AddComponent<MeshRenderer>();SurfaceRenderer.sharedMaterial=material;
            SurfaceRenderer.shadowCastingMode=ShadowCastingMode.Off;SurfaceRenderer.receiveShadows=false;
            properties=new MaterialPropertyBlock();Apply();original.enabled=false;
            UpdateLogoVisibility();
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
            UpdateLogoVisibility();
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
            properties.SetFloat(TimeId,AnimationTime);properties.SetFloat(DensityId,Mathf.Clamp(surfaceDensity,5,14));
            properties.SetFloat(ReliefId,Mathf.Clamp(surfaceRelief,.03f,.16f));properties.SetFloat(WetnessId,Mathf.Clamp01(wetness));
            properties.SetFloat(RimWidthId,Mathf.Clamp(rimWidth,0,.4f));properties.SetFloat(RimAngleId,rimAngle);
            properties.SetVector(AttractionId,new Vector4(AttractionOffset.x,AttractionOffset.y,AttractionOffset.z,0));
            properties.SetTexture(LogoFieldId,logoDistanceField!=null?logoDistanceField:Texture2D.blackTexture);
            float width=Mathf.Clamp(Mathf.Clamp(logoArcWidth,1.5f,2.9f)*Mathf.Clamp(logoScale,.25f,1.3f),.375f,2.9f);
            float height=logoDistanceField!=null?width*logoDistanceField.height/logoDistanceField.width:.5f;
            properties.SetFloat(LogoEnabledId,LogoIsEmbedded?1:0);
            properties.SetFloat(LogoSpacingId,Mathf.Clamp(logoLetterSpacing,-.008f,.06f));
            properties.SetFloat(LogoRevealId,LogoRevealProgress);
            properties.SetFloat(LogoFlowId,Mathf.Clamp01(logoSurfaceFlow));
            properties.SetFloat(LogoTypeFlowId,Mathf.Clamp01(logoTypeFlow));
            // Use the actual mesh, since changing resolution takes effect on enable.
            int meshResolution=mesh!=null?Mathf.RoundToInt(Mathf.Sqrt(mesh.vertexCount/6f))-1:128;
            properties.SetFloat(LogoMeshGuardId,3.2f/meshResolution);
            properties.SetVector(LogoSettingsId,new Vector4(width,height,logoElevation,LogoIsEmbedded?Mathf.Clamp(logoRecess,0,.03f):0));
            properties.SetVector(LogoRightId,logoRight);properties.SetVector(LogoUpId,logoUp);properties.SetVector(LogoFrontId,logoFront);
            SurfaceRenderer.SetPropertyBlock(properties);
        }
        void UpdateLogoVisibility()
        {
            if(LogoIsEmbedded && logoRenderers==null)
            {
                // Only hide the original title's renderers, including its particle
                // letters. Its objects and behavior remain available for restoration.
                logoRenderers=originalLogo.GetComponentsInChildren<Renderer>(true);
                logoRendererStates=new bool[logoRenderers.Length];
                for(int i=0;i<logoRenderers.Length;i++){logoRendererStates[i]=logoRenderers[i].enabled;logoRenderers[i].enabled=false;}
            }
            else if(!LogoIsEmbedded)RestoreLogo();
        }
        void RestoreLogo()
        {
            if(logoRenderers==null)return;
            for(int i=0;i<logoRenderers.Length;i++)if(logoRenderers[i]!=null)logoRenderers[i].enabled=logoRendererStates[i];
            logoRenderers=null;logoRendererStates=null;
        }
        void OnDisable()
        {
            ReleaseSurface();
        }
        void ReleaseSurface()
        {
            RestoreLogo();
            if(original!=null)original.enabled=originalEnabled;
            Dispose(visual);Dispose(mesh);Dispose(material);
            visual=null;mesh=null;material=null;SurfaceRenderer=null;properties=null;AnimationTime=0;
            original=null;
            logoArrivalElapsed=0;logoWasEmbedded=false;
            attraction.Reset();AttractionTarget=Vector2.zero;AttractionOffset=Vector3.zero;ActiveInputPlayers=0;
            playerSticks.Clear();horizontalAction=-1;verticalAction=-1;inputCamera=null;
#if UNITY_EDITOR
            PreviewInputProvider=null;
            PreviewArrivalDeltaTime=null;
#endif
        }
        static void Dispose(Object item){if(item==null)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}

        public static Mesh BuildSphere(int resolution)
        {
            resolution=Mathf.Clamp(resolution,64,192);
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
            var result=new Mesh{name="Dense continuous ferrofluid shell",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};
            result.vertices=vertices;result.normals=normals;result.triangles=indices;
            // Includes every displacement within the exposed parameter ranges.
            result.bounds=new Bounds(Vector3.zero,Vector3.one*1.5f);
            result.UploadMeshData(false);return result;
        }
    }
}
