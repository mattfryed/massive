using TMPro;
using Rewired;
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
        [Header("Begin prompt")]
        [InspectorName("Prompt Size"),Min(1)] public float beginPromptSize=24;
        [InspectorName("Prompt Horizontal Position"),Tooltip("Screen position: 0 is left, 0.5 is centered, 1 is right."),Range(0,1)] public float beginPromptX=.5f;
        [InspectorName("Prompt Vertical Position"),Tooltip("Screen position: 0 is bottom, 1 is top. Independent of the player spheres."),Range(0,1)] public float beginPromptY=.315f;
        [InspectorName("Prompt Animation Prefab"),Tooltip("The begin text and its existing TMP Text Transition component. Open this prefab to edit SDF Grow, intro/outro durations and easing.")]
        public TextMeshProUGUI beginPromptPrefab;
        [Header("Player sphere surfaces")]
        [Range(5,14)] public float playerDensity=9;
        [Range(.03f,.16f)] public float playerRelief=.1f;
        [Range(0,1)] public float playerHighlightCoverage=.86f;
        [Tooltip("One is the original pattern size. Higher values make larger mounds; lower values make finer detail. Independent of the title sphere."),Range(.5f,2.5f)]
        public float whiteSphereTextureScale=1;
        [Tooltip("Independent pattern size for black player spheres."),Range(.5f,2.5f)] public float blackSphereTextureScale=1;
        [Header("Liquid formation")]
        [InspectorName("Fill / Drain Duration"),Tooltip("Seconds for fluid to emerge from a slot's glob, or retreat into it."),Range(.25f,3)] public float transitionTime=1.1f;
        [Tooltip("Diameter of an inactive glob relative to a full player sphere."),Range(.12f,.4f)] public float idleGlobSize=.24f;

        public bool IsAlternateActive=>presentation!=null && presentation.activeSelf;
        public bool HasBegun {get;private set;}
        public bool MenuInputReady {get;private set;}
        public int SelectedOption {get;private set;}
        public int PreviewSphereCount=>SelectedOption==0?1:SelectedOption==1?2:4;
        public Button GetOptionButton(int index)=>buttons!=null && index>=0 && index<buttons.Length?buttons[index]:null;
