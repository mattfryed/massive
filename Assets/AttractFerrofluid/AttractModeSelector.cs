using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Massive.AttractStudy
{
    /// <summary>Presentation alternate for ATTRACT. Existing buttons still own
    /// the versus game routes; score attack is intentionally a menu preview.</summary>
    [DisallowMultipleComponent]
    public sealed class AttractModeSelector : MonoBehaviour
    {
        [Tooltip("Switch live between the horizontal ferrofluid menu and the original vertical menu.")]
        public bool useFerrofluidModeSelect=true;
        [Header("Existing scene bindings")]
        public GameObject legacyModeSelect;
        public GameObject legacyPseudoPlayers;
        public Button legacyOneVOne;
        public Button legacyTwoVTwo;
        public AttractFerrofluidStudy sphereStyle;
        public Camera menuCamera;
        public TMP_FontAsset labelFont;
        public TMP_FontAsset regularLabelFont;
        public Shader coalescenceShader;
        [Header("Layout")]
        [Range(.08f,.3f)] public float labelsHeight=.16f;
        [Range(.22f,.4f)] public float spheresHeight=.315f;
        [Range(.05f,.15f)] public float sphereDiameter=.112f;
        [Range(.14f,.3f)] public float optionSpacing=.22f;
        [Range(18,32)] public float labelSize=24;
        [Tooltip("Clear space between same-team spheres, measured in sphere diameters."),Range(0,2)] public float sphereSpacing=.16f;
        [Tooltip("Extra space between opposing teams, measured in sphere diameters."),Range(0,2)] public float teamSpacing=.30f;
        [Header("Player sphere surfaces")]
        [Tooltip("One is the original pattern size. Higher values make larger mounds; lower values make finer detail. Independent of the title sphere."),Range(.5f,2.5f)]
        public float whiteSphereTextureScale=1;
        [Tooltip("Independent pattern size for black player spheres."),Range(.5f,2.5f)] public float blackSphereTextureScale=1;
        [Header("Liquid formation")]
        [InspectorName("Coalescence Duration"),Tooltip("Seconds to gather the droplets into a sphere, or separate them on departure."),Range(.25f,3)] public float transitionTime=1.1f;

        public bool IsAlternateActive=>presentation!=null && presentation.activeSelf;
        public int SelectedOption {get;private set;}
        public int PreviewSphereCount=>SelectedOption==0?1:SelectedOption==1?2:4;
        public Button GetOptionButton(int index)=>buttons!=null && index>=0 && index<buttons.Length?buttons[index]:null;
#if UNITY_EDITOR
        public float? PreviewDeltaTime {get;set;}
#endif
        float FrameDelta
        {
            get
            {
#if UNITY_EDITOR
                if(PreviewDeltaTime.HasValue)return Mathf.Clamp(PreviewDeltaTime.Value,0,.05f);
#endif
                // Preserve the visible gathering motion after a shader warmup
                // or a stalled frame instead of skipping to a finished sphere.
                return Mathf.Min(Time.unscaledDeltaTime,.05f);
            }
        }

        GameObject presentation;
        Canvas canvas;
        Mesh mesh;
        Material material;
        Material formationMaterial;
        MaterialPropertyBlock properties;
        Button[] buttons;
        TextMeshProUGUI[] labels;
        TextMeshProUGUI hint;
        readonly Transform[] spheres=new Transform[4];
        readonly Renderer[] renderers=new Renderer[4];
        readonly float[] reveal=new float[4];
        readonly Vector4[] drops=new Vector4[FerrofluidFormation.DropCount];
        bool legacyMenuState,legacyPlayersState;
        GameObject previousSelection,previousFirstSelection;
        EventSystem input;
        float clock;
        bool submitting;
        static readonly string[] Labels={"MASSIVE\nSCORE ATTACK","1 vs 1","2 vs 2"};
        static readonly int TimeId=Shader.PropertyToID("_FluidTime"),DensityId=Shader.PropertyToID("_Density"),ReliefId=Shader.PropertyToID("_Relief"),WetnessId=Shader.PropertyToID("_Wetness"),RimWidthId=Shader.PropertyToID("_RimWidth"),RimAngleId=Shader.PropertyToID("_RimAngle"),AttractionId=Shader.PropertyToID("_Attraction"),WhiteId=Shader.PropertyToID("_WhiteDominant");
        static readonly int DropsId=Shader.PropertyToID("_FluidDrops"),CoreId=Shader.PropertyToID("_FormationCore");

        void OnEnable(){if(Application.isPlaying && useFerrofluidModeSelect)CreatePresentation();}
        void Update()
        {
            if(!useFerrofluidModeSelect){ReleasePresentation();return;}
            if(presentation==null)CreatePresentation();
            if(presentation==null)return;
            clock+=FrameDelta*Mathf.Max(0,sphereStyle.motionSpeed);
            if(input==null)input=EventSystem.current;
            // Also recover selection after an input module's first-frame setup.
            if(input!=null && (input.currentSelectedGameObject==null || IsLegacySelection(input.currentSelectedGameObject)))
                input.SetSelectedGameObject(buttons[SelectedOption].gameObject);
        }
        bool IsLegacySelection(GameObject obj)=>obj!=null && legacyModeSelect!=null && obj.transform.IsChildOf(legacyModeSelect.transform);
        void CreatePresentation()
        {
            if(presentation!=null)return;
            if(menuCamera==null)menuCamera=Camera.main;
            if(menuCamera==null || sphereStyle==null || sphereStyle.surfaceShader==null || labelFont==null || regularLabelFont==null || coalescenceShader==null || legacyModeSelect==null || legacyPseudoPlayers==null || legacyOneVOne==null || legacyTwoVTwo==null)
            {Debug.LogError("[Attract Mode Select] Assign the camera, sphere style, font and original menu references.",this);enabled=false;return;}
            // This controller must be outside the roots it hides.
            if(transform.IsChildOf(legacyModeSelect.transform) || transform.IsChildOf(legacyPseudoPlayers.transform))
            {Debug.LogError("[Attract Mode Select] Place this controller outside the legacy menu and pseudo-player roots.",this);enabled=false;return;}
            legacyMenuState=legacyModeSelect.activeSelf;legacyPlayersState=legacyPseudoPlayers.activeSelf;
            input=EventSystem.current;
            previousSelection=input!=null?input.currentSelectedGameObject:null;
            previousFirstSelection=input!=null?input.firstSelectedGameObject:null;
            if(input!=null && IsLegacySelection(previousSelection))input.SetSelectedGameObject(null);
            legacyModeSelect.SetActive(false);legacyPseudoPlayers.SetActive(false);
            presentation=new GameObject("Horizontal mode select — runtime"){hideFlags=HideFlags.DontSave};
            presentation.transform.SetParent(transform,false);
            mesh=AttractFerrofluidStudy.BuildSphere(64);
            material=new Material(sphereStyle.surfaceShader){name="Mode preview ferrofluid — transient",hideFlags=HideFlags.DontSave};
            formationMaterial=new Material(coalescenceShader){name="Mode preview liquid formation — transient",hideFlags=HideFlags.DontSave};
            properties=new MaterialPropertyBlock();
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Mode preview sphere "+(i+1)){hideFlags=HideFlags.DontSave};
                go.transform.SetParent(presentation.transform,false);spheres[i]=go.transform;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderers[i]=renderer;reveal[i]=0;go.SetActive(false);
            }
            BuildLabels();
            submitting=false;SelectedOption=Mathf.Clamp(SelectedOption,0,2);
            SelectOption(SelectedOption);
            if(input!=null)
            {
                input.firstSelectedGameObject=buttons[SelectedOption].gameObject;
                input.SetSelectedGameObject(buttons[SelectedOption].gameObject);
            }
        }
        void BuildLabels()
        {
            var go=new GameObject("Mode labels",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(presentation.transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=menuCamera;canvas.planeDistance=10;canvas.sortingOrder=50;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            buttons=new Button[3];labels=new TextMeshProUGUI[3];
            for(int i=0;i<3;i++)
            {
                var option=new GameObject(Labels[i].Replace('\n',' '),typeof(RectTransform),typeof(Button));
                option.transform.SetParent(go.transform,false);
                var button=option.GetComponent<Button>();button.transition=Selectable.Transition.None;buttons[i]=button;
                int index=i;button.onClick.AddListener(()=>Submit(index));
                var selection=option.AddComponent<AttractModeOption>();selection.owner=this;selection.index=i;
                var text=MakeText(option.transform,"Label",Labels[i],labelSize);
                labels[i]=text;
                Stretch(text.rectTransform);text.margin=new Vector4(8,4,8,4);
                text.raycastTarget=true;button.targetGraphic=text;
            }
            for(int i=0;i<3;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=buttons[(i+2)%3],selectOnRight=buttons[(i+1)%3]};
            hint=MakeText(go.transform,"Mode status","PREVIEW",13);hint.raycastTarget=false;
            LayoutLabels();
        }
        TextMeshProUGUI MakeText(Transform parent,string name,string value,float size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>();text.font=regularLabelFont;text.fontStyle=FontStyles.Normal;text.text=value;text.fontSize=size;text.color=Color.white;
            text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;
            return text;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void LayoutLabels()
        {
            for(int i=0;i<3;i++)
            {
                var rect=(RectTransform)buttons[i].transform;float center=.5f+(i-1)*optionSpacing;
                rect.anchorMin=new Vector2(center-optionSpacing*.46f,labelsHeight);rect.anchorMax=new Vector2(center+optionSpacing*.46f,labelsHeight);
                rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(0,68);
                labels[i].fontSize=labelSize;
            }
            var hr=hint.rectTransform;hr.anchorMin=hr.anchorMax=new Vector2(.5f,labelsHeight);hr.anchoredPosition=new Vector2(0,-62);hr.sizeDelta=new Vector2(340,22);
        }
        public void SelectOption(int index)
        {
            if(buttons==null || index<0 || index>2)return;
            SelectedOption=index;
            // Use the real bold and regular faces, rather than marking an
            // already-bold font as "normal" and leaving it visually unchanged.
            for(int i=0;i<3;i++)labels[i].font=i==index?labelFont:regularLabelFont;
            hint.text=index==0?"SCORE ATTACK PREVIEW":"";
        }
        void Submit(int index)
        {
            if(submitting || !IsAlternateActive)return;
            SelectOption(index);
            if(index==0)return; // Deliberately no scene load or game-mode mutation.
            submitting=true;
            // Preserve the serialized route, including any future scene setup
            // attached to the user's existing versus buttons.
            (index==1?legacyOneVOne:legacyTwoVTwo).onClick.Invoke();
        }
        void LateUpdate()
        {
            if(presentation==null || menuCamera==null)return;
            LayoutLabels();
            float diameter=Mathf.Clamp(sphereDiameter,.05f,.15f);
            Vector3 origin=menuCamera.ViewportToWorldPoint(new Vector3(.5f,spheresHeight,14));
            float worldDiameter=Vector3.Distance(origin,menuCamera.ViewportToWorldPoint(new Vector3(.5f,spheresHeight+diameter,14)));
            float blend=1-Mathf.Exp(-FrameDelta*8);
            for(int i=0;i<4;i++)
            {
                // Stable slots: white A, black A, white B, black B. Removing
                // partners never recolors an existing team member mid-animation.
                bool visible=i<PreviewSphereCount;bool newlyVisible=reveal[i]==0 && visible;
                reveal[i]=Mathf.MoveTowards(reveal[i],visible?1:0,FrameDelta/Mathf.Max(.25f,transitionTime));
                var sphere=spheres[i];sphere.gameObject.SetActive(reveal[i]>0);
                float pitch=1+Mathf.Max(0,sphereSpacing),gap=Mathf.Max(0,teamSpacing);
                int order=i==1?2:i==2?1:i;
                float offset=PreviewSphereCount==1?0:PreviewSphereCount==2?(i==0?-1:1)*(pitch+gap)*.5f:(order-1.5f)*pitch+(order<2?-gap*.5f:gap*.5f);
                Vector3 target=origin+menuCamera.transform.right*(offset*worldDiameter);
                if(visible)sphere.position=newlyVisible?target:Vector3.Lerp(sphere.position,target,blend);
                sphere.localScale=Vector3.one*worldDiameter;
                if(reveal[i]==0)continue;
                bool white=(i&1)==0;
                Material activeMaterial=reveal[i]<1?formationMaterial:material;
                if(renderers[i].sharedMaterial!=activeMaterial)renderers[i].sharedMaterial=activeMaterial;
                properties.Clear();properties.SetFloat(WhiteId,white?1:0);
                float patternScale=white?whiteSphereTextureScale:blackSphereTextureScale;
                properties.SetFloat(TimeId,clock+i*2.71f);properties.SetFloat(DensityId,9/Mathf.Clamp(patternScale,.5f,2.5f));properties.SetFloat(ReliefId,sphereStyle.surfaceRelief);
                properties.SetFloat(WetnessId,sphereStyle.wetness);properties.SetFloat(RimWidthId,sphereStyle.rimWidth);properties.SetFloat(RimAngleId,sphereStyle.rimAngle);
                Vector2 attraction=sphereStyle.SmoothedAttraction;
                Vector3 point=(menuCamera.transform.right*attraction.x+menuCamera.transform.up*attraction.y)*sphereStyle.attractionReach;
                properties.SetVector(AttractionId,point);
                if(reveal[i]<1)
                {
                    FerrofluidFormation.Evaluate(reveal[i],i,drops);
                    properties.SetVectorArray(DropsId,drops);properties.SetFloat(CoreId,FerrofluidFormation.Core(reveal[i]));
                }
                renderers[i].SetPropertyBlock(properties);
            }
        }
        void OnDisable(){ReleasePresentation();}
        void ReleasePresentation()
        {
            if(presentation==null)return;
            bool ownsSelection=input!=null && input.currentSelectedGameObject!=null && input.currentSelectedGameObject.transform.IsChildOf(presentation.transform);
            if(ownsSelection)input.SetSelectedGameObject(null);
            // Hide immediately; generated resources are destroyed at frame end.
            presentation.SetActive(false);
            if(legacyModeSelect!=null)legacyModeSelect.SetActive(legacyMenuState);
            if(legacyPseudoPlayers!=null)legacyPseudoPlayers.SetActive(legacyPlayersState);
            if(input!=null)
            {
                input.firstSelectedGameObject=previousFirstSelection;
                if(ownsSelection)
                {
                    var restore=previousSelection!=null && previousSelection.activeInHierarchy?previousSelection:previousFirstSelection;
                    if(restore!=null && restore.activeInHierarchy)input.SetSelectedGameObject(restore);
                }
            }
            Dispose(presentation);Dispose(mesh);Dispose(material);Dispose(formationMaterial);
            presentation=null;canvas=null;mesh=null;material=null;formationMaterial=null;properties=null;buttons=null;labels=null;hint=null;input=null;
        }
        static void Dispose(Object item){if(item==null)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
