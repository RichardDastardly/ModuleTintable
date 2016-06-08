using System.Collections.Generic;
using UnityEngine;

// please bear in mind I don't know any better yet.

namespace Tinter
{
    #region TDebug
    public static class TDebug
    {
        // debugging stuff from the start! how novel
        // dump this when we're done
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
    #endregion

    #region ColourSet
    public class ColourSet
    {
        private Dictionary<string, float> Settings = new Dictionary<string, float>();

        private void InitialiseSettings()
        {
            Settings["BlendPoint"] = 0f;
            Settings["BlendBand"] = 0f;
            Settings["BlendFalloff"] = 0f;
            Settings["BlendSaturationThreshold"] = 0f;
            Settings["Hue"] = 0f;
            Settings["Saturation"] = 0f;
            Settings["Value"] = 0f;
            Settings["Glossiness"] = 0f;
            Settings["Specular"] = 0f;
            // possibly add spec falloff & surface roughness later
        }

        public ColourSet()
        {
            InitialiseSettings();
        }

        public ColourSet( ColourSet clone )
        {
            InitialiseSettings();
            CloneFromColourSet(clone);
        }

        public float? Get(string k)
        {
            try
            {
                return Settings[k];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public void Set(string k, float v)
        {
            if(Settings.ContainsKey(k))
            {
                Settings[k] = v;
                return;
            }
            return;
        }

        public void CloneIntoColourSet( ColourSet t)
        {
            foreach(KeyValuePair<string, float> kv in Settings)
            {
                t.Set(kv.Key, kv.Value);
            }
        }

        public void CloneFromColourSet( ColourSet t)
        {

            List<string> keys = new List<string>(Settings.Keys);
            foreach(string k in keys )
            {
                Settings[k] = t.Get(k) ?? 0f;
            }
        }
    }
    #endregion

    #region Clipboard
    // check how GC & static fields really interact at some point, would be better to do this in the partmodule.

    // copy rather than copyref colourset objects here so we don't trap complete partmodules
    [KSPAddon(KSPAddon.Startup.EditorAny, true)]
    public class ClipBoard : MonoBehaviour
    {

        static List<ColourSet> colourSets = new List<ColourSet>();

        public static void Copy(List<ColourSet> t)
        {
            // destroy old ColourSets
            colourSets.Clear(); // does this call the destructor of each element?

            for( int i = 0; i < t.Count; i++ )
            {
                colourSets.Add(new ColourSet( t[i]));
            }
        }

        public static void Paste(List<ColourSet> t)
        {
            t.Clear();
            for( int i = 0; i < colourSets.Count;i++ )
            {
                t.Add(new ColourSet(colourSets[i]));
            }
        }

        private void Start()
        {
            TDebug.Print("Clipboard started");
        }

        private void OnDestroy()
        {
            colourSets.Clear();
            TDebug.Print("Clipboard destroyed");
        }
    }
    #endregion

    public class ModuleTinter : PartModule
    {

        /* Reference
         * 
         * Game load:
         * Constructor -> OnAwake -> OnLoad -> GetInfo
         * prefab clone:
         * Constructor -> OnAwake
         * 
         * Editor new instance:
         * Constructor -> OnAwake -> OnStart -> OnSave
         * 
         * Vessel load:
         * Constructor -> OnAwake -> OnLoad -> OnSave -> OnStart
         * 
         * Craft Launch
         * Constructor -> OnAwake -> OnLoad ( from craft file ) -> OnSave -> OnStart -> OnActive -> On[Fixed]Update
         * 
         * Craft switch via tracking:
         * Constructor -> OnAwake -> OnLoad ( from persistence ) -> OnStart -> OnActive -> On[Fixed]Update
         */

        #region Vars
        private bool active = false;
        private bool needUpdate = false;
        private bool needShaderReplacement = true;
        private bool isSymmetryCounterpart = false;

        private List<ColourSet> colourSets = new List<ColourSet>();
        private int colourSetIndex = 0;

        private List<Material> ManagedMaterials = new List<Material>();

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Value"),
            UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendPoint = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Band"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendBand = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Falloff"),
         UI_FloatRange( minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendFalloff = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Saturation Threshold"),
         UI_FloatRange( minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendSaturationThreshold = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Hue"),
          UI_FloatRange( minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIHue = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Saturation"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUISaturation = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Value"),
          UI_FloatRange( minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIValue = 0;

        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Glossiness"),
          UI_FloatRange( minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIGloss = 100;


        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Copy colour settings")]
        public void CopytoClipboard()
        {
            ClipBoard.Copy(colourSets);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Paste colour settings")]
        public void PastefromClipboard()
        {
            ClipBoard.Paste(colourSets);
            colourSetIndex = (colourSetIndex > colourSets.Count) ? 0 : colourSetIndex;
            ColourSetToUI(colourSets[colourSetIndex]);
            needUpdate = true;
        }
        #endregion

        #region Private
        private void onTweakableChange(BaseField field, object what)
        {
            needUpdate = true;
        }

        private void AttachGuiFields()
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                var uiField = Fields[i].uiControlEditor;
                if (uiField.GetType().FullName == "UI_FloatRange")
                {
                    uiField.onFieldChanged = onTweakableChange;
                }
            }
        }

        private void ToggleFields(bool flag)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                var uiField = Fields[i].uiControlEditor;
                if (uiField != null && uiField.GetType().FullName == "UI_FloatRange")
                {
                    Fields[i].guiActiveEditor = flag;
                }
            }
            Events["CopytoClipboard"].guiActiveEditor = flag;
            Events["PastefromClipboard"].guiActiveEditor = flag;
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
            // also can't remember why not to use foreach, but I'm sure there was something

            MeshRenderer[] r = part.FindModelComponents<MeshRenderer>();
            for ( int i = 0; i < r.Length; i++ )
            {
                Materials.AddRange(r[i].materials);
            }

            for( int i = 0; i < Materials.Count; i++ )
            {
                bool manageThisMaterial = false;
                Material m = Materials[i];

                var replacementShader = AssetLoader.FetchRepacementShader(m.shader.name);
                if (replacementShader)
                {
                    m.shader = replacementShader;
                    manageThisMaterial = true;
                }
                else if ( AssetLoader.IsReplacementShader( m.shader.name ))
                {
                    manageThisMaterial = true;
                }

                if(manageThisMaterial)
                    ManagedMaterials.Add(m);

            }

            if( ManagedMaterials.Count > 0)
            {
                active = true;
                needUpdate = true;
            }

            needShaderReplacement = false;
        }

        private static float SliderToShaderValue( float v ) {
            return v / 255;
        }

        private static float Saturate( float v ) {
            return Mathf.Clamp01(v);
        }

        private void UpdateShaderValues()
        {

            foreach (Material m in ManagedMaterials.ToArray())
            {
                m.SetFloat("_TintPoint", SliderToShaderValue(tintUIBlendPoint));
                m.SetFloat("_TintBand", SliderToShaderValue(tintUIBlendBand));

                float tintFalloff = SliderToShaderValue(tintUIBlendFalloff);
                m.SetFloat("_TintFalloff", (tintFalloff > 0 ) ? tintFalloff : 0.001f ); // we divide by this in the shader
                m.SetFloat("_TintHue", SliderToShaderValue(tintUIHue));
                m.SetFloat("_TintSat", SliderToShaderValue(tintUISaturation));
                m.SetFloat("_TintVal", SliderToShaderValue(tintUIValue));

                float shaderTBTST = SliderToShaderValue(tintUIBlendSaturationThreshold);
                m.SetFloat("_TintSatThreshold", shaderTBTST);

                float shaderSatFalloff = Saturate(shaderTBTST * 0.75f);
                m.SetFloat("_SaturationFalloff", shaderSatFalloff);

                m.SetFloat("_SaturationWindow", shaderTBTST - shaderSatFalloff); // we divide by this in the shader too, but should only be 0 if the fraction is 0/0
                m.SetFloat("_GlossMult", tintUIGloss * 0.01f);
            }

            UIToColourSet(colourSets[colourSetIndex]);

            if(isSymmetryCounterpart)
            {
                isSymmetryCounterpart = false;
                return;
            }

            Part[] p = part.symmetryCounterparts.ToArray();
            for ( int i = 0; i < p.Length; i++ )
                p[i].Modules.GetModule<ModuleTinter>().SymmetryUpdate( this );
        }

        private void UIToColourSet( ColourSet c)
        {
            c.Set("BlendPoint", tintUIBlendPoint);
            c.Set("BlendBand", tintUIBlendBand);
            c.Set("BlendFalloff", tintUIBlendFalloff);
            c.Set("BlendSaturationThreshold", tintUIBlendSaturationThreshold);
            c.Set("Hue", tintUIHue);
            c.Set("Saturation", tintUISaturation);
            c.Set("Value", tintUIValue);
            c.Set("Glossiness", tintUIGloss);
            // specular
        }

        private void ColourSetToUI(ColourSet c)
        {
            tintUIBlendPoint = c.Get("BlendPoint") ?? 0f;
            tintUIBlendBand = c.Get("BlendBand" ) ?? 0f;
            tintUIBlendFalloff = c.Get("BlendFalloff" ) ?? 0f;
            tintUIBlendSaturationThreshold = c.Get("BlendSaturationThreshold" ) ?? 0f;
            tintUIHue = c.Get("Hue" ) ?? 0f;
            tintUISaturation = c.Get("Saturation" ) ?? 0f;
            tintUIValue = c.Get("Value" ) ?? 0f;
            tintUIGloss = c.Get("Glossiness" ) ?? 0f;
            // specular
        }
        
        #endregion


        #region Counterparts
        // consider doing symmetry updates via the clipboard
        public void CloneValuesFrom(ModuleTinter t)
        {
            tintUIBlendPoint = t.tintUIBlendPoint;
            tintUIBlendBand = t.tintUIBlendBand;
            tintUIBlendFalloff = t.tintUIBlendFalloff;
            tintUIBlendSaturationThreshold = t.tintUIBlendSaturationThreshold;
            tintUIHue = t.tintUIHue;
            tintUISaturation = t.tintUISaturation;
            tintUIValue = t.tintUIValue;
            tintUIGloss = t.tintUIGloss;
        }

        public void SymmetryUpdate(ModuleTinter t )
        {
            CloneValuesFrom(t);
            needUpdate = true;
            active = true;
            isSymmetryCounterpart = true;
        }
        #endregion

        #region Public Unity

        // why is the constructor being called twice
        public ModuleTinter()
        {
            active = false;
            needShaderReplacement = true;
//            TDebug.Print("ModuleTinter constructor called");
            
            // belt & braces
            if(colourSets.Count == 0 )
                colourSets.Add(new ColourSet());
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            part.OnEditorAttach += new Callback(OnEditorAttach);
            AttachGuiFields();
            TraverseAndReplaceShaders();

            if( HighLogic.LoadedSceneIsEditor )
                ToggleFields(active);
        }

        public void OnEditorAttach()
        {
            ToggleFields(active);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // temporary, need to store coloursets in save files
            UIToColourSet(colourSets[colourSetIndex]);
        }

        public void Update()
        {
            if (needUpdate)
            {
                needUpdate = false;
                UpdateShaderValues();
            }
        }

        public void Setup()
        {
 //           TDebug.Print("Setup() [" + this.part.name + "]");
        }
     }
    #endregion
}
