using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DLTD.Utility;

// please bear in mind I don't know any better yet.

namespace DLTD.Modules
{
    #region Asset Management

    public enum ShaderOverlayMask { None, Explicit, UseDefaultTexture };

    public class ShaderRecord
    {
        [Persistent]
        public string[] Replace;

        [Persistent]
        public bool useBlend = false;

        [Persistent]
        public bool usePaintMask = false;

        [Persistent]
        public ShaderOverlayMask shadingOverlayType = ShaderOverlayMask.None;

        [Persistent]
        public int numColourAreas = 1;

        public Shader Shader;
        
        public ShaderRecord( Shader newShader )
        {
            Shader = newShader;
        }

        public bool isReplacementFor( string shaderNameToReplace)
        {
            for( int i = 0; i < Replace.Length; i++)
            {
                if (Replace[i] == shaderNameToReplace)
                    return true;
            }
            return false;
        }

        public void _dumpToLog()
        {
            if (Replace == null)
            {
                TDebug.Print("No replacements found :(");
                Replace = new string[0];
            }
            var s = "Record "+ Shader.name + ": Replaces: ";
            for (int i = 0; i < Replace.Length; i++)
                s += Replace[i] + " ";

            s += " useBlend: " + useBlend.ToString();
            s += " usePaintMask: " + usePaintMask.ToString();
            s += " shadingOverlayType: " + shadingOverlayType.ToString();
            TDebug.Print(s);
            
        }
    }


    [KSPAddon(KSPAddon.Startup.MainMenu,true)]
    public class ShaderAssetManager : MonoBehaviour
    {
        private List<ShaderRecord> Shaders;
        private KSPPaths ModuleTintablePaths;

        private readonly string ShaderBundle = "DLTDTintableShaders";
        private readonly string BundleID = "ModuleTintable";

        private AssetManager AssetMgr;
        public static ShaderAssetManager instance;
        public static bool shadersLoaded = false;

        public void Awake()
        {
            Shaders = new List<ShaderRecord>();
            ModuleTintablePaths = new KSPPaths("DLTD/Plugins/ModuleTintable");
        }

        private IEnumerator LoadShaders()
        {
            var b = AssetMgr.LoadModBundle(ModuleTintablePaths, ShaderBundle, BundleID);
            while (b.state != BundleState.Loaded)
                yield return null;

            var bundleContents = AssetMgr.GetAssetsOfType<Shader>(BundleID);

            foreach (AssetRecord assetRec in bundleContents.Values)
            {
                //TDebug.Print("ShaderAssetManager got handed " + assetRec.Asset.name + " from " + assetRec.BundleID);
                if (assetRec.Attributes != null )
                {
                    var newShaderRec = new ShaderRecord(assetRec.Asset as Shader);
                    ConfigNode.LoadObjectFromConfig(newShaderRec, assetRec.Attributes);
                    newShaderRec.Replace = assetRec.Attributes.GetValues("replace");
                    Shaders.Add(newShaderRec);

                    //TDebug.Print("ShaderAssetManager added " + newShaderRec.Shader.name);
                    newShaderRec._dumpToLog();
                }
            }
            shadersLoaded = true;
        }

        public ShaderRecord GetReplacementShaderFor( string shaderNameToReplace )
        {
            for (int i = 0; i < Shaders.Count; i++)
                if (Shaders[i].isReplacementFor(shaderNameToReplace))
                    return Shaders[i];

            return null;
        }

        public void Start()
        {
            AssetMgr = AssetManager.instance;
            instance = this;

            StartCoroutine(LoadShaders());

        }
    }
    #endregion
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

    #region PaletteEntry
    public class PaletteEntry : IConfigNode
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

        public PaletteEntry() { }

        public PaletteEntry( PaletteEntry clone )
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

        // Confignode read/write
        public void Load(ConfigNode node )
        {
            _Settings.Clear();

            foreach (ConfigNode.Value v in node.values)
                _Settings.Add(v.name, float.Parse(v.value));
            
        }

        public void Save(ConfigNode node )
        {
            var k = new List<string>(_Settings.Keys);

            for (int i = 0; i < k.Count; i++)
                node.AddValue(k[i], _Settings[k[i]]);
        }

        // temp back compatible
        public void CloneIntoColourSet( PaletteEntry t)
        {
            Values = t.Values;
        }

