using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    /// <summary>Dependency-free targeted checks; uses a separate physics scene and never advances the match.</summary>
    public static class ResonanceValidation
    {
        [MenuItem("MASSIVE/Resonance/Run Prototype Checks")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            var def = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root = null;
            try
            {
                def.Set345HzDefaults();
                Check(def.arcs.Count == 8, "sparse eight-arc preset", passed);
                Check(Mathf.Abs(def.arcs[1].startDegrees + def.arcs[1].sweepDegrees * .5f) < .001f, "outer hard arc starts at concave east section", passed);
                Check(Vector3.Distance(def.curves[1].Evaluate(45), def.curves[1].Evaluate(225) * -1f) < 0.0001f, "opposing lobe symmetry", passed);
                var arc = def.arcs[0];
                Check(arc.Envelope(0) == 0f && arc.Envelope(1) == 0f && arc.Envelope(.5f) == 1f, "taper ends and plateau", passed);
                Check(Vector3.Distance(def.curves[0].Evaluate(-10), def.curves[0].Evaluate(350)) < 0.0001f, "wrapped angles", passed);
                root = new GameObject("Test pattern"); SceneManager.MoveGameObjectToScene(root, scene);
                var controller = root.AddComponent<ResonancePatternController>();
                controller.definition = def;
                controller.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>(ResonancePrototypeSetup.Folder + "/Resonance Ribbon.mat");
                controller.patternScale = Vector2.one;
                var original = def.arcs[0]; float savedThickness = original.thickness;
                controller.globalVisualProfile.enabled = true;
                controller.globalVisualProfile.thickness = .7f;
                controller.globalVisualProfile.taperLength = 1f;
                controller.globalVisualProfile.falloffShape = ResonanceTaperShape.Linear;
                var resolved = controller.ResolveArc(original, 10f);
                Check(resolved.thickness == .7f && Mathf.Abs(resolved.endTaperFraction-.1f) < .0001f, "global visual width and length override", passed);
                Check(original.thickness == savedThickness && original.taperLength == 0f, "global override never mutates definition", passed);
                Check(Mathf.Abs(resolved.Envelope(.05f)-.5f) < .001f, "linear taper option", passed);
                controller.globalVisualProfile.enabled = false;
                Check(controller.ResolveArc(original,10f).thickness == savedThickness, "disabling global profile restores authored value", passed);
                controller.globalCollisionProfile.enabled = true;
                controller.globalCollisionProfile.thickness = .12f;
                controller.overrideCollisionExtent = true; controller.globalCollisionStart = .25f; controller.globalCollisionEnd = .75f;
                controller.globalCollisionProfile.taperLength = 1f;
                resolved = controller.ResolveArc(original,10f);
                Check(resolved.colliderThickness == .12f && resolved.CollisionEnvelope(.2f) == 0f && Mathf.Abs(resolved.collisionTaperFraction-.2f)<.001f,
                    "global contact extent and fitted-length taper independent from visual", passed);
                controller.globalCollisionProfile.enabled = false; controller.overrideCollisionExtent = false;
                def.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = -20, sweepDegrees = 40, thickness = .3f, colliderThickness = .3f } };
                arc = def.arcs[0]; controller.Rebuild(); Physics.SyncTransforms();
                var physics = scene.GetPhysicsScene(); RaycastHit hit;
                Check(physics.Raycast(Vector3.zero, Vector3.right, out hit, 3f), "exposed arc blocks a ray", passed);
                Check(!physics.Raycast(Vector3.zero, Vector3.forward, out hit, 3f), "unexposed arc remains open", passed);
                int contacts = root.GetComponentsInChildren<Collider>().Length;
                var shared = controller.segmentMaterial;
                var generated = root.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Check(generated != shared && generated.hideFlags == HideFlags.DontSave, "blend settings use owned transient material", passed);
                var coverage = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(coverage.vertexCount==4 && coverage.triangles.Length==6, "distance-field coverage has no folded arc strips", passed);
                var block=new MaterialPropertyBlock();
                root.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
                Check(block.GetInt("_CurveCount")>=16 && block.GetInt("_CurveCount")<=128 && block.GetVectorArray("_CurvePoints").Length==128,
                    "continuous curve data is bounded and bound to the renderer",passed);
                Color savedFilament=controller.filament.color; controller.filament.color=Color.red; controller.PreviewImpact();
                root.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
                Check(block.GetColor("_FilamentColor")==Color.red && block.GetColor("_RibbonColor")==controller.ribbon.color,
                    "global layer colors are independent",passed);
                controller.filament.color=savedFilament;
                Check(controller.GridSampleCount > 0 && controller.GridSampleCount <= controller.gridSampleBudget, "bounded grid sampling", passed);
                for (int mode=0; mode<5; mode++)
                {
                    controller.blending=(ResonanceBlend)mode; controller.Rebuild();
                    Check(root.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetFloat("_BlendStyle")==mode,
                        "blend mode " + ((ResonanceBlend)mode), passed);
                }
                controller.blending=ResonanceBlend.Alpha;
                controller.diffuse.width = 5f; controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == contacts, "diffuse width never expands collision topology", passed);
                controller.diffuse.width = 2.8f;
                var ghost = controller.GeneratedRoot.transform.Find("Full pattern — impact reveal (visual only)");
                Check(ghost != null && ghost.GetComponentsInChildren<Collider>().Length == 0, "full pattern is a separate non-colliding layer", passed);
                controller.PreviewImpact();
                Check(controller.CurrentReveal > .9f && root.GetComponentsInChildren<Collider>().Length == contacts, "reveal leaves collision topology unchanged", passed);
                arc.independentCollisionProfile = true; arc.collisionStart = .2f; arc.collisionEnd = .8f; arc.collisionTaperFraction = .1f;
                float visible = arc.Envelope(.15f);
                Check(visible > 0f && arc.CollisionEnvelope(.15f) == 0f && arc.CollisionEnvelope(.5f) == 1f, "collision extent independent from visual extent", passed);
                arc.collisionFalloffPower = 3f;
                Check(arc.Envelope(.15f) == visible, "collision taper does not change visual taper", passed);
                arc.independentCollisionProfile = false;
                controller.layoutFit = ResonanceLayoutFit.RoundedRectangle; controller.patternScale = new Vector2(3.24f, 1.32f);
                Vector3 p = controller.Evaluate(def.curves[1], 20f), opposite = controller.Evaluate(def.curves[1], 200f);
                Check(Vector3.Distance(p, -opposite) < .001f, "rectangle fit preserves opposing symmetry", passed);
                Check(Vector3.Distance(controller.Evaluate(def.curves[1], -1f), controller.Evaluate(def.curves[1], 359f)) < .001f, "rectangle fit wraps without seam", passed);
                for (int degree = 0; degree < 360; degree++)
                {
                    Vector3 v = controller.Evaluate(def.curves[1], degree);
                    if (float.IsNaN(v.x) || float.IsNaN(v.z)) throw new InvalidOperationException("Non-finite fitted curve");
                }
                passed.Add("rectangle fit remains finite around full contour");
                controller.layoutFit = ResonanceLayoutFit.Stretch; controller.patternScale = Vector2.one;
                controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == contacts && root.transform.childCount == 1, "rebuild does not duplicate contacts", passed);
                arc.opacity = 0; controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == 0, "zero opacity removes contacts", passed);
                arc.opacity = 1; arc.intensity = 0; controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == 0, "zero intensity removes contacts", passed);
                arc.intensity = 1; arc.exposed = false; controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == 0, "hidden arc removes contacts", passed);
                arc.exposed = true; arc.sweepDegrees = 0; controller.Rebuild();
                Check(root.GetComponentsInChildren<Collider>().Length == 0, "zero sweep removes contacts", passed);
                arc.sweepDegrees = 360; controller.Rebuild(); Physics.SyncTransforms();
                Check(arc.Envelope(0) == 1 && physics.Raycast(Vector3.zero, Vector3.right, out hit, 3f), "full circle closes its seam", passed);
                arc.sweepDegrees = 40; arc.behavior = ResonanceBehavior.DampingMembrane; controller.Rebuild();
                Check(Array.TrueForAll(root.GetComponentsInChildren<Collider>(), c => c.isTrigger), "membranes generate only triggers", passed);
                Vector3 velocity = new Vector3(8, 2, 3);
                Vector3 once = ResonanceSegment.ModifyVelocity(velocity, Vector3.forward, arc, 1, 1);
                Vector3 stepped = velocity;
                for (int i = 0; i < 50; i++) stepped = ResonanceSegment.ModifyVelocity(stepped, Vector3.forward, arc, 1, .02f);
                Check(Vector3.Distance(once, stepped) < .0001f && once.y == 2 && once.x < velocity.x, "damping is timestep-independent and preserves vertical velocity", passed);
                arc.behavior = ResonanceBehavior.LensDeflector;
                Vector3 turn = ResonanceSegment.ModifyVelocity(velocity, Vector3.forward, arc, 1, .1f);
                Check(Mathf.Abs(turn.magnitude - velocity.magnitude) < .0001f && turn.z > velocity.z && turn.y == velocity.y, "lens turns without adding speed", passed);
                controller.enabled = false;
                Check(root.GetComponentsInChildren<Collider>().Length == 0, "disable removes contacts", passed);
                controller.enabled = true; arc.behavior = ResonanceBehavior.HardWall; controller.Rebuild(); Physics.SyncTransforms();
                Check(root.GetComponentsInChildren<Collider>().Length == contacts, "reenable restores contacts", passed);
                Vector3 incoming = new Vector3(-8f, 2f, 3f);
                Vector3 slowed = ResonanceInteractionDriver.MagneticVelocity(incoming,Vector3.right,.1f,.2f,.3f,.9f);
                Vector3 stopped = ResonanceInteractionDriver.MagneticVelocity(incoming,Vector3.right,.2f,.2f,.3f,.9f);
                Vector3 reflected = ResonanceInteractionDriver.MagneticVelocity(incoming,Vector3.right,.5f,.2f,.3f,.9f);
                Check(Mathf.Abs(slowed.x+4f)<.001f && slowed.y==2f, "magnetic braking slows whole planar velocity", passed);
                Check(stopped.x==0f && stopped.z==0f && stopped.y==2f, "magnetic transition reaches a planar stop", passed);
                Check(Vector3.Distance(reflected,new Vector3(7.2f,2f,2.7f))<.001f, "magnetic release follows reflected angle and retention", passed);
                Vector3 glide=ResonanceInteractionDriver.MagneticGlideVelocity(incoming,Vector3.right,.2f,.2f,.3f,1f,1f);
                Check(Mathf.Abs(glide.x)<.001f && glide.z==3f && glide.y==2f, "curved glide retains tangent motion at closest approach",passed);
                Vector3 quarter=ResonanceInteractionDriver.MagneticGlideVelocity(incoming,Vector3.right,.1f,.2f,.3f,1f,1f);
                Check(Mathf.Abs(quarter.x+4f)<.001f && quarter.z==3f, "constant normal deceleration produces a parabolic approach",passed);
                Vector3 mirror=ResonanceInteractionDriver.MagneticGlideVelocity(new Vector3(8f,2f,3f),Vector3.left,.1f,.2f,.3f,1f,1f);
                Check(Vector3.Distance(mirror,new Vector3(-quarter.x,quarter.y,quarter.z))<.001f,"curved response mirrors on opposite side",passed);
                var testPlayer = root.AddComponent<PlayerControllerScript>();
                testPlayer.SetMovementInfluence(controller,.3f); testPlayer.SetMovementInfluence(def,.6f);
                Check(testPlayer.ExternalMovementMultiplier==.3f, "overlapping movement slows choose strongest", passed);
                testPlayer.RemoveMovementInfluence(controller);
                Check(testPlayer.ExternalMovementMultiplier==.6f, "removing one slow preserves another source", passed);
                testPlayer.RemoveMovementInfluence(def);
                Check(testPlayer.ExternalMovementMultiplier==1f, "movement influence removal restores baseline", passed);
                UnityEngine.Object.DestroyImmediate(testPlayer);
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); SceneManager.MoveGameObjectToScene(ball, scene);
                ball.transform.position = Vector3.zero; ball.transform.localScale = Vector3.one * .2f;
                var body = ball.AddComponent<Rigidbody>(); body.useGravity = false; body.linearVelocity = Vector3.right * 20;
                Check(!controller.NotifyCoreImpact(body), "ordinary body does not reveal full pattern", passed);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Physics.SyncTransforms();
                for (int i = 0; i < 50; i++) physics.Simulate(.01f);
                Check(body.position.x < 1.5f, "moving rigidbody cannot cross hard arc at 20 units/sec", passed);
                Vector3 legacyLeft=ProbeGridCap(false,false), legacyRight=ProbeGridCap(false,true);
                Check(legacyLeft.x<0 && legacyRight.x>0,"original first-force cap reproduction remains isolated to legacy policy",passed);
                Vector3 balancedLeft=ProbeGridCap(true,false), balancedRight=ProbeGridCap(true,true);
                Check(balancedLeft.magnitude<.00001f && balancedRight.magnitude<.00001f,"GPU balanced opposing attractions cancel in either order",passed);
                ResonanceGridFalloffValidation.AppendChecks(passed);
                ResonanceContactVisualValidation.AppendChecks(passed);
                return passed.Count + " checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(def);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Check(bool condition, string name, List<string> passed)
        { if (!condition) throw new InvalidOperationException("Resonance check failed: " + name); passed.Add(name); }

        private static Vector3 ProbeGridCap(bool balanced, bool reverse)
        {
            var shader=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VectorGridNu/GridUnlitLines.compute"));
            var orig=new ComputeBuffer(9,12); var pos=new ComputeBuffer(9,12); var vel=new ComputeBuffer(9,12);
            var forces=new ComputeBuffer(2,System.Runtime.InteropServices.Marshal.SizeOf(typeof(VectorGridGPU.Force)));
            try
            {
                int k=shader.FindKernel("CSMain"); var points=new Vector3[9];
                for(int i=0;i<9;i++) points[i]=new Vector3(i%3-1,i/3-1,0);
                orig.SetData(points); pos.SetData(points); vel.SetData(new Vector3[9]);
                shader.SetBuffer(k,"_OrigPos",orig); shader.SetBuffer(k,"_Pos",pos); shader.SetBuffer(k,"_Vel",vel); shader.SetBuffer(k,"_Forces",forces);
                shader.SetInt("_Count",9); shader.SetInt("_ForceCount",2); shader.SetInt("_GridX",3); shader.SetInt("_GridY",3);
                shader.SetInt("_PinEdges",1); shader.SetInt("_FeatherCells",0); shader.SetInt("_FeatherUseExp",0); shader.SetInt("_ProjectAtEdge",0);
                shader.SetInt("_FalloffMode",1); shader.SetFloat("_DeltaTime",.02f); shader.SetFloat("_Kspring",0); shader.SetFloat("_Damping",0);
                shader.SetFloat("_WeightCap",.8f); shader.SetFloat("_CrowdStiffness",0); shader.SetFloat("_FalloffExp",1);
                shader.SetFloat("_InnerFrac",0); shader.SetFloat("_Sharpness",2); shader.SetFloat("_MaxSpeed",0);
                shader.SetFloat("_FeatherSharp",1); shader.SetFloat("_EdgeSpringMul",1); shader.SetFloat("_EdgeDampMul",1); shader.SetFloat("_EdgeForceMin",0);
                var a=balanced ? VectorGridGPU.MakeBalancedRadial(Vector3.left*.5f,2,1) : VectorGridGPU.MakeRadial(Vector3.left*.5f,2,1);
                var b=balanced ? VectorGridGPU.MakeBalancedRadial(Vector3.right*.5f,2,1) : VectorGridGPU.MakeRadial(Vector3.right*.5f,2,1);
                forces.SetData(reverse ? new[]{b,a} : new[]{a,b}); shader.Dispatch(k,1,1,1);
                var output=new Vector3[9]; vel.GetData(output); return output[4];
            }
            finally { orig.Release(); pos.Release(); vel.Release(); forces.Release(); UnityEngine.Object.DestroyImmediate(shader); }
        }
    }
}
