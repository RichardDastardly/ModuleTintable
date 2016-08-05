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

        public KSPPaths( string m = null, string pdl = null )
        {
            Base = Path.GetDirectoryName(KSPUtil.ApplicationRootPath);
            Mod = m;
            PluginDataIsBelow = pdl;
        }
    }


    public class AssetRecord
    {
        public string BundleID;
        public UnityEngine.Object Asset;
        public ConfigNode Attributes;

        public AssetRecord( UnityEngine.Object a, string b = "root" )
        {
            Asset = a;
            BundleID = b;
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
            BundleLoc = loc;
            state = initialState;
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class AssetManager : MonoBehaviour
    {
        
        // do some error checking please...

        public Dictionary<string, AssetRecord> Assets;
        public Dictionary<string, BundleRecord> AssetsByBundleID;

        public static AssetManager instance;

        private IEnumerator LoadAssetBundle( BundleRecord b )
        {
            while (!Caching.ready)
                yield return null;

//            var bundleLoadRequest = AssetBundle.LoadFromFileAsync(b.BundleLoc);


            b.state = BundleState.BundleLoading;
            var www = WWW.LoadFromCacheOrDownload(b.BundleLocForWWW, 1);

            yield return www;

            if(!string.IsNullOrEmpty(www.error))
            {
                Debug.Log(www.error);
                yield break;
            }

 //           TDebug.Print("LoadAssetBundle: bundle " + b.BundleID + " loaded from disk, preparing to load assets.");

            var bundle = www.assetBundle;
            b.state = BundleState.AssetLoading;
            var assetsLoadRequest = bundle.LoadAllAssetsAsync();

            yield return assetsLoadRequest;

 //           TDebug.Print("LoadAssetBundle: bundle " + b.BundleID + " assets loaded.");

            b.Assets = assetsLoadRequest.allAssets;

            AssetsByBundleID[b.BundleID] = b;

            for (int i = 0; i < b.Assets.Length; i++)
            {
                var n = b.Assets[i].name;
                var a = new AssetRecord(b.Assets[i], b.BundleID);
//                TDebug.Print("LoadAssetBundle: " + n);
                Assets[n] = a;
            }

            b.state = BundleState.Loaded;
            bundle.Unload(false);

        }

        private IEnumerator LoadAssetAttributes( KSPPaths p, BundleRecord b )
        {
            while ( b.state != BundleState.Loaded )
                yield return null;

            var cfgFile = p.Packages + Path.DirectorySeparatorChar + b.BundleID + ".atr";
            if (File.Exists(cfgFile))
            {
                b.Attributes = ConfigNode.Load(cfgFile);

                foreach (ConfigNode n in b.Attributes.GetNodes())
                    if (Assets.ContainsKey(n.name))
                        Assets[n.name].Attributes = n;
            }
        }

        public BundleRecord LoadModBundle( KSPPaths p, string bundleFN, string bundleID )
        {
   //         TDebug.Print("LoadModBundle: " + p.Mod + " " + bundleFN + " " + bundleID);
            var b = new BundleRecord(p.Packages + "/" + bundleFN.Replace("\\", "/"), bundleID);
            StartCoroutine(LoadAssetBundle(b));
            StartCoroutine(LoadAssetAttributes(p, b));
            return b;
        }

        public Dictionary<string,T> GetRawAssetsOfTypeBundledIn<T>( string bundleID ) where T : UnityEngine.Object
        {
            if( AssetsByBundleID.ContainsKey( bundleID ))
            {
                var r = new Dictionary<string, T>();
                var c = AssetsByBundleID[bundleID].Assets;
                for (int i = 0; i < c.Length; i++)
                    if (c[i].GetType() == typeof(T))
                        r[c[i].name] = (T)c[i];
                return r;
            }
            return null;
        }

        public Dictionary<string,AssetRecord> GetAssetsOfType<T>( string b_ID = null ) where T : UnityEngine.Object
        {
  //          TDebug.Print("GetAssetsOfType: getting entries of type " + typeof(T).ToString());
            var l = new Dictionary<string,AssetRecord>();
            foreach (var k in Assets.Keys)
            {
 //               TDebug.Print("GetAssetsOfType: asset " + Assets[k].Asset.name + " type " +Assets[k].Asset.GetType().ToString());
                if ((Assets[k].Asset.GetType() == typeof(T) ) && (b_ID != null && b_ID == Assets[k].BundleID))
                    l[k] = Assets[k];
            }
            return l;
        }


        public void Awake()
        {
            Assets = new Dictionary<string, AssetRecord>();
            AssetsByBundleID = new Dictionary<string, BundleRecord>();


            DontDestroyOnLoad(this);
        }

        public void Start()
        {
            Caching.CleanCache();
            instance = this;
        }
    }
}
