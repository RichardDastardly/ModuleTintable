using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// please bear in mind I don't know any better yet.

namespace Tintable
{
    #region Custom Attributes
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct|AttributeTargets.Method )]
    public sealed class Section : Attribute
    {
        public int section = 1;

        public Section(int section)
        {
            this.section = section;
        }
    }
    #endregion

    #region TDebug
    public static class TDebug
    {
        // debugging stuff from the start! how novel
        // dump this when we're done
        private readonly static string dbgTag = "[ModuleTintable] ";

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
        private Dictionary<string, float> _Settings = new Dictionary<string, float>();
        public Dictionary<string, float> Values
        {
            get
            {
                return new Dictionary<string, float>(_Settings);
            }
            set
            {
                _Settings = new Dictionary<string, float>(value);
            }
        }


        private List<bool> SectionFlags = new List<bool>();

        public ColourSet() { }

        public ColourSet( ColourSet clone )
        {
            Values = clone.Values;
        }

        public float? Get(string k)
        {
            try
            {
                return _Settings[k];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public void Set(string k, float v)
        {
            _Settings[k] = v;
        }

        public void SetSection( int section, bool flag )
        {
            while (section < SectionFlags.Count)
                SectionFlags.Add(true);
            SectionFlags[section] = flag;
        }

        // temp back compatible
        public void CloneIntoColourSet( ColourSet t)
        {
            Values = t.Values;
        }

        public void CloneFromColourSet( ColourSet t)
        {
            t.Values = Values;
        }
    }
    #endregion

    #region Clipboard
    // check how GC & static fields really interact at some point, would be better to do this in the partmodule.

    // copy rather than copyref colourset objects here so we don't trap complete partmodules
    //[KSPAddon(KSPAddon.Startup.EditorAny, true)]
    //public class ClipBoard : MonoBehaviour
    //{

    //    static List<ColourSet> colourSets = new List<ColourSet>();

    //    public static void Copy(List<ColourSet> t)
    //    {
    //        // destroy old ColourSets
    //        colourSets.Clear(); // does this call the destructor of each element?

    //        for( int i = 0; i < t.Count; i++ )
    //        {
    //            colourSets.Add(new ColourSet( t[i]));
    //        }
    //    }

    //    public static void Paste(List<ColourSet> t)
    //    {
    //        t.Clear();
    //        for( int i = 0; i < colourSets.Count;i++ )
    //        {
    //            t.Add(new ColourSet(colourSets[i]));
    //        }
    //    }

    //    private void OnStart()
    //    {
    //        TDebug.Print("Clipboard started");
    //    }

    //    private void OnDestroy()
    //    {
    //        colourSets.Clear();
    //        TDebug.Print("Clipboard destroyed");
    //    }
    //}
    #endregion

    public class ModuleTintable : PartModule
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

        private List<ColourSet> colourSets; // a part may use more than one colourset depending if it has a blend mask

        // use RGB values of mask to blend different coloursets if it has one
        [KSPField]
        private string paintMask = null;

        // should only be set in part config if there's a mask
        [KSPField(isPersistant = true)]
        private int paintableColours = 1;

        // Intended for patching existing parts
        // Will use blend as well as paintmask alpha to determine whether to colour an area, as 
        // static paint is usually in the paintmask
        [KSPField]
        private bool useBlendForStaticPaintMask = false;

        private int colourSetIndex = 0;

        private List<Material> ManagedMaterials;

        private enum tintUISection : int { All, Blend, Colour, Surface, Channel, Clipboard };
        private enum MapChannel : int { C1 = 0xFF0000, C2 = 0xFF00, C3 = 0xFF };

        [Section(1)] // can't use enum here, grumble
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Value"),
            UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendPoint = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Band"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendBand = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Falloff"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendFalloff = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Saturation Threshold"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIBlendSaturationThreshold = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Hue"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIHue = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Saturation"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUISaturation = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Value"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIValue = 0;

        [Section(3)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Glossiness"),
          UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUIGloss = 100;

        [Section(3)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Reflection tightness"),
          UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintUITightness = 100;

        #region Clipboard

        private static List<ColourSet> _clipboard;
        private static List<ColourSet> Clipboard
        {
            set {
                _clipboard.Clear(); // does this call the destructor of each element?

                for (int i = 0; i < value.Count; i++)
                {
                    _clipboard.Add(new ColourSet(value[i]));
                }
            }

            get { // not sure how useful this really is

                var t = new List<ColourSet>();
                for (int i = 0; i < _clipboard.Count; i++)
                {
                    t.Add(new ColourSet(_clipboard[i]));
                }
                return t;
            }
        }

        [Section(5)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Copy colour settings")]
        public void CopytoClipboard()
        {
            //ClipBoard.Copy(colourSets);
            Clipboard = colourSets;
        }

        [Section(5)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Paste colour settings")]
        public void PastefromClipboard()
        {
            colourSets.Clear();
            for (int i = 0; i < Clipboard.Count; i++)
            {
                colourSets.Add(new ColourSet(Clipboard[i]));
            }
            colourSetIndex = (colourSetIndex > colourSets.Count) ? 0 : colourSetIndex;
            ColourSetToUI(colourSets[colourSetIndex]);
            needUpdate = true;
        }
        #endregion
        #endregion


        #region Reflection / field cache / GetSet methods

        // consider abstracting this stuff a bit. Preferably consider doing something more sane.

        private static Dictionary<string, MemberInfo> UIMembers; // Cache of field data with [Section] attrib via reflection. Don't use if there's any alternative.
        //     private static List<List<FieldInfo>> UISection;
        
        private static bool structureCachePopulated = false;

 
        private enum UIEntityType { Field, Event, Action };

        class UIControlEntity
        {
            public string name;
            public int index;
            public UIEntityType type;
            public UIControlEntity( int i, UIEntityType t, string n = "" )
            {
                name = n;
                index = i;
                type = t;
            }

            public UIControlEntity( int i, MemberTypes t, string n = "" )
            {
                name = n;
                index = i;
                type = UIEntityType.Field;
                if (t == MemberTypes.Method)
                    type = UIEntityType.Event;
            }
        }

        class UIControlSection
        {
            private List<UIControlEntity> _entries = new List<UIControlEntity>();
            public UIControlEntity this[int index]
            {
                get
                {
                    return _entries[index];
                }
                set
                {
                    _entries[index] = value;
                }
            }
            public bool Active = true;

            public int Count
            {
                get
                {
                    return _entries.Count;
                }
            }

            public void Add( UIControlEntity e )
            {
                _entries.Add(e);
            }
        }
        private static List<UIControlSection> UISection;

 /*       private static Dictionary<string, UIControlEntity> UIControls; //  list of KSPField/Events. You know if only BaseField/Base/Event had 
                                                                // a common ancestor with the gui flags in it so much of this code would
                                                                // be unnecessary. Populated in OnAwake rather than via reflection
*/

        private static void PopulateStructureCache( object obj )
        {
            if (structureCachePopulated)
                return;
            //TDebug.Print("Populating structure cache");

            UIMembers = new Dictionary<string, MemberInfo>();
            UISection = new List<UIControlSection>();


            var _thisType = obj.GetType();
            var _uiMembers = _thisType.GetMembers(BindingFlags.Instance|BindingFlags.Public);

            for( int i = 0; i < _uiMembers.Length; i++ )
            {
                //TDebug.Print("Structure cache checking field " + _uiMembers[i].Name);
                if (Attribute.IsDefined(_uiMembers[i], typeof(Section)))
                {
                    UIMembers[_uiMembers[i].Name] = _uiMembers[i];
                    var _SectionAttr = (Section)Attribute.GetCustomAttribute(_uiMembers[i], typeof(Section));

                    while (UISection.Count <= _SectionAttr.section)
                        UISection.Add(new UIControlSection());

                    TDebug.Print("Structure cache saving field " + _uiMembers[i].Name + " to section "+ _SectionAttr.section+ " . Type " + _uiMembers[i].MemberType.ToString());

                    // section 0 is all fields with [Section] attributes
                    var _entry = new UIControlEntity(0, _uiMembers[i].MemberType, _uiMembers[i].Name);
                    UISection[0].Add( _entry );
                    UISection[_SectionAttr.section].Add(_entry);
                }
            }
            structureCachePopulated = true;
        }


        // these two need some work - creating lists just to dispose when you grab values is bad form
        // All fields
        //public static List<string> GetKSPFieldKeys()
        //{
        //    return new List<string>(UIMembers.Keys); // maybe just return (List<string>)UIMembers.Keys ?
        //}

        // only field names from a particular section
        private static List<string> GetKSPFieldKeys( int section = 0 )
        {
            if (section > UISection.Count || UISection[section] == null)
                return null;

            var SectionKeys = new List<string>();
            for( int i = 0; i < UISection[section].Count; i++ )
            {
                SectionKeys.Add(UISection[section][i].name);
            }
            return SectionKeys;
        }

        private static UIControlSection GetKSPEntities( int section = 0 )
        {
            if (section > UISection.Count || UISection[section] == null)
                return null;

            return UISection[section];
        }

        //public Dictionary<string,float> GetKSPFields()
        //{
        //    var UIValues = new Dictionary<string, float>(UIMembers.Count);
        //    var _UIKeys = new List<string>(UIMembers.Keys);
        //    for( int i = 0; i < _UIKeys.Count; i++ )
        //    {
        //        UIValues[_UIKeys[i]] = (float)Fields.GetValue(_UIKeys[i]);
        //    }
        //    return UIValues;
        //}

        public Dictionary<string, float> GetKSPFields(int section = 0 )
        {
            if ((section > UISection.Count )|| (UISection[section] == null))
                    return null;

            var UIValues = new Dictionary<string, float>(UISection[section].Count);

            for (int i = 0; i < UISection[section].Count; i++)
            {
                var _entity = UISection[section][i];
                if(_entity != null && _entity.type == UIEntityType.Field )
                    UIValues[_entity.name] = (float)Fields.GetValue(_entity.name);
            }
            return UIValues;
        }

        public void SetKSPFields(Dictionary<string, float> fieldData, int section = 0)
        {
            if ((section > UISection.Count) || (UISection[section] == null))
                return;

            var _Section = GetKSPEntities(section);
            for (int i = 0; i < _Section.Count; i++)
            {
                if( _Section[i].type == UIEntityType.Field )
                    Fields.SetValue(_Section[i].name, fieldData[_Section[i].name]);
            }
        }


        // Get/Set via reflection - this is pretty slow, but I don't know how else to do it without
        // directly referencing
        //public Dictionary<string,float> GetKSPFields()
        //{
        //    var UIValues = new Dictionary<string,float>( UIMembers.Count );
        //    var _UIKeys = new List<string>(UIMembers.Keys);
        //    for( int i = 0; i < _UIKeys.Count; i++ )
        //    {
        //        UIValues[UIMembers[_UIKeys[i]].Name] = (float)UIMembers[_UIKeys[i]].GetValue(this);
        //        //TDebug.Print("UI key " + _UIKeys[i] + " value " + UIMembers[_UIKeys[i]].GetValue(this));
        //    }
        //    return UIValues;
        //}

        //public Dictionary<string, float> GetKSPFields( int section )
        //{
        //    if ((section > UISection.Count )|| (UISection[section] == null))
        //        return null;

        //    var UIValues = new Dictionary<string, float>(UISection[section].Count);
        //    for (int i = 0; i < UISection[section].Count; i++)
        //    {
        //        UIValues[UISection[section][i].Name] = (float)UISection[section][i].GetValue(this);
        //    }
        //    return UIValues;
        //}

        //public void SetKSPFields( Dictionary<string,float> fieldData )
        //{
        //    var _UIKeys = new List<string>(fieldData.Keys);
        //    for ( int i = 0; i < fieldData.Count; i++ )
        //    {
        //        UIMembers[_UIKeys[i]].SetValue(this, fieldData[_UIKeys[i]]);
        //    }
        //}

        //public void SetKSPFields( int section, Dictionary<string,float> fieldData )
        //{
        //    if ((section > UISection.Count) || (UISection[section] == null))
        //        return;

        //    var _SectionKeys = GetKSPFieldKeys(section);
        //    for( int i = 0; i < _SectionKeys.Count; i++ )
        //    {
        //        UIMembers[_SectionKeys[i]].SetValue(this, fieldData[_SectionKeys[i]]);
        //    }
        //}


        #endregion

        #region Private
        private void UIEvent_onTweakableChange(BaseField field, object what)
        {
            needUpdate = true;
        }

        private void SetupUIFieldCallbacks()
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                var uiField = Fields[i].uiControlEditor;
                if (uiField.GetType().FullName == "UI_FloatRange")
                {
                    uiField.onFieldChanged = UIEvent_onTweakableChange;
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

            for (int i = 0; i <  ManagedMaterials.Count; i++ ) 
            {
                Material m = ManagedMaterials[i];
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
                p[i].Modules.GetModule<ModuleTintable>().SymmetryUpdate( this );
        }

        private void UIToColourSet( ColourSet c)
        {
            c.Values = GetKSPFields();// process uses a temporary dictionary, rather unnecessarily - clean
        }

        private void ColourSetToUI(ColourSet c)
        {
            SetKSPFields(c.Values); // process uses a temporary dictionary, rather unnecessarily - clean
        }

        private void UIMembersVisible(bool flag, int section = 0)
        {
            var _section = GetKSPEntities(section);
            for (int i = 0; i < _section.Count; i++)
            {
                //             UIControls[keys[i]].obj.guiActiveEditor = flag;
                //Convert.ChangeType(UIControls[keys[i]].obj, UIControls[keys[i]].type).guiActiveEditor = flag;

                if (_section[i].type == UIEntityType.Field)
                    Fields[_section[i].name].guiActiveEditor = flag;
                else if (_section[i].type == UIEntityType.Event)
                    Events[_section[i].name].guiActiveEditor = flag;
            }
        }

        #endregion


        #region Counterparts
        // consider doing symmetry updates via the clipboard
        public void CloneValuesFrom(ModuleTintable t)
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

        public void SymmetryUpdate(ModuleTintable t )
        {
            CloneValuesFrom(t);
            needUpdate = true;
            active = true;
            isSymmetryCounterpart = true;
        }
        #endregion

        #region Public Unity

        // why is the constructor being called twice
        public ModuleTintable()
        {
            active = false;
            needShaderReplacement = true;
            //            TDebug.Print("ModuleTintable constructor called");
            if (!structureCachePopulated)
                PopulateStructureCache(this);

        }

        // OnAwake() - initialise field refs here
        public override void OnAwake()
        {
            //if (UIControls == null)
            //{
            //    UIControls = new Dictionary<string, UIControlEntity>();

            //    for (int i = 0; i < Fields.Count; i++)
            //      UIControls[Fields[i].name] = new UIControlEntity(i, UIEntityType.Field);

            //    for (int i = 0; i < Events.Count; i++)
            //    {
            //        if (Events.GetByIndex(i) == null)
            //        {
            //            TDebug.Print("Events[" + i + "] is null!");
            //            continue;
            //        }

            //        UIControls[Events.GetByIndex(i).name] = new UIControlEntity(i, UIEntityType.Event);
            //    }
            //}

            ManagedMaterials = new List<Material>();

              // belt & braces
            if (colourSets == null)
            {
                //TDebug.Print("Initialising colourSets");
                colourSets = new List<ColourSet>();
            }
            if (colourSets.Count == 0)
                colourSets.Add(new ColourSet());

            if (_clipboard == null)
                _clipboard = new List<ColourSet>();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            part.OnEditorAttach += new Callback(OnEditorAttach);
            SetupUIFieldCallbacks();
            TraverseAndReplaceShaders();

            if( HighLogic.LoadedSceneIsEditor )
                UIMembersVisible(active);
        }

        public void OnEditorAttach()
        {
            UIMembersVisible(active);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // temporary, dump a few fields
            TDebug.Print("Paintmask: " + paintMask);
            TDebug.Print("PaintableColours: " + paintableColours);
            TDebug.Print("useBlendForStaticPaintMask: " + useBlendForStaticPaintMask.ToString());

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
