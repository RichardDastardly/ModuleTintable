using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Tinter
{
    public static class TDebug
    {
        // debugging stuff from the start! how novel
        public static string dbgTag = "[Tinter] ";

        public static void Print(string dbgString)
        {
            Debug.Log(dbgTag + dbgString);
        }

        public static void Warn(string dbgString)
        {
            Debug.LogWarning(dbgTag + dbgString);
        }

        public static void Err(string dbgString)
        {
            Debug.LogError(dbgTag + dbgString);
        }
    }

    public class Tinter : PartModule
    {

        private bool active = false;
        private bool needUpdate = false;
        private bool needShaderReplacement = true;

        private List<Material> ManagedMaterials = new List<Material>();

        private void EditorPartEvent( ConstructionEventType cEvent, Part part )
        {
 //           if (cEvent != ConstructionEventType.PartTweaked) return;
            TDebug.Print(part.name + "  event "+ cEvent.ToString());
        }

        private void EditorShipEvent( ShipConstruct cEvent )
        {
            TDebug.Print(part.name + " ship event "+ cEvent.ToString());
        }

        private void onTweakableChange(BaseField field, object what )
        {
            needUpdate = true;
 //          UpdateShaderValues();
//            TDebug.Print(part.name + " tweakable slider changed");
        }

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Value"),
            UI_FloatRange(
                affectSymCounterparts = UI_Scene.Editor,
                minValue = 0,
                maxValue = 255,
                stepIncrement = 1,
                scene = UI_Scene.Editor
        )]
        public float tintBaseTexVal = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Band"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexVBand = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Falloff"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexVFalloff = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Saturation Threshold"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexSatThreshold = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Hue"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintHue = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Saturation"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintSaturation = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Value"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintValue = 0;

        private void ToggleFields( bool flag )
        {
            for ( int i = 0; i< Fields.Count; i++ )
            {
                var uiField = Fields[i].uiControlEditor as UI_FloatRange; // good grief this looks terrible, surely must be a better way?
                if ( uiField != null ) 
                {
                    Fields[i].guiActiveEditor = flag;
                }
            }
        }

        private void TweakFieldSetup()
        {
            for( int i = 0; i < Fields.Count; i++ )
            {
                var uiField = Fields[i].uiControlEditor as UI_FloatRange;
                if (uiField != null) // all parts with ui components are currently sliders we want to trap changes for
                {
  //                  TDebug.Print("Setting onFieldChange for " + Fields[i].guiName);
                    uiField.onFieldChanged = onTweakableChange;
                }
            }
        }

        private void TraverseAndReplaceShaders()
        {
            if (!AssetLoader.shadersLoaded)
                return;

            if (!needShaderReplacement)
            {
                TDebug.Print("Apparently " + part.name + " doesn't need shader replacement");
                return;
            }

            var Materials = new List<Material>();
            
            // messy messy, tidy
            foreach ( Renderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                Materials.AddRange(r.materials);
            }
            foreach( Material m in Materials.ToArray())
            {
                TDebug.Print(part.name + " material " + m.name);
                var replacementShader = AssetLoader.FetchRepacementShader(m.shader.name);
                if (replacementShader)
                {
                    active = true;
                    TDebug.Print(part.name+ " Replacing shader " + m.shader.name + " with " + replacementShader.name);
                    m.shader = replacementShader;
                    ManagedMaterials.Add(m);
                    needUpdate = true;
                }
            }
            ToggleFields(active);
            needShaderReplacement = false;
        }

  //      float _TintHue;
  //      float _TintSat;
   //     float _TintVal;
   //     float _TintPoint;
   //     float _TintBand;
   //     float _TintFalloff;
   //     float _TintSatThreshold;

        private float SliderToShaderValue( float v )
        {
            return v / 255;
        }

        private void UpdateShaderValues()
        {
            foreach ( Material m in ManagedMaterials.ToArray())
            {
                TDebug.Print(part.name + "Updating material " + m.name);
                m.SetFloat("_TintPoint", SliderToShaderValue(tintBaseTexVal));
                m.SetFloat("_TintBand", SliderToShaderValue(tintBaseTexVBand));
                m.SetFloat("_TintFalloff", SliderToShaderValue(tintBaseTexVFalloff));
                m.SetFloat("_TintHue", SliderToShaderValue(tintHue));
                m.SetFloat("_TintSat", SliderToShaderValue(tintSaturation));
                m.SetFloat("_TintVal", SliderToShaderValue(tintValue));
                m.SetFloat("_TintSatThreshold", SliderToShaderValue(tintBaseTexSatThreshold));
            }
        }

        public void Update()
        {
            if( needUpdate )
            {
                needUpdate = false;
  //              TDebug.Print("Update() - updating shader values");
                UpdateShaderValues();
                // update shader values
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            TweakFieldSetup();
            TraverseAndReplaceShaders();
            TDebug.Print("OnStart() [" + part.name + "]" );
        }

        public void Start()
        {
            this.part.OnEditorAttach += new Callback(OnEditorAttach);
  //          GameEvents.onEditorPartEvent.Add(EditorPartEvent);
  //          GameEvents.onEditorShipModified.Add(EditorShipEvent);
            TDebug.Print("Start() [" + this.part.name + "]");
        }

        private void OnDestroy()
        {
  //          GameEvents.onEditorPartEvent.Remove(EditorPartEvent);
 //           GameEvents.onEditorShipModified.Remove(EditorShipEvent);
        }


        public override void OnUpdate()
        {
            if( needUpdate )
            {
                needUpdate = false;
                TDebug.Print("OnUpdate(): needUpdate set " + this.part.name);
            }
        }

        public void OnEditorAttach()
        {
            TDebug.Print("Editor - attach [" + this.part.name + "]");
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            TDebug.Print("OnSave() [" + this.part.name + "]");
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            TDebug.Print("OnLoad() [" + this.part.name + "]");
            TraverseAndReplaceShaders();
        }

        public void Setup()
        {
            TDebug.Print("Setup() [" + this.part.name + "]");
        }
     }
}