        public void CloneFromColourSet( PaletteEntry t)
        {
            t.Values = Values;
        }
    }
    #endregion

    #region Palette
    public class Palette : IConfigNode
    {
        private List<PaletteEntry> _pStore;
        public PaletteEntry this[int index]
        {
            get { return _pStore[Mathf.Clamp(index, 0, _pStore.Count)]; }
            set { _pStore[index] = value; }
        }

        public int Count
        {
            get { return _pStore.Count; }
        }

        public int activeEntry = 0;
        private int _EntryCount = 1;

        public Palette()
        {
            Initialise();
            Add(new PaletteEntry());
        }

        public Palette( Palette p )
        {
            Clone( p );
        }

        public Palette( Palette p, int cols )
        {
            Clone(p);
            _EntryCount = cols;
        }

        public void Clone( Palette p )
        {
            Initialise();

            for (int i = 0; i < p.Count; i++)
                Add(new PaletteEntry(p[i]));
        }

        private void Initialise()
        {
            if (_pStore == null)
                _pStore = new List<PaletteEntry>();
            _pStore.Clear();
            _EntryCount = 0;
        }

        public PaletteEntry Next()
        {
            if (activeEntry++ < _EntryCount) // _EntryCount starts at 1
            {
                if (activeEntry >= _pStore.Count || _pStore[activeEntry] == null)
                    _pStore.Add(new PaletteEntry());
                return _pStore[activeEntry];
            }
            return null;
        }

        public PaletteEntry Previous()
        {
            if (activeEntry > 0 )
                return _pStore[--activeEntry];
            return null;
        }

        public PaletteEntry Active()
        {
            return _pStore[activeEntry];
        }

        public void Add( PaletteEntry p )
        {
            _pStore.Add(p);
            _EntryCount++;
        }

        public void Limit( int c )
        {
            _EntryCount = c;
        }

        public void Clear()
        {
            _pStore.Clear();
            Initialise();
        }

        private static string _entryTag = "PALETTE_ENTRY";

        public void Load(ConfigNode node)
        {
            Clear();
            Initialise();
            // Node is called PALETTE - assume this is passed the node, not the entire config 
            // entries will be PALETTE_ENTRY sub nodes
            foreach (ConfigNode e in node.GetNodes(_entryTag)) // I hope GetNodes() is ordered...
            {
                var p_e = new PaletteEntry();
                p_e.Load(e);
                Add(p_e);
            }
        }

        public void Save(ConfigNode node)
        {
            for ( int i = 0; i < _pStore.Count; i++ )
            {
                var n = new ConfigNode(_entryTag);
                _pStore[i].Save(n);
                node.AddNode( n );
            }
        }
    }
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
        private bool moduleActive = false;
        private bool needUpdate = false;
        private bool needShaderReplacement = true;
        private bool isSymmetryCounterpart = false;

        [KSPField]
        public Palette Palette;

        // use RGB values of mask to blend different coloursets if it has one
        [KSPField(isPersistant = true)]
        public string paintMask = null;

        // should only be set in part config if there's a mask
        [KSPField(isPersistant = true)]
        public int paintableColours = 1;

        // Intended for patching existing parts
        // Will use blend as well as paintmask alpha to determine whether to colour an area, as 
        // static paint is usually in the paintmask
        [KSPField(isPersistant = true)]
        public bool useBlendForStaticPaintMask = false;

        [KSPField(isPersistant = true)]
        List<string> ignoreGameObjects;

        private List<Material> ManagedMaterials;

        private enum UISectionID : int { All, Blend, Colour, Selector, Surface, Channel, Clipboard };
        private enum MapChannel : int { C1 = 0xFF0000, C2 = 0xFF00, C3 = 0xFF };

        [Section(1)] // can't use enum here, grumble
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Value"),
            UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBlendPoint = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Band"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBlendBand = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Falloff"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBlendFalloff = 0;

        [Section(1)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Saturation Threshold"),
         UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBlendSaturationThreshold = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Hue"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintHue = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Saturation"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintSaturation = 0;

        [Section(2)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Value"),
          UI_FloatRange(minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintValue = 0;

        [Section(3)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Next colour")]
        public void UINextColour()
        {

            Events[nameof(UIPrevColour)].guiActiveEditor = true;

            if (Palette.activeEntry < paintableColours)
            {
                ColourSetToUI(Palette.Next());
                if( Palette.activeEntry == paintableColours )
                    Events[nameof(UINextColour)].guiActiveEditor = false;
            }
        }

        [Section(3)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Prev colour")]
        public void UIPrevColour()
        {
            Events[nameof(UINextColour)].guiActiveEditor = true;
            if (Palette.activeEntry > 0 )
            {
                ColourSetToUI(Palette.Previous());
                if (Palette.activeEntry == 0 )
                    Events[nameof(UIPrevColour)].guiActiveEditor = false;
                // disable button
            }
        }

        [Section(4)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Glossiness"),
          UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintGloss = 100;

        [Section(4)]
        [KSPField(category = "TintMenu", isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Reflection tightness"),
          UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintTightness = 100;

        #region Clipboard

        //private static List<PaletteEntry> _clipboard;
        //private static List<PaletteEntry> Clipboard
        //{
        //    set {
        //        _clipboard.Clear(); // does this call the destructor of each element?

        //        for (int i = 0; i < value.Count; i++)
        //        {
        //            _clipboard.Add(new PaletteEntry(value[i]));
        //        }
        //    }

        //    get { // not sure how useful this really is

        //        var t = new List<PaletteEntry>();
        //        for (int i = 0; i < _clipboard.Count; i++)
        //        {
        //            t.Add(new PaletteEntry(_clipboard[i]));
        //        }
        //        return t;
        //    }
        //}

        private static Palette _cb;
        private static Palette Clipboard
        {
            set
            {
                _cb = new Palette(value);
            }
            get
            {
                return new Palette(_cb );
            }
        }

        [Section(6)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Copy colour settings")]
        public void CopytoClipboard()
        {
            //ClipBoard.Copy(Palette);
            Clipboard = Palette;
        }

        [Section(6)]
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Paste colour settings")]
        public void PastefromClipboard()
        {
            // check what happens if this is an empty palette...
            Palette = Clipboard;
            Palette.Limit(paintableColours);
            
            ColourSetToUI(Palette.Active());
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

            TDebug.DbgTag = "[ModuleTintable] ";
        }


        // these two need some work - creating lists just to dispose when you grab values is bad form
        // All fields
        //public static List<string> GetKSPSectionEntityKeys()
        //{
        //    return new List<string>(UIMembers.Keys); // maybe just return (List<string>)UIMembers.Keys ?
        //}

        // only field names from a particular section
        private static List<string> GetKSPSectionEntityKeys( int section = 0 )
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
                if( _Section[i].type == UIEntityType.Field && fieldData.ContainsKey(_Section[i].name))
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

        //    var _SectionKeys = GetKSPSectionEntityKeys(section);
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
            if (!ShaderAssetManager.shadersLoaded)
                return;

            var SAM = ShaderAssetManager.instance;

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

                var replacementShader = SAM.GetReplacementShaderFor(m.shader.name);
                if (replacementShader != null )
                {
                    m.shader = replacementShader.Shader;
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
                moduleActive = true;
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
                m.SetFloat("_TintPoint", SliderToShaderValue(tintBlendPoint));
                m.SetFloat("_TintBand", SliderToShaderValue(tintBlendBand));

                float tintFalloff = SliderToShaderValue(tintBlendFalloff);
                m.SetFloat("_TintFalloff", (tintFalloff > 0 ) ? tintFalloff : 0.001f ); // we divide by this in the shader
                m.SetFloat("_TintHue", SliderToShaderValue(tintHue));
                m.SetFloat("_TintSat", SliderToShaderValue(tintSaturation));
                m.SetFloat("_TintVal", SliderToShaderValue(tintValue));

                float shaderTBTST = SliderToShaderValue(tintBlendSaturationThreshold);
                m.SetFloat("_TintSatThreshold", shaderTBTST);

                float shaderSatFalloff = Saturate(shaderTBTST * 0.75f);
                m.SetFloat("_SaturationFalloff", shaderSatFalloff);

                m.SetFloat("_SaturationWindow", shaderTBTST - shaderSatFalloff); // we divide by this in the shader too, but should only be 0 if the fraction is 0/0
                m.SetFloat("_GlossMult", tintGloss * 0.01f);
            }

            if(isSymmetryCounterpart)
            {
                isSymmetryCounterpart = false;
                return;
            }

            Part[] p = part.symmetryCounterparts.ToArray();
            for ( int i = 0; i < p.Length; i++ )
                p[i].Modules.GetModule<ModuleTintable>().SymmetryUpdate( this );
        }

        private void UIToColourSet( PaletteEntry c)
        {
            c.Values = GetKSPFields();// process uses a temporary dictionary, rather unnecessarily - clean
        }

        private void ColourSetToUI(PaletteEntry c)
        {
            SetKSPFields(c.Values); // process uses a temporary dictionary, rather unnecessarily - clean
        }

        private void UISectionVisible(bool flag, int section = 0)
        {
            var _section = GetKSPEntities(section);
            _section.Active = flag;
            TDebug.Print(part.name + " UISectionVisible setting " + flag.ToString() + " for section " + section);
            for (int i = 0; i < _section.Count; i++)
            {
                //             UIControls[keys[i]].obj.guiActiveEditor = flag;
                //Convert.ChangeType(UIControls[keys[i]].obj, UIControls[keys[i]].type).guiActiveEditor = flag;
                TDebug.Print(part.name + " Set flag for entity " + _section[i].name);
                if (_section[i].type == UIEntityType.Field)
                    Fields[_section[i].name].guiActiveEditor = flag;
                else if (_section[i].type == UIEntityType.Event)
                    Events[_section[i].name].guiActiveEditor = flag;
            }
        }

        private void UIVisible( bool flag )
        {
            if (!flag )
            {
                UISectionVisible(flag);
            }
            else
            {
                for (int i = 1; i < UISection.Count; i++)
                {
                    UISectionVisible(UISection[i].Active, i);
                }
            }
        }

        #endregion


        #region Counterparts
        // consider doing symmetry updates via the clipboard
        //public void CloneValuesFrom(ModuleTintable t)
        //{
        //    tintBlendPoint = t.tintBlendPoint;
        //    tintBlendBand = t.tintBlendBand;
        //    tintBlendFalloff = t.tintBlendFalloff;
        //    tintBlendSaturationThreshold = t.tintBlendSaturationThreshold;
        //    tintHue = t.tintHue;
        //    tintSaturation = t.tintSaturation;
        //    tintValue = t.tintValue;
        //    tintGloss = t.tintGloss;
        //}

        public void SymmetryUpdate(ModuleTintable t )
        {
            SetKSPFields(t.GetKSPFields());
            needUpdate = true;
            moduleActive = true;
            isSymmetryCounterpart = true;
        }
        #endregion

        #region Public Unity

        // why is the constructor being called twice
        public ModuleTintable()
        {
            moduleActive = false;
            needShaderReplacement = true;
            //            TDebug.Print("ModuleTintable constructor called");
            if (!structureCachePopulated)
                PopulateStructureCache(this);

        }

        // OnAwake() - initialise field refs here
        public override void OnAwake()
        {
            ManagedMaterials = new List<Material>();

              // belt & braces
            if (Palette == null)
            {
                Palette = new Palette();
            }

            //TDebug.Print(part.name + " OnAwake: paintableColours " + paintableColours + " prev/next visible " + UISection[(int)UISectionID.Selector].Active);
            //// temporary, need to store coloursets in save files
            //UIToColourSet(Palette[activePaletteEntry]);

        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            part.OnEditorAttach += new Callback(OnEditorAttach);

            //TDebug.Print(part.name + " OnStart: paintableColours " + paintableColours + " prev/next visible " + UISection[(int)UISectionID.Selector].Active);
            UISectionVisible(paintableColours > 1, (int)UISectionID.Selector);

            Palette.Limit(paintableColours);            

            // need to serialise PaletteEntry
            UIToColourSet(Palette.Active());

            SetupUIFieldCallbacks();
            TraverseAndReplaceShaders();

            if( HighLogic.LoadedSceneIsEditor )
                UIVisible(moduleActive);
        }

        public void OnEditorAttach()
        {
            UIVisible(moduleActive);
        }

        private static string PaletteTag = "PALETTE";

        public override void OnSave(ConfigNode node)
        {
            var p = new ConfigNode(PaletteTag);
            Palette.Save(p);

            node.AddNode(p);
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            var p = node.GetNode(PaletteTag);
            if (p != null)
                Palette.Load(p);

            // temporary, dump a few fields
            //TDebug.Print(part.name + "OnLoad:");
 //           TDebug.Print("Paintmask: " + paintMask);
            //TDebug.Print(part.name + "PaintableColours: " + paintableColours);
 //           TDebug.Print("useBlendForStaticPaintMask: " + useBlendForStaticPaintMask.ToString());

            UISectionVisible(paintableColours > 1, (int)UISectionID.Selector);
        }

        public void Update()
        {
            if (needUpdate)
            {
                needUpdate = false;
                UIToColourSet(Palette.Active());
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
