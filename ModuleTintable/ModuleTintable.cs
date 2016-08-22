using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DLTD.Utility;

// please bear in mind I don't know any better yet.

namespace DLTD.Modules
{

    #region Custom Attributes
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct|AttributeTargets.Method|AttributeTargets.Property )]
    public sealed class Section : Attribute
    {
        public int section = 1;
        public bool uiEntry = true;

        public Section(int section)
        {
            this.section = section;
        }

        public Section(int section, bool uiEntry) : this ( section )
        {
            this.uiEntry = uiEntry;
        }
    }
    #endregion

    #region PaletteEntry
    public class PaletteEntry : IConfigNode
    {
        protected Dictionary<string, float> _Settings = new Dictionary<string, float>();

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

        public float this[string key]
        {
            get { try { return _Settings[key]; } catch (KeyNotFoundException) { return 0; } }
            set { _Settings[key] = value; }
        }

        public Color Colour
        {
            get { return HSVtoRGB(_Settings["tintHue"], _Settings["tintSaturation"], _Settings["tintValue"]); }
        }

        private List<bool> SectionFlags = new List<bool>();

        public PaletteEntry() { }

        public PaletteEntry(PaletteEntry clone)
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

        public float? GetForShader(string k)
        {
            try
            {
                return Mathf.Clamp01(_Settings[k] / 255);
            }
            catch(KeyNotFoundException)
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

        public static Color HSVtoRGB(float H, float S, float V)
        {
            float R, G, B;
            H = H / 255;
            S = S / 255;
            V = V / 255;


            if (V <= 0)
            {
                R = G = B = 0;
            }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                float hf = H * 6.0f;
                int i = (int)Mathf.Floor(hf);
                float f = hf - i;
                float pv = V * (1 - S);
                float qv = V * (1 - S * f);
                float tv = V * (1 - S * (1 - f));
                switch (i)
                {
                    // Red is the dominant color
                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    // Green is the dominant color
                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;
                    // Blue is the dominant color
                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;
                    // Red is the dominant color
                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;
                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.
                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;
                    // The color is not defined, we should throw an error.
                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }

            return new Color(
                Mathf.Clamp01(R),
                Mathf.Clamp01(G),
                Mathf.Clamp01(B));
        }

    }
    #endregion

    #region Palette
    public class Palette : IConfigNode
    {
        private List<PaletteEntry> _pStore;
        public PaletteEntry this[int index]
        {
            get { return _pStore[Mathf.Clamp(index, 0, _pStore.Count-1)]; }
            set { _pStore[index] = value; }
        }

        public int Count
        {
            get { return _pStore.Count; }
        }

        public int activeEntry = 0;
        private int _EntryCount = 1;
        public int Length
        {
            get { return _EntryCount; }
        }

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

        // should only be set in part config if there's a mask
        [KSPField(isPersistant = true)]
        public int paintableColours = 1;

        [KSPField(isPersistant = true)]
        public string requestShader;

  //      [KSPField(isPersistant = true)]
        public string[] ignoreGameObjects;
        public string[] rawMaps;

        //        private List<Material> ManagedMaterials;
        private List<ShaderReplacementController> ManagedMaterials;

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

        [Section(1, uiEntry = false)]
        public float saturationFalloff
        {
            get { return Mathf.Clamp01(tintBlendSaturationThreshold * 0.75f); }
        }

        [Section(1, uiEntry = false)]
        public float saturationWindow
        {
            get { return tintBlendSaturationThreshold - saturationFalloff; }
        }

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
        public float tintValue = 150;

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

        private TDebug dbg;

        #region Reflection / field cache / GetSet methods

        // consider abstracting this stuff a bit. Preferably consider doing something more sane.

        private static Dictionary<string, MemberInfo> SectionMembers; // Cache of field data with [Section] attrib via reflection. Don't use if there's any alternative.
        //     private static List<List<FieldInfo>> MembersBySection;
        
        private static bool structureCachePopulated = false;
 
        private enum SectionEntityType { Field, Event, Action, Property };

        class SectionControlEntity
        {
            public string name;
            public int index;
            public SectionEntityType type;
            public SectionControlEntity( int i, SectionEntityType t, string n = "" )
            {
                name = n;
                index = i;
                type = t;
            }

            public SectionControlEntity( int i, MemberTypes t, string n = "" )
            {
                name = n;
                index = i;
                type = SectionEntityType.Field;

                if (t == MemberTypes.Method)
                    type = SectionEntityType.Event;

                if (t == MemberTypes.Property)
                    type = SectionEntityType.Property;
            }
        }

        class SectionControlSection
        {
            private List<SectionControlEntity> _entries = new List<SectionControlEntity>();
            public SectionControlEntity this[int index]
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

            public void Add( SectionControlEntity e )
            {
                _entries.Add(e);
            }
        }
        private static List<SectionControlSection> MembersBySection;

        private static void PopulateStructureCache( object obj )
        {
            if (structureCachePopulated)
                return;
            //dbg.Print("Populating structure cache");

            SectionMembers = new Dictionary<string, MemberInfo>();
            MembersBySection = new List<SectionControlSection>();


            var _thisType = obj.GetType();
            var _classMembers = _thisType.GetMembers(BindingFlags.Instance|BindingFlags.Public);

            for( int i = 0; i < _classMembers.Length; i++ )
            {
                //dbg.Print("Structure cache checking field " + _uiMembers[i].Name);
                if (Attribute.IsDefined(_classMembers[i], typeof(Section)))
                {
                    var _SectionAttr = (Section)Attribute.GetCustomAttribute(_classMembers[i], typeof(Section));

                    while (MembersBySection.Count <= _SectionAttr.section)
                        MembersBySection.Add(new SectionControlSection());


                    switch(_classMembers[i].MemberType)
                    {
                        case MemberTypes.Field:
                            SectionMembers[_classMembers[i].Name] = _thisType.GetField(_classMembers[i].Name);
                            break;
                        case MemberTypes.Property:
                            SectionMembers[_classMembers[i].Name] = _thisType.GetProperty(_classMembers[i].Name);
                            break;
                        case MemberTypes.Method:
                            SectionMembers[_classMembers[i].Name] = _thisType.GetMethod(_classMembers[i].Name);
                            break;
                    }

  //                  SectionMembers[_classMembers[i].Name] = _classMembers[i];

                    (obj as ModuleTintable).dbg.Print("Structure cache saving field " + _classMembers[i].Name + " to section " + _SectionAttr.section + " . Type " + _classMembers[i].MemberType.ToString());

                    // section 0 is all fields with [Section] attributes
                    var _entry = new SectionControlEntity(0, _classMembers[i].MemberType, _classMembers[i].Name);
                    MembersBySection[0].Add(_entry);
                    MembersBySection[_SectionAttr.section].Add(_entry);

                }
            }
            structureCachePopulated = true;

        }


        // these two need some work - creating lists just to dispose when you grab values is bad form
        // All fields
        //public static List<string> GetKSPSectionEntityKeys()
        //{
        //    return new List<string>(SectionMembers.Keys); // maybe just return (List<string>)SectionMembers.Keys ?
        //}

        // only field names from a particular section
        private static List<string> GetKSPSectionEntityKeys( int section = 0 )
        {
            if (section > MembersBySection.Count || MembersBySection[section] == null)
                return null;

            var SectionKeys = new List<string>();
            for( int i = 0; i < MembersBySection[section].Count; i++ )
            {
                SectionKeys.Add(MembersBySection[section][i].name);
            }
            return SectionKeys;
        }

        private static SectionControlSection GetKSPEntities( int section = 0 )
        {
            if (section > MembersBySection.Count || MembersBySection[section] == null)
                return null;

            return MembersBySection[section];
        }

        //public Dictionary<string,float> GetKSPFields()
        //{
        //    var MemberValues = new Dictionary<string, float>(SectionMembers.Count);
        //    var _UIKeys = new List<string>(SectionMembers.Keys);
        //    for( int i = 0; i < _UIKeys.Count; i++ )
        //    {
        //        MemberValues[_UIKeys[i]] = (float)Fields.GetValue(_UIKeys[i]);
        //    }
        //    return MemberValues;
        //}

        public Dictionary<string, float> GetKSPFields(int section = 0 )
        {
            if ((section > MembersBySection.Count )|| (MembersBySection[section] == null))
                    return null;

            var MemberValues = new Dictionary<string, float>(MembersBySection[section].Count);

            for (int i = 0; i < MembersBySection[section].Count; i++)
            {
                var _entity = MembersBySection[section][i];
                if (_entity != null)
                {
                    if (_entity.type == SectionEntityType.Field)
                        MemberValues[_entity.name] = (float)Fields.GetValue(_entity.name);
                    else if (_entity.type == SectionEntityType.Property)
                        MemberValues[_entity.name] = (float)(SectionMembers[_entity.name] as PropertyInfo).GetValue(this, null);
                }
            }
            return MemberValues;
        }

        public void SetKSPFields(Dictionary<string, float> fieldData, int section = 0)
        {
            if ((section > MembersBySection.Count) || (MembersBySection[section] == null))
                return;

            var _Section = GetKSPEntities(section);
            for (int i = 0; i < _Section.Count; i++)
            {
                if( _Section[i].type == SectionEntityType.Field && fieldData.ContainsKey(_Section[i].name))
                    Fields.SetValue(_Section[i].name, fieldData[_Section[i].name]);
            }
        }


        // Get/Set via reflection - this is pretty slow, but I don't know how else to do it without
        // directly referencing
        //public Dictionary<string,float> GetKSPFields()
        //{
        //    var MemberValues = new Dictionary<string,float>( SectionMembers.Count );
        //    var _UIKeys = new List<string>(SectionMembers.Keys);
        //    for( int i = 0; i < _UIKeys.Count; i++ )
        //    {
        //        MemberValues[SectionMembers[_UIKeys[i]].Name] = (float)SectionMembers[_UIKeys[i]].GetValue(this);
        //        //dbg.Print("UI key " + _UIKeys[i] + " value " + SectionMembers[_UIKeys[i]].GetValue(this));
        //    }
        //    return MemberValues;
        //}

        //public Dictionary<string, float> GetKSPFields( int section )
        //{
        //    if ((section > MembersBySection.Count )|| (MembersBySection[section] == null))
        //        return null;

        //    var MemberValues = new Dictionary<string, float>(MembersBySection[section].Count);
        //    for (int i = 0; i < MembersBySection[section].Count; i++)
        //    {
        //        MemberValues[MembersBySection[section][i].Name] = (float)MembersBySection[section][i].GetValue(this);
        //    }
        //    return MemberValues;
        //}

        //public void SetKSPFields( Dictionary<string,float> fieldData )
        //{
        //    var _UIKeys = new List<string>(fieldData.Keys);
        //    for ( int i = 0; i < fieldData.Count; i++ )
        //    {
        //        SectionMembers[_UIKeys[i]].SetValue(this, fieldData[_UIKeys[i]]);
        //    }
        //}

        //public void SetKSPFields( int section, Dictionary<string,float> fieldData )
        //{
        //    if ((section > MembersBySection.Count) || (MembersBySection[section] == null))
        //        return;

        //    var _SectionKeys = GetKSPSectionEntityKeys(section);
        //    for( int i = 0; i < _SectionKeys.Count; i++ )
        //    {
        //        SectionMembers[_SectionKeys[i]].SetValue(this, fieldData[_SectionKeys[i]]);
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
                    uiField.onFieldChanged += UIEvent_onTweakableChange;
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
                dbg.Print("Apparently " + part.name + " doesn't need shader replacement");
                return;
            }

            var Materials = new List<Material>();

            // messy messy, tidy

            MeshRenderer[] r = part.FindModelComponents<MeshRenderer>();
            for ( int i = 0; i < r.Length; i++ )
            {
                Materials.AddRange(r[i].materials);
            }

            if (ManagedMaterials.Count == 0)
            {
                for (int i = 0; i < Materials.Count; i++)
                {
                    bool manageThisMaterial = false;
                    Material m = Materials[i];

                    ShaderReplacementController replacementShader = null;

                    if (requestShader != null)
                        replacementShader = SAM.GetShader(requestShader);

                    if (replacementShader == null)
                        replacementShader = SAM.GetReplacementShaderFor(m.shader.name);

                    if (replacementShader != null)
                    {
                        if (rawMaps != null)
                            replacementShader.Load(rawMaps);
                        replacementShader.ReplaceShaderIn(m);
                        manageThisMaterial = true;
                    }
                    else if (SAM.IsManagedShader(m.shader.name))
                    {
                        dbg.Print("This is a managed shader");
                        manageThisMaterial = true;
                        replacementShader = SAM.GetShader(m);
                        replacementShader.doShaderSetup();
                    }

                    if (manageThisMaterial)
                    {
                        ManagedMaterials.Add(replacementShader);
                    }
                }
            }
            else
            {
                for (int i = 0; i < ManagedMaterials.Count; i++)
                {
                    ManagedMaterials[i].doShaderSetup();
                }

            }

            if( ManagedMaterials.Count > 0)
            {
                moduleActive = true;
                needUpdate = true;

            }

            UISectionVisible(false, (int)UISectionID.Blend);

            for (int i = 0; i < ManagedMaterials.Count; i++)
            {
                dbg.Print(part.name + " shader " + ManagedMaterials[i].Shader.name + " useBlend " + ManagedMaterials[i].useBlend.ToString());
                if (ManagedMaterials[i].useBlend)
                    UISectionVisible(true, (int)UISectionID.Blend);
            }


                needShaderReplacement = false;
        }

        private void UpdateShaderValues()
        {

            for (int i = 0; i <  ManagedMaterials.Count; i++ ) 
            {
                ManagedMaterials[i].UpdateShaderWith(Palette);
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
 //           dbg.Print(part.name + " UISectionVisible setting " + flag.ToString() + " for section " + section);
            for (int i = 0; i < _section.Count; i++)
            {
                //             UIControls[keys[i]].obj.guiActiveEditor = flag;
                //Convert.ChangeType(UIControls[keys[i]].obj, UIControls[keys[i]].type).guiActiveEditor = flag;
 //               dbg.Print(part.name + " Set flag for entity " + _section[i].name);
                if (_section[i].type == SectionEntityType.Field)
                    Fields[_section[i].name].guiActiveEditor = flag;
                else if (_section[i].type == SectionEntityType.Event)
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
                for (int i = 1; i < MembersBySection.Count; i++)
                {
                    UISectionVisible(MembersBySection[i].Active, i);
                }
            }
        }

        #endregion


        #region Counterparts
        public void SymmetryUpdate(ModuleTintable t )
        {
            //           SetKSPFields(t.GetKSPFields());
            Palette = t.Palette;
            ColourSetToUI(Palette.Active());
            needUpdate = true;
            moduleActive = true;
            isSymmetryCounterpart = true;
        }
        #endregion

        #region Public Unity

        // why is the constructor being called twice
        public ModuleTintable()
        {
            dbg = new TDebug("[ModuleTintable] ");
            moduleActive = false;
            needShaderReplacement = true;
            //            dbg.Print("ModuleTintable constructor called");
            if (!structureCachePopulated)
                PopulateStructureCache(this);
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
   //         dbg.Print("OnInitialize(): symmetry member "+ part.isSymmetryCounterPart(part).ToString());
            UIVisible(moduleActive);
        }

        // OnAwake() - initialise field refs here
        public override void OnAwake()
        {
            ManagedMaterials = new List<ShaderReplacementController>();

              // belt & braces
            if (Palette == null)
            {
                Palette = new Palette();
            }

            base.OnAwake();
            //dbg.Print(part.name + " OnAwake: paintableColours " + paintableColours + " prev/next visible " + MembersBySection[(int)UISectionID.Selector].Active);
            //// temporary, need to store coloursets in save files
            //UIToColourSet(Palette[activePaletteEntry]);

        }

        public void Start()
        {
        //    base.OnStart(state);
            part.OnEditorAttach += new Callback(OnEditorAttach);

            //dbg.Print(part.name + " OnStart: paintableColours " + paintableColours + " prev/next visible " + MembersBySection[(int)UISectionID.Selector].Active);
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
            base.OnSave(node);

            var paletteNode = new ConfigNode(PaletteTag);
            Palette.Save(paletteNode);

            node.AddNode(paletteNode);

            node.AddValue("ignoreGameObjects", string.Join(",",ignoreGameObjects));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            var paletteNode = node.GetNode(PaletteTag);
            if (paletteNode != null)
                Palette.Load(paletteNode);

            //            ignoreGameObjects = node.GetValues("ignoreGameObject");
            var goNode = node.GetValue("ignoreGameObjects");
            if( goNode != null )
                ignoreGameObjects = goNode.Split(',');

            rawMaps = node.GetValues("Map");

            // temporary, dump a few fields
            //dbg.Print(part.name + "OnLoad:");
 //           dbg.Print("Paintmask: " + paintMask);
            //dbg.Print(part.name + "PaintableColours: " + paintableColours);
 //           dbg.Print("useBlendForStaticPaintMask: " + useBlendForStaticPaintMask.ToString());

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
 //           dbg.Print("Setup() [" + this.part.name + "]");
        }
     }
    #endregion
}
