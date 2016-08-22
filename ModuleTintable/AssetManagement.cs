using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DLTD.Utility
{
    public class KSPPaths
    {
        public string Base;
        public string LocalDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("\\","/" ); }
        }
           
        private string _modsub;
        public string Mod
        {
            get { return Base + "/" + "GameData" + "/" +  _modsub; }
            set { _modsub = value; }
        }

        public string ModRelative
        {
            get { return _modsub; }
        }

        private string _pdLivesIn = "";
        public string PluginDataIsBelow
        {
            set { _pdLivesIn = value + Path.DirectorySeparatorChar; }
        }

        public string PluginData
        {
            get { return Mod + "/" + _pdLivesIn + "PluginData"; }
        }

        public string Packages
        {
            get { return Mod + "/" + "Packages"; }
        }

        public KSPPaths( string modName = null, string pdl = null )
        {
            Base = Path.GetDirectoryName(KSPUtil.ApplicationRootPath);
            Mod = modName;
            PluginDataIsBelow = pdl;
        }
    }


    public class AssetRecord
    {
        public string BundleID;
        public UnityEngine.Object Asset;
        public ConfigNode Attributes;

        public AssetRecord( UnityEngine.Object asset, string b_ID = "root" )
        {
            Asset = asset;
            BundleID = b_ID;
        }
    }

    public enum BundleState {  Unloaded, BundleLoading, AssetLoading, Loaded }

    public class BundleRecord
    {
        public BundleState state;
        public string BundleID;
        public string BundleLoc;
        public string BundleLocForWWW
        {
            get { return "file:///" + BundleLoc; }
        }
        public UnityEngine.Object[] Assets;
        public ConfigNode Attributes;

        public BundleRecord( string loc, string b_ID = "root", BundleState initialState = BundleState.Unloaded )
        {
            BundleID = b_ID;
            BundleLoc = loc.Replace("\\", "/");
            state = initialState;
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class AssetManager : MonoBehaviour
    {

        TDebug dbg;
        // do some error checking please...

        public Dictionary<string, AssetRecord> Assets;
        public Dictionary<string, BundleRecord> AssetsByBundleID;

        public static AssetManager instance;

        private IEnumerator LoadAssetBundle( BundleRecord bundleRec )
        {
            while (!Caching.ready)
                yield return null;

            //            var bundleLoadRequest = AssetBundle.LoadFromFileAsync(b.BundleLoc);
            //            var bundle = AssetBundle.CreateFromFile(b.BundleLoc);

            bundleRec.state = BundleState.BundleLoading;
            using (WWW www = new WWW(bundleRec.BundleLocForWWW))
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.Log(www.error);
                    yield break;
                }

                //           dbg.Print("LoadAssetBundle: bundle " + b.BundleID + " loaded from disk, preparing to load assets.");

                var bundle = www.assetBundle;
                bundleRec.state = BundleState.AssetLoading;
                var assetsLoadRequest = bundle.LoadAllAssetsAsync();

                yield return assetsLoadRequest;

                //           dbg.Print("LoadAssetBundle: bundle " + b.BundleID + " assets loaded.");

                bundleRec.Assets = assetsLoadRequest.allAssets;

                AssetsByBundleID[bundleRec.BundleID] = bundleRec;

                for (int i = 0; i < bundleRec.Assets.Length; i++)
                {
                    var assetName = bundleRec.Assets[i].name;
                    var assetRec = new AssetRecord(bundleRec.Assets[i], bundleRec.BundleID);
                    //                dbg.Print("LoadAssetBundle: " + n);
                    Assets[assetName] = assetRec;
                }

                bundleRec.state = BundleState.Loaded;
                bundle.Unload(false);
            }
        }

        private string attributeTag = "ASSET_ATTRIBUTE";

        private IEnumerator LoadAssetAttributes( BundleRecord bundleRec)
        {
            while (bundleRec.state != BundleState.Loaded )
                yield return null;

            var cfgFile = bundleRec.BundleLoc + ".atr";
            //dbg.Print("Loading attributes for bundle " + b.BundleID + " from " + cfgFile);

            if (File.Exists(cfgFile))
            {
                //dbg.Print(cfgFile + " exists, loading...");
                bundleRec.Attributes = ConfigNode.Load(cfgFile);

                foreach (ConfigNode node in bundleRec.Attributes.GetNodes(attributeTag))
                {
                    var name = node.GetValue("name");
                    dbg.Print("Attribute loader looking for [" + name + "]");
                    if (Assets.ContainsKey(name))
                    {
                        dbg.Print("Attribute loader found " + name);
                        Assets[name].Attributes = node;
                    }
                }
            }
        }

        public BundleRecord LoadModBundle( KSPPaths modPaths, string bundleFN, string bundleID )
        {
   //         dbg.Print("LoadModBundle: " + p.Mod + " " + bundleFN + " " + bundleID);
            var bundleRec = new BundleRecord(modPaths.Packages + "/" + bundleFN, bundleID);
            StartCoroutine(LoadAssetBundle(bundleRec));
            StartCoroutine(LoadAssetAttributes(bundleRec));
            return bundleRec;
        }

        public Dictionary<string,T> GetRawAssetsOfTypeBundledIn<T>( string bundleID ) where T : UnityEngine.Object
        {
            if( AssetsByBundleID.ContainsKey( bundleID ))
            {
                var rawAssets = new Dictionary<string, T>();
                var assetList = AssetsByBundleID[bundleID].Assets;
                for (int i = 0; i < assetList.Length; i++)
                    if (assetList[i].GetType() == typeof(T))
                        rawAssets[assetList[i].name] = (T)assetList[i];
                return rawAssets;
            }
            return null;
        }

        public Dictionary<string,AssetRecord> GetAssetsOfType<T>( string b_ID = null ) where T : UnityEngine.Object
        {
  //          dbg.Print("GetAssetsOfType: getting entries of type " + typeof(T).ToString());
            var assetRecords = new Dictionary<string,AssetRecord>();
            foreach (var k in Assets.Keys)
            {
 //               dbg.Print("GetAssetsOfType: asset " + Assets[k].Asset.name + " type " +Assets[k].Asset.GetType().ToString());
                if ((Assets[k].Asset.GetType() == typeof(T) ) && (b_ID != null && b_ID == Assets[k].BundleID))
                    assetRecords[k] = Assets[k];
            }
            return assetRecords;
        }


        public void Awake()
        {
            Assets = new Dictionary<string, AssetRecord>();
            AssetsByBundleID = new Dictionary<string, BundleRecord>();

            dbg = new TDebug("[DLTD AssetManager] ");

            DontDestroyOnLoad(this);
        }

        public void Start()
        {
            Caching.CleanCache();
            instance = this;
        }
    }
}
