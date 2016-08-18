using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DLTD.Modules;

namespace DLTD.Utility
{
    #region Asset Management
    public enum ShaderOverlayMask { None, Explicit, UseDefaultTexture };

    public abstract class ShaderAbstract
    {
        public Dictionary<string, string> ParameterMap;

        public List<string> Keywords;

        [Persistent]
        public string disableBlendUIfor;

        [Persistent]
        public bool useBlend = false;

        [Persistent]
        public ShaderOverlayMask shadingOverlayType = ShaderOverlayMask.None;

        [Persistent]
        public int numColourAreas = 1;

        public Shader Shader;

        protected ShaderAbstract(Shader newShader)
        {
            Shader = newShader;
            Keywords = new List<string>();
            ParameterMap = new Dictionary<string, string>();
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
   //             TDebug.Print( Shader.name+ " isReplacementFor testing " + Replace[i] + " vs " + shaderNameToReplace);
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
            var s = "Record " + Shader.name + ": Replaces: ";
            for (int i = 0; i < Replace.Length; i++)
                s += Replace[i] + " ";

            s += " useBlend: " + useBlend.ToString();
            s += " shadingOverlayType: " + shadingOverlayType.ToString();
            TDebug.Print(s);

        }

        public void Load(ConfigNode node)
        {
            Replace = node.GetValues("replace");

            var parameterStrings = node.GetValues("parameter");
            if (parameterStrings.Length > 0)
            {
                ParameterMap = new Dictionary<string, string>(parameterStrings.Length);
                for (int i = 0; i < parameterStrings.Length; i++)
                {
                    var parameterSplit = parameterStrings[i].Split(',');
                    ParameterMap.Add(parameterSplit[0].Trim(), parameterSplit[1].Trim());
                }
            }

            var shaderKeywords = node.GetValues("testForKeyword");
            if (shaderKeywords.Length > 0)
            {
 //               TDebug.Print("Shader " + Shader.name + " has " + shaderKeywords.Length + " defined keyword tests.");
                Keywords = new List<string>(shaderKeywords);
            }
        }
    }


    // this one deals with managed shaders, IE ones which need the colour editing interface
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
                if (ParameterMap == null)
                {
                    throw new Exception("ParameterMap is null for shader " + Shader.name);
                }

                // make sure section 1 entries are only copied to the first palette entry
                for (int i = 0; i < tintPalette.Length; i++)
                {
                    var paletteEntry = tintPalette[i];
                    foreach (var shaderParams in ParameterMap)
                    {                                                                                                                                                                                                                                                                                                                                 
 //                       TDebug.Print("Palette["+i+"] " + shaderParams.Key + "->" + shaderParams.Value + ": "+paletteEntry.GetForShader(shaderParams.Key));
                        var f = paletteEntry.GetForShader(shaderParams.Key);
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
    public class ShaderReplacementController : ShaderManaged, IConfigNode
    {
        private Dictionary<string, string> Maps; // store EXTRA maps here unless you really want to replace a default map

        public ShaderReplacementController(Shader newShader) : base(newShader)
        {
            Maps = new Dictionary<string, string>();
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

        private bool shaderKeywordExists(string kw)
        {
            if (managedMat != null && managedMat.shader != null)
            {
                for (var i = 0; i < managedMat.shaderKeywords.Length; i++)
                    if (kw == managedMat.shaderKeywords[i])
                        return true;
            }
            return false;
        }

        public void ReplaceShaderIn(Material materialForReplacement)
        {
            managedMat = materialForReplacement;
            var mapKey = "";

            // need to preload a list of checks to make sure we get the 
            // right replacement shader keywords even if there's no map replacement/addition
            var kwToEnable = new List<string>(Keywords.Count);

            for (int i = 0; i < Keywords.Count; i++)
            {
 //               TDebug.Print("Shader " + Shader.name + " Testing for keyword " + Keywords[i]);
                if (managedMat.HasProperty("_" + Keywords[i]))
                {
                    kwToEnable.Add(Keywords[i]);
                }
            }

            TDebug.Print("Replacing shader " + managedMat.shader.name + " with " + Shader.name);

            managedMat.shader = Shader;

            for (int i = 0; i < kwToEnable.Count; i++)
            {
 //               TDebug.Print("Enabling keyword " + kwToEnable[i] + " for existing mat");
                managedMat.EnableKeyword(kwToEnable[i].ToUpper());
            }

            managedMat.SetFloat("usableColours", numColourAreas);

            foreach (var mapEntry in Maps)
            {
                if (managedMat.HasProperty("_" + mapEntry.Key))
                {
                    TDebug.Print("Setting texture " + mapEntry.Key + " for new shader mat");
                    var mat = GameDatabase.Instance.GetTexture(mapEntry.Value, false);
                    if (mat != null)
                        managedMat.SetTexture("_" + mapEntry.Key, mat);
                    else
                        Debug.LogError("GetTexture failed for " + mapEntry.Value + ", ignoring map insertion");
                }
                else
                    Debug.LogError("Attempted to set map property " + mapEntry.Key + " in shader " + managedMat.shader.name);


                // shader keywords are uppercase versions of map parameters
                // that way if we try and set the map we get the correct shader functionality
                
                mapKey = mapEntry.Key.ToUpper();
 //               if (shaderKeywordExists(mapKey))
 // I think shader.Keywords is set by EnableKeywords...
                {
                    TDebug.Print("Attempting to enable keyword " + mapKey + " from keyword scan");
                    managedMat.EnableKeyword(mapKey);
                }

            }
            if (disableBlendUIfor != null && shaderKeywordExists(disableBlendUIfor.ToUpper()))
            {
                TDebug.Print("useBlend set to " + !managedMat.IsKeywordEnabled(disableBlendUIfor.ToUpper()));
                useBlend = !managedMat.IsKeywordEnabled(disableBlendUIfor.ToUpper());
            }
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
            if (mapEntries != null)
                splitMaps(mapEntries);
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


    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ShaderAssetManager : MonoBehaviour
    {
        private List<ShaderRecord> Shaders;
        private List<string> ManagedShaders;
        private KSPPaths ModuleTintablePaths;

        //        private readonly string ShaderBundle = "DLTDTintableShaders";
        private readonly string ShaderBundle = "dltdtintableshaders"; // unfortunately unity seems to want to save bundles in lowercase
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
                if (assetRec.Attributes != null)
                {
                    var newShaderRec = new ShaderRecord(assetRec.Asset as Shader);
                    ConfigNode.LoadObjectFromConfig(newShaderRec, assetRec.Attributes);
                    newShaderRec.Load(assetRec.Attributes);
                    Shaders.Add(newShaderRec);

                    if (ManagedShaders == null)
                        ManagedShaders = new List<string>();
                    ManagedShaders.Add(newShaderRec.Shader.name);

                    //TDebug.Print("ShaderAssetManager added " + newShaderRec.Shader.name);
                    newShaderRec._dumpToLog();
                }
            }
            shadersLoaded = true;
        }

        public ShaderReplacementController GetShader( string shaderName )
        {
            for (int i = 0; i < Shaders.Count; i++)
                if (Shaders[i].Shader.name == shaderName)
                    return new ShaderReplacementController(Shaders[i]);

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

        public void Start()
        {
            AssetMgr = AssetManager.instance;
            instance = this;

            StartCoroutine(LoadShaders());

        }
    }
#endregion
}
