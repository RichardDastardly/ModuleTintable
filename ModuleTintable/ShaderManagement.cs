using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DLTD.Utility
{
    /// <summary>
    /// 
    /// DLTD_U_SM_Constant : Constants for ShaderManagement in DLTD.Utility namespace
    /// </summary>
    public static class DLTD_U_SM_Constant
    {
        public const string PaletteTag = "PALETTE";
        public const string PaletteEntryTag = "PALETTE_ENTRY";
    }

    #region PaletteEntry
    public class PaletteEntry : IConfigNode
    {
        protected Dictionary<string, float> _Settings = new Dictionary<string, float>();
        public float OutputDivisor = 255;
        
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
            get { try { return _Settings[key]; } catch (KeyNotFoundException) { return 0; } } // yeah, should probably not return defaults
            set { _Settings[key] = value; }
        }

        public Color Colour
        {
            get { return HSVtoRGB(Output("tintHue"), Output("tintSaturation"), Output("tintValue")); }
        }

        private List<bool> SectionFlags = new List<bool>();

        public PaletteEntry() { }
        public PaletteEntry( float Divisor )
        {
            OutputDivisor = Divisor;
        }

        public PaletteEntry(PaletteEntry clone)
        {
            Values = clone.Values;
            OutputDivisor = clone.OutputDivisor;
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

        public float Output(string k)
        {
            return Mathf.Clamp01(this[k] / OutputDivisor);
        }

        public void Set(string k, float v)
        {
            _Settings[k] = v;
        }

        public void SetSection(int section, bool flag)
        {
            while (section < SectionFlags.Count)
                SectionFlags.Add(true);
            SectionFlags[section] = flag;
        }

        // Confignode read/write
        public void Load(ConfigNode node)
        {
            _Settings.Clear();

            foreach (ConfigNode.Value v in node.values)
                _Settings.Add(v.name, float.Parse(v.value));

        }

        public void Save(ConfigNode node)
        {
            var k = new List<string>(_Settings.Keys);

            for (int i = 0; i < k.Count; i++)
                node.AddValue(k[i], _Settings[k[i]]);
        }

        // temp back compatible
        public void CloneIntoColourSet(PaletteEntry t)
        {
            Values = t.Values;
        }

        public void CloneFromColourSet(PaletteEntry t)
        {
            t.Values = Values;
        }

        // taken from the shader

        private const float Epsilon = 1e-10f;

        public static Color HSVtoRGB2(float H = 0f, float S = 0f, float V = 0f)
        {
            // HUEtoRGB section
            var R = Mathf.Abs(H * 6 - 3) - 1;
            var G = 2 - Mathf.Abs(H * 6 - 2);
            var B = 2 - Mathf.Abs(H * 6 - 4);

            return new Color(
                (R - 1) * (S + 1) * V,
                (G - 1) * (S + 1) * V,
                (B - 1) * (S + 1) * V
                );
        }

        public static Color HSVtoRGB(float H, float S, float V)
        {
            float R, G, B;

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
    public delegate void PaletteActiveEntryEvent(Palette p);

    public class Palette : IConfigNode
    {
        private List<PaletteEntry> _pStore;
        public PaletteEntry this[int index]
        {
            get { return _pStore[Mathf.Clamp(index, 0, _pStore.Count - 1)]; }
            set { _pStore[index] = value; }
        }

        public float DefaultOutputDivisor = 255;

        public PaletteActiveEntryEvent PaletteActiveEntryChange;

        public virtual void OnPaletteEntryChange()
        {
            PaletteActiveEntryChange?.Invoke(this);
        }

        public int Count
        {
            get { return _pStore.Count; }
        }

        public int activeEntry = 0;

        //
        private int _EntryCount = 1;
        public int Length
        {
            get { return _EntryCount; }
        }

        public int LastIndex
        {
            get { return _EntryCount - 1; }
        }

        public Palette()
        {
            Initialise();
            Add(new PaletteEntry(DefaultOutputDivisor));
        }

        public Palette(Palette p)
        {
            Initialise();
            DefaultOutputDivisor = p.DefaultOutputDivisor;

            for (int i = 0; i < p.Count; i++)
                Add(new PaletteEntry(p[i]));
        }

        public Palette(Palette p, int cols) : this ( p )
        {
            _EntryCount = cols;
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
            if (activeEntry < LastIndex)
            {
                activeEntry++;

                if (activeEntry >= _pStore.Count || _pStore[activeEntry] == null)
                    _pStore.Add(new PaletteEntry(DefaultOutputDivisor));

                OnPaletteEntryChange();
            }
            return _pStore[activeEntry];
        }

        public PaletteEntry Previous()
        {
            if (activeEntry > 0)
            {
                activeEntry--;
                OnPaletteEntryChange();
            }
            return _pStore[activeEntry];
        }

        public PaletteEntry Active
        {
            get { return _pStore[activeEntry]; }
        }

        public void Add(PaletteEntry p)
        {
            _pStore.Add(p);
            _EntryCount++;
        }

        public void Limit(int c) // number of entries, not final array position
        {
            //          _EntryCount = c > 0 ? c - 1 : 0;
            _EntryCount = c;
        }

        public void Clear()
        {
            _pStore.Clear();
            Initialise();
        }

        public void Load(ConfigNode node)
        {
            Clear();
            Initialise();
            // Node is called PALETTE - assume this is passed the node, not the entire config 
            // entries will be PALETTE_ENTRY sub nodes
            foreach (ConfigNode e in node.GetNodes(DLTD_U_SM_Constant.PaletteEntryTag)) // I hope GetNodes() is ordered...
            {
                var p_e = new PaletteEntry(DefaultOutputDivisor);
                p_e.Load(e);
                Add(p_e);
            }
        }

        public void Save(ConfigNode node)
        {
            for (int i = 0; i < _pStore.Count; i++)
            {
                var n = new ConfigNode(DLTD_U_SM_Constant.PaletteEntryTag);
                _pStore[i].Save(n);
                node.AddNode(n);
            }
        }
    }
    #endregion

    #region Asset Management
    public enum ShaderOverlayMask { None, Explicit, UseDefaultTexture };

    [Serializable]
    public abstract class ShaderAbstract
    {
        [NonSerialized]
        protected TDebug dbg;

        [Persistent]
        public StringDict ParameterMap;

        [Persistent]
        public List<string> Keywords;

        // shader keyword to turn off any blending
        [Persistent]
        public string disableBlendUIfor;

        [Persistent]
        public bool useBlend = false;

        [Persistent]
        public int numColourAreas = 1;

        [Persistent]
        public Shader Shader;

        protected ShaderAbstract(Shader newShader)
        {
            Shader = newShader;
            Keywords = new List<string>();
            ParameterMap = new StringDict();

            dbg = new TDebug("[Shader] ");
        }

    }

    public class ShaderRecord : ShaderAbstract
    {
        public string[] Replace;

        public ShaderRecord(Shader newShader) : base(newShader)
        {
        }

        public bool isReplacementFor(string shaderNameToReplace)
        {
            for (int i = 0; i < Replace.Length; i++)
            {
   //             dbg.Print( Shader.name+ " isReplacementFor testing " + Replace[i] + " vs " + shaderNameToReplace);
                if (Replace[i] == shaderNameToReplace)
                    return true;
            }
            return false;
        }

        public void _dumpToLog()
        {
            if (Replace == null)
            {
                dbg.Print("No replacements found :(");
                Replace = new string[0];
            }
            var s = "Record " + Shader.name + ": Replaces: ";
            for (int i = 0; i < Replace.Length; i++)
                s += Replace[i] + " ";

            s += " useBlend: " + useBlend.ToString();
            dbg.Print(s);

        }

        public void Load(ConfigNode node)
        {
            Replace = node.GetValues("replace");

            var parameterStrings = node.GetValues("parameter");
            if (parameterStrings.Length > 0)
            {
                ParameterMap = new StringDict();
                for (int i = 0; i < parameterStrings.Length; i++)
                {
                    var parameterSplit = parameterStrings[i].Split(',');
                    ParameterMap.Add(parameterSplit[0].Trim(), parameterSplit[1].Trim());
                }
            }

            var shaderKeywords = node.GetValues("testForKeyword");
            if (shaderKeywords.Length > 0)
            {
 //               dbg.Print("Shader " + Shader.name + " has " + shaderKeywords.Length + " defined keyword tests.");
                Keywords = new List<string>(shaderKeywords);
            }
        }
    }


    // this one deals with managed shaders, IE ones which need the colour editing interface
    [Serializable]
    public class ShaderManaged : ShaderAbstract
    {
        protected Material managedMat;

        public ShaderManaged(Shader newShader) : base(newShader)
        { }

        public ShaderManaged( Material matManaged ) : base( matManaged.shader )
        {
            managedMat = matManaged;
        }

        public void UpdateShaderWith(Palette tintPalette)
        {
            // shader fields should be set up in Tint.cginc
            // parameter map is defined in attributes

            if (managedMat != null && tintPalette != null)
            {
                if (ParameterMap.Count == 0 )
                {
                    throw new Exception("ParameterMap is zero length for shader " + Shader.name);
                }


                // make sure section 1 entries are only copied to the first palette entry
                for (int i = 0; i < tintPalette.Length; i++)
                {
                    var paletteEntry = tintPalette[i];
                    foreach (var shaderParams in ParameterMap)
                    {                                                                                                                                                                                                                                                                                                                                 
 //                      dbg.Print("Palette["+i+"] " + shaderParams.Key + "->" + shaderParams.Value + ": "+paletteEntry.Output(shaderParams.Key));
                        var f = paletteEntry.Output(shaderParams.Key);
                        if(f != null)
                            managedMat.SetFloat(shaderParams.Value, (float)f);
                    }

                    if (i == 0 && numColourAreas == 1)
                        managedMat.SetColor("_Color", paletteEntry.Colour);
                    else
                        managedMat.SetColor("Color" + i, paletteEntry.Colour);
                }
            }
        }
    }

    // this one deals with replacing existing material settings, and adding new maps/replacing shaders/enabling shader features
    [Serializable]
    public class ShaderReplacementController : ShaderManaged, IConfigNode
    {
        [Persistent]
        [SerializeField]
        private StringDict Maps; // store EXTRA maps here unless you really want to replace a default map

        public ShaderReplacementController(Shader newShader) : base(newShader)
        {
            Maps = new StringDict();
        }

        public ShaderReplacementController(Material matToManage ) : base( matToManage )
        {
            Maps = new StringDict();
        }

        // well this is ugly, I'm really sure there's a better way
        public ShaderReplacementController( ShaderAbstract prefab ) : this( prefab.Shader )
        {
            if (prefab.ParameterMap != null)
                ParameterMap = prefab.ParameterMap;

            Shader = prefab.Shader;
            disableBlendUIfor = prefab.disableBlendUIfor;
            numColourAreas = prefab.numColourAreas;
            Keywords = prefab.Keywords;
            useBlend = prefab.useBlend;
        }

        public ShaderReplacementController( Material matToManage, ShaderAbstract prefab ) : this( prefab )
        {
            managedMat = matToManage;
        }

        private bool ShaderKeywordExists(string kw)
        {
            if (managedMat != null && managedMat.shader != null)
            {
                for (var i = 0; i < managedMat.shaderKeywords.Length; i++)
                    if (kw == managedMat.shaderKeywords[i])
                        return true;
            }
            return false;
        }

        public void doShaderSetup()
        { 
            managedMat.SetFloat("usableColours", numColourAreas);
            var mapKey = "";
            
            foreach (var mapEntry in Maps)
            {
                if (managedMat.HasProperty("_" + mapEntry.Key))
                {
                    dbg.Print("Setting texture " + mapEntry.Key + " for new shader mat");
                    var mat = GameDatabase.Instance.GetTexture(mapEntry.Value, false);
                    if (mat != null)
                        managedMat.SetTexture("_" + mapEntry.Key, mat);
                    else
                        Debug.LogError("GetTexture failed for " + mapEntry.Value + ", ignoring map insertion");
                }
                else
                    Debug.LogError("Attempted to set nonexistant map property " + mapEntry.Key + " in shader " + managedMat.shader.name);


                // shader keywords are uppercase versions of map parameters
                // that way if we try and set the map we get the correct shader functionality
                
                mapKey = mapEntry.Key.ToUpper();
 //               if (ShaderKeywordExists(mapKey))
 // I think shader.Keywords is set by EnableKeywords...
                {
                    dbg.Print("Attempting to enable keyword " + mapKey + " from keyword scan");
                    managedMat.EnableKeyword(mapKey);
                }

            }
            if (disableBlendUIfor != null && ShaderKeywordExists(disableBlendUIfor.ToUpper()))
            {
                dbg.Print("useBlend set to " + !managedMat.IsKeywordEnabled(disableBlendUIfor.ToUpper()));
                useBlend = !managedMat.IsKeywordEnabled(disableBlendUIfor.ToUpper());
            }
        }

        public void ReplaceShaderIn(Material materialForReplacement)
        {
            managedMat = materialForReplacement;

            // need to preload a list of checks to make sure we get the 
            // right replacement shader keywords even if there's no map replacement/addition
            var kwToEnable = new List<string>(Keywords.Count);

            for (int i = 0; i < Keywords.Count; i++)
            {
                //               dbg.Print("Shader " + Shader.name + " Testing for keyword " + Keywords[i]);
                if (managedMat.HasProperty("_" + Keywords[i]))
                {
                    kwToEnable.Add(Keywords[i]);
                }
            }

            dbg.Print("Replacing shader " + managedMat.shader.name + " with " + Shader.name);

            managedMat.shader = Shader;

            for (int i = 0; i < kwToEnable.Count; i++)
            {
                //               dbg.Print("Enabling keyword " + kwToEnable[i] + " for existing mat");
                managedMat.EnableKeyword(kwToEnable[i].ToUpper());
            }

            doShaderSetup();
        }

        private void splitMaps(string[] mapEntries)
        {
            for (int i = 0; i < mapEntries.Length; i++)
            {
                var mapSplit = mapEntries[i].Split(',');
                if (mapSplit[0] != null && mapSplit[1] != null)
                    Maps[mapSplit[0].Trim()] = mapSplit[1].Trim();
            }
        }

        public void Load(ConfigNode node)
        {
            var mapEntries = node.GetValues("Map");
            Load(mapEntries);
        }

        public void Load( string[] mapEntries )
        {
            if (mapEntries != null)
                splitMaps(mapEntries);
        }

        public void Save(ConfigNode node) // I don't exactly care about this!
        {
            foreach (var mapEntry in Maps)
            {
                node.AddValue("Map", mapEntry.Key + "," + mapEntry.Value);
            }
        }

   }

    /// <summary>
    /// Singleton manager of shader assets and management classes
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ShaderAssetManager : MonoBehaviour
    {
        private List<ShaderRecord> Shaders;
        private List<string> ManagedShaders;

        private static AssetManager AssetMgr;
        public static ShaderAssetManager instance;
        public static bool shadersLoaded = false;

        public void Awake()
        {
            Shaders = new List<ShaderRecord>();
            ManagedShaders = new List<string>();
        }

        private static void ParseAssetQueryResult( Dictionary<string,AssetRecord> assetResults )
        {
            foreach ( var assetRec in assetResults )
            {
                var idx = instance.GetShaderIndexFromString(assetRec.Value.Asset.name);
                if ( idx == -1 )
                {
                    var newShaderRec = new ShaderRecord(assetRec.Value.Asset as Shader);
                    if ( assetRec.Value.Attributes != null )
                    {
                        ConfigNode.LoadObjectFromConfig(newShaderRec, assetRec.Value.Attributes);
                        newShaderRec.Load(assetRec.Value.Attributes);
                        instance.Shaders.Add(newShaderRec);

                        instance.ManagedShaders.Add(newShaderRec.Shader.name);
                        newShaderRec._dumpToLog();
                    }
                }
            }
            
        }

        public static void OnBundleStateChange( BundleRecord b )
        {
            var bundleContents = AssetMgr.GetAssetsOfType<Shader>(b.BundleID);
            ParseAssetQueryResult(bundleContents);

            shadersLoaded = true;
        }

        public static void LoadShaders( KSPPaths bundlePath, string bundleFN, string bundleID)
        {
            var b = AssetManager.CreateModBundle(bundlePath, bundleFN, bundleID);
            b.OnFinalize += OnBundleStateChange;
            AssetMgr.LoadModBundle(b);
        }

        private int GetShaderIndexFromString( string shaderName )
        {
            for (int i = 0; i < Shaders.Count; i++)
                if (Shaders[i].Shader.name == shaderName)
                    return i;

            return -1;
        }
        public ShaderReplacementController GetShader(string shaderName)
        {
            var idx = GetShaderIndexFromString(shaderName);
            if (idx >= 0)
                return new ShaderReplacementController(Shaders[idx]);
            return null;
        }

        public ShaderReplacementController GetShader(Material matToManage)
        {
            var idx = GetShaderIndexFromString(matToManage.shader.name);
            if (idx >= 0)
                return new ShaderReplacementController(matToManage, Shaders[idx] );
            return null;
        }

        public ShaderReplacementController GetReplacementShaderFor(string shaderNameToReplace)
        {
            for (int i = 0; i < Shaders.Count; i++)
                if (Shaders[i].isReplacementFor(shaderNameToReplace))
                    return new ShaderReplacementController(Shaders[i]);

            return null;
        }

        public bool IsManagedShader(string shaderName)
        {
            return ManagedShaders.Contains(shaderName);
        }


        private static IEnumerator FetchAllLoadedShaders()
        {
            while (HighLogic.LoadedScene == GameScenes.LOADING)
                yield return HighLogic.LoadedScene;
            // grab all shader objects from assetmgr
            ParseAssetQueryResult(AssetMgr.GetAssetsOfType<Shader>());
        }

        public void Start()
        {
            AssetMgr = AssetManager.instance;
            instance = this;

            StartCoroutine(FetchAllLoadedShaders());
        }
    }
#endregion
}