#if UNITY_EDITOR
        public float? PreviewDeltaTime {get;set;}
        public System.Func<bool> PreviewAnyButtonDown {get;set;}
        public System.Func<bool> PreviewAnyButtonHeld {get;set;}
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
        TextMeshProUGUI beginPrompt;
        TMPTextTransition beginTransition;
        bool beginRequested,promptIntroComplete,promptOutroStarted;
        readonly Transform[] spheres=new Transform[4];
        readonly Renderer[] renderers=new Renderer[4];
        readonly float[] reveal=new float[4];
        readonly Vector4[] drops=new Vector4[FerrofluidFormation.DropCount];
        bool legacyMenuState,legacyPlayersState;
        GameObject previousSelection,previousFirstSelection;
        EventSystem input;
        float clock;
        bool submitting;
        int createdFrame,beginFrame;
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
            if(input==null)
            {
                input=EventSystem.current;
                if(input!=null){previousSelection=input.currentSelectedGameObject;previousFirstSelection=input.firstSelectedGameObject;}
            }
            if(!HasBegun)
            {
                ClearMenuFocus();
                // Ignore a press inherited from the frame that created the scene.
                if(Time.frameCount>createdFrame && AnyButtonDown())beginRequested=true;
                // Finish an interrupted intro before reversing the existing
                // transition, which otherwise starts OUT from fully visible.
                if(beginRequested && promptIntroComplete && !promptOutroStarted)
                {
                    promptOutroStarted=true;
                    if(beginTransition!=null && beginTransition.isActiveAndEnabled)beginTransition.PlayOut();
                    else RevealMenu();
                }
                return;
            }
            if(!MenuInputReady)
            {
                ClearMenuFocus();
                // A held submit, mouse press, or second player's simultaneous
                // press must finish before the newly visible options can act.
                if(Time.frameCount<=beginFrame || AnyButtonHeld())return;
                MenuInputReady=true;
                for(int i=0;i<buttons.Length;i++)buttons[i].interactable=true;
                if(input!=null)input.firstSelectedGameObject=buttons[SelectedOption].gameObject;
            }
            // Also recover selection after an input module's first-frame setup.
            if(input!=null && (input.currentSelectedGameObject==null || IsLegacySelection(input.currentSelectedGameObject)))
                input.SetSelectedGameObject(buttons[SelectedOption].gameObject);
        }
        bool IsLegacySelection(GameObject obj)=>obj!=null && legacyModeSelect!=null && obj.transform.IsChildOf(legacyModeSelect.transform);
        bool AnyButtonDown()
        {
#if UNITY_EDITOR
            if(PreviewAnyButtonDown!=null)return PreviewAnyButtonDown();
#endif
            // Poll physical controller buttons, rather than treating joystick
            // axes (including drift) as a request to begin.
            return (ReInput.isReady && ReInput.controllers.GetAnyButtonDown()) || Input.anyKeyDown;
        }
        bool AnyButtonHeld()
        {
#if UNITY_EDITOR
            if(PreviewAnyButtonHeld!=null)return PreviewAnyButtonHeld();
#endif
            return (ReInput.isReady && ReInput.controllers.GetAnyButton()) || Input.anyKey;
        }
        void ClearMenuFocus()
        {
            if(input==null)return;
            input.firstSelectedGameObject=null;
            if(input.currentSelectedGameObject!=null)input.SetSelectedGameObject(null);
        }
        void RevealMenu()
        {
            if(presentation==null || !presentation.activeInHierarchy || HasBegun)return;
            HasBegun=true;beginFrame=Time.frameCount;
            beginPrompt.gameObject.SetActive(false);
            ApplySelection(1);
            for(int i=0;i<buttons.Length;i++)buttons[i].gameObject.SetActive(true);
            hint.gameObject.SetActive(true);
            // The existing reservoir animation begins at the small-glob state.
            // LateUpdate places and shades each slot before its first render.
        }
        void PromptIntroCompleted(){promptIntroComplete=true;}
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
                var go=new GameObject("Player "+(i+1)+" ferrofluid slot"){hideFlags=HideFlags.DontSave};
                go.transform.SetParent(presentation.transform,false);spheres[i]=go.transform;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderers[i]=renderer;reveal[i]=0;go.SetActive(false);
            }
            beginRequested=false;promptIntroComplete=false;promptOutroStarted=false;
            BuildLabels();
            submitting=false;HasBegun=false;MenuInputReady=false;createdFrame=Time.frameCount;
            ApplySelection(1);
            for(int i=0;i<buttons.Length;i++){buttons[i].interactable=false;buttons[i].gameObject.SetActive(false);}
            hint.gameObject.SetActive(false);ClearMenuFocus();
            canvas.gameObject.SetActive(true);
            // Auto-play begins when the canvas activates. Retain a manual
            // fallback for an animation prefab with auto-play disabled.
            if(beginTransition!=null && beginTransition.isActiveAndEnabled && !beginTransition.IsPlaying)beginTransition.PlayIn();
        }
        void BuildLabels()
        {
            var go=new GameObject("Mode labels",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            // Configure the prompt and listeners before its transition awakens.
            go.SetActive(false);
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
            beginPrompt=beginPromptPrefab!=null ? Instantiate(beginPromptPrefab,go.transform,false) : MakeText(go.transform,"Begin prompt","PRESS ANY BUTTON TO BEGIN",beginPromptSize);
            beginPrompt.name="Begin prompt";beginPrompt.text="PRESS ANY BUTTON TO BEGIN";beginPrompt.font=regularLabelFont;beginPrompt.raycastTarget=false;
            beginPrompt.gameObject.SetActive(true);
            beginTransition=beginPrompt.GetComponent<TMPTextTransition>();
            if(beginTransition!=null && beginTransition.enabled)
            {
                beginTransition.onInComplete.AddListener(PromptIntroCompleted);
                beginTransition.onOutComplete.AddListener(RevealMenu);
            }
            else promptIntroComplete=true;
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
            var pr=beginPrompt.rectTransform;float x=Mathf.Clamp01(beginPromptX),y=Mathf.Clamp01(beginPromptY);
            pr.anchorMin=new Vector2(x-.4f,y);pr.anchorMax=new Vector2(x+.4f,y);
            pr.anchoredPosition=Vector2.zero;pr.sizeDelta=new Vector2(0,Mathf.Max(54,beginPromptSize*1.5f));beginPrompt.fontSize=Mathf.Max(1,beginPromptSize);
        }
        public void SelectOption(int index)
        {
            if(!MenuInputReady)return;
            ApplySelection(index);
        }
        void ApplySelection(int index)
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
            if(submitting || !IsAlternateActive || !MenuInputReady || Time.frameCount<=beginFrame)return;
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
            if(!HasBegun)return;
            float diameter=Mathf.Clamp(sphereDiameter,.05f,.15f);
            Vector3 origin=menuCamera.ViewportToWorldPoint(new Vector3(.5f,spheresHeight,14));
            float worldDiameter=Vector3.Distance(origin,menuCamera.ViewportToWorldPoint(new Vector3(.5f,spheresHeight+diameter,14)));
            for(int i=0;i<4;i++)
            {
                // Player identity never changes: left to right is P2, P1, P3, P4.
                // Solo fills P1; versus fills P1/P3; team play fills all four.
                bool visible=SelectedOption==2 || i==0 || (SelectedOption==1 && i==2);
                reveal[i]=Mathf.MoveTowards(reveal[i],visible?1:0,FrameDelta/Mathf.Max(.25f,transitionTime));
                var sphere=spheres[i];
                sphere.gameObject.SetActive(true);
                float pitch=1+Mathf.Max(0,sphereSpacing),gap=Mathf.Max(0,teamSpacing);
                int order=i==0?1:i==1?0:i;
                float offset=(order-1.5f)*pitch+(order<2?-gap*.5f:gap*.5f);
                sphere.position=origin+menuCamera.transform.right*(offset*worldDiameter);
                sphere.localScale=Vector3.one*worldDiameter;
                bool white=i<2;
                Material activeMaterial=reveal[i]<1?formationMaterial:material;
                if(renderers[i].sharedMaterial!=activeMaterial)renderers[i].sharedMaterial=activeMaterial;
                properties.Clear();properties.SetFloat(WhiteId,white?1:0);
                float patternScale=white?whiteSphereTextureScale:blackSphereTextureScale;
                properties.SetFloat(TimeId,clock+i*2.71f);properties.SetFloat(DensityId,Mathf.Clamp(playerDensity,5,14)/Mathf.Clamp(patternScale,.5f,2.5f));properties.SetFloat(ReliefId,Mathf.Clamp(playerRelief,.03f,.16f));
                properties.SetFloat(WetnessId,Mathf.Clamp01(playerHighlightCoverage));properties.SetFloat(RimWidthId,sphereStyle.rimWidth);properties.SetFloat(RimAngleId,sphereStyle.rimAngle);
                Vector2 attraction=sphereStyle.SmoothedAttraction;
                Vector3 point=(menuCamera.transform.right*attraction.x+menuCamera.transform.up*attraction.y)*sphereStyle.attractionReach;
                properties.SetVector(AttractionId,point);
                if(reveal[i]<1)
                {
                    FerrofluidFormation.Evaluate(reveal[i],i,drops,idleGlobSize);
                    properties.SetVectorArray(DropsId,drops);properties.SetFloat(CoreId,FerrofluidFormation.Core(reveal[i],idleGlobSize));
                }
                renderers[i].SetPropertyBlock(properties);
            }
        }
        void OnDisable(){ReleasePresentation();}
        void ReleasePresentation()
        {
            if(presentation==null)return;
            bool ownsSelection=input!=null && (input.currentSelectedGameObject==null || input.currentSelectedGameObject.transform.IsChildOf(presentation.transform));
            if(ownsSelection)input.SetSelectedGameObject(null);
            // Hide immediately; generated resources are destroyed at frame end.
            if(beginTransition!=null)
            {
                beginTransition.onInComplete.RemoveListener(PromptIntroCompleted);
                beginTransition.onOutComplete.RemoveListener(RevealMenu);
            }
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
            presentation=null;canvas=null;mesh=null;material=null;formationMaterial=null;properties=null;buttons=null;labels=null;hint=null;beginPrompt=null;beginTransition=null;input=null;
            HasBegun=false;MenuInputReady=false;
        }
        static void Dispose(Object item){if(item==null)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
