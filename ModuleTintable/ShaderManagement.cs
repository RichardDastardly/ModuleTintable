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
        [Persistent]
        public string[] Replace;

        [Persistent]
        public Dictionary<string, string> ParameterMap;

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
        }
    }

    public class ShaderRecord : ShaderAbstract
    {
        public ShaderRecord(Shader newShader) : base(newShader)
        {
        }

        public bool isReplacementFor(string shaderNameToReplace)
        {
            for (int i = 0; i < Replace.Length; i++)
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
            var s = "Record " + Shader.name + ": Replaces: ";
            for (int i = 0; i < Replace.Length; i++)
                s += Replace[i] + " ";

            s += " useBlend: " + useBlend.ToString();
            s += " shadingOverlayType: " + shadingOverlayType.ToString();
            TDebug.Print(s);

        }
    }

    public class TranslatedPaletteEntry : PaletteEntry
    {
        public new float this[string key]
        {
            get { return Mathf.Clamp01(_Settings[key] / 255); }
        }
    }

    // this should be instanciated by ModuleTintable in shader replacement mode to hold records for shader objects attached to gameobjects
    // Module should keep a toplevel default one & have it linked to individual gameobjects
    // unless there's an alternative specified
    // this should probably be called Material & hold a shaderobject instead of being a descendent

    public class ShaderGameObject : ShaderAbstract, IConfigNode
    {

        [Persistent]
        public Dictionary<string, string> Maps; // store EXTRA maps here unless you really want to replace a default map

        private Material managedMat;

        public ShaderGameObject(Shader newShader) : base(newShader)
        {
            Maps = new Dictionary<string, string>();
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

            managedMat.shader = Shader;

            var mapKey = "";

            foreach (var mapEntry in Maps)
            {
                if (managedMat.HasProperty(mapEntry.Key))
                    managedMat.SetTexture("_" + mapEntry.Key, GameDatabase.Instance.GetTexture(mapEntry.Value, false));
                else
                    Debug.LogError("Attempted to set map property " + mapEntry.Key + " in shader " + managedMat.shader.name);


                // shader keywords are uppercase versions of map parameters
                // that way if we try and set the map we get the correct shader functionality
                mapKey = mapEntry.Key.ToUpper();
                if (shaderKeywordExists(mapKey))
                    managedMat.EnableKeyword(mapKey);

            }
            if (shaderKeywordExists(disableBlendUIfor.ToUpper()))
                useBlend = !managedMat.IsKeywordEnabled(disableBlendUIfor.ToUpper());
        }

        private static float SliderToShaderValue(float v)
        {
            return v / 255;
        }

        public void UpdateShaderWith( Palette tintPalette )
        {
            // shader fields should be set up in Tint.cginc
            // parameter map is defined in attributes

            if (managedMat != null && tintPalette != null)
            {
                if (ParameterMap == null)
                {
                    throw new Exception("ParameterMap is null for shader " + Shader.name);
                }

                //if (useBlend)
                //{
                //    var paletteEntry = modTint.Palette[0] as TranslatedPaletteEntry;
                //    managedMat.SetFloat("_TintPoint", paletteEntry["tintBlendPoint"]);
                //    managedMat.SetFloat("_TintBand", paletteEntry["tintBlendBand"]);

                //    var tintFalloff = paletteEntry["tintFalloff"];
                //    managedMat.SetFloat("_TintFalloff", tintFalloff > 0 ? tintFalloff : 0.001f);

                //    var tintSatThreshold = paletteEntry["tintBlendSaturationThreshold"];
                //    managedMat.SetFloat("_TintSatThreshold", tintSatThreshold);

                //    var saturationFalloff = Mathf.Clamp01(tintSatThreshold * 0.75f);
                //    managedMat.SetFloat("_SaturationFalloff", saturationFalloff);
                //    managedMat.SetFloat("_SaturationWindow", tintSatThreshold - saturationFalloff);

                //}

                // make sure section 1 entries are only copied to the first palette entry
                for (int i = 0; i < tintPalette.Length; i++)
                {
                    var paletteEntry = tintPalette[i] as TranslatedPaletteEntry;
                    foreach (KeyValuePair<string, string> shaderParams in ParameterMap)
                    {
                        managedMat.SetFloat(shaderParams.Value, paletteEntry[shaderParams.Key]);
                    }

                    managedMat.SetColor("_Color", paletteEntry.Colour);
//                    managedMat.SetFloat("_GlossMult", paletteEntry["tintGloss"]);
                }
            }
        }

        public void Load(ConfigNode node)
        {
            var mapEntries = node.GetValues("Map");
            for (int i = 0; i < mapEntries.Length; i++)
            {
                var mapSplit = mapEntries[i].Split(',');
                if (mapSplit[0] != null && mapSplit[1] != null)
                    Maps[mapSplit[0].Trim()] = mapSplit[1].Trim();
            }
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
                if (assetRec.Attributes != null)
                {
                    var newShaderRec = new ShaderRecord(assetRec.Asset as Shader);
                    ConfigNode.LoadObjectFromConfig(newShaderRec, assetRec.Attributes);
                    newShaderRec.Replace = assetRec.Attributes.GetValues("replace");

                    var parameterStrings = assetRec.Attributes.GetValues("parameter");
                    if (parameterStrings.Length > 0) {
                        newShaderRec.ParameterMap = new Dictionary<string, string>(parameterStrings.Length);
                        for (int i = 0; i < parameterStrings.Length; i++)
                        {
                            var parameterSplit = parameterStrings[i].Split(',');
                            newShaderRec.ParameterMap.Add(parameterSplit[0].Trim(), parameterSplit[1].Trim());
                        }
                    }
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

        public ShaderRecord GetReplacementShaderFor(string shaderNameToReplace)
        {
            for (int i = 0; i < Shaders.Count; i++)
                if (Shaders[i].isReplacementFor(shaderNameToReplace))
                    return Shaders[i];

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
