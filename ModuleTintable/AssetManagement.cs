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
            get { return Base + "/" + _modsub; }
            set { _modsub = value; }
        }
        private string _pdLivesIn = "";
        public string PluginDataIsBelow
        {
            set { _pdLivesIn = value + "/"; }
        }

        public string PluginData
        {
            get { return Mod + "/" + _pdLivesIn + "PluginData"; }
        }

        public KSPPaths( string m = null, string pdl = null )
        {
            Base = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location).Replace("\\","/");
            Mod = m;
            PluginDataIsBelow = pdl;
        }
    }


    class AssetRecord
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

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class AssetManager : MonoBehaviour
    {
        
        // do some error checking please...

        public Dictionary<string, AssetRecord> Assets;
        public Dictionary<string, UnityEngine.Object[]> AssetsByBundleID;
        private string bundleLoaded = null;

        public static AssetManager instance;

        private IEnumerator LoadAssetBundle( string loc, string bundleID = "root" )
        {
            while (!Caching.ready)
                yield return null;

            using (WWW www = WWW.LoadFromCacheOrDownload(loc, 1))
            {
                var bundle = www.assetBundle;

                AssetsByBundleID[bundleID] = bundle.LoadAllAssets();

                for( int i = 0; i < AssetsByBundleID[bundleID].Length; i++)
                {
                    var n = AssetsByBundleID[bundleID][i].name;
                    var a = new AssetRecord(AssetsByBundleID[bundleID][i], bundleID );
                    Assets[n] = a;
                    yield return www;
                }

                bundleLoaded = bundleID;
                bundle.Unload(false);
            }
        }

        private IEnumerator LoadAssetAttributes( KSPPaths p, string bundleID )
        {
            while (bundleLoaded != bundleID)
                yield return null;

            var cfgFile = p.PluginData + "/" + bundleID + ".atr";
            if (File.Exists(cfgFile))
            {
                var cfg = ConfigNode.Load(cfgFile);
                foreach (ConfigNode n in cfg.GetNodes())
                    if (Assets.ContainsKey(n.name))
                        Assets[n.name].Attributes = n;
            }
        }

        public void LoadModBundle( KSPPaths p, string bundleFN, string bundleID )
        {
            bundleLoaded = null;
            StartCoroutine(LoadAssetBundle(p.Mod + "/" + bundleFN.Replace("\\", "/"), bundleID));
            StartCoroutine(LoadAssetAttributes(p, bundleID));
        }

        public Dictionary<string,T> GetRawAssetsOfTypeBundledIn<T>( string bundleID ) where T : UnityEngine.Object
        {
            if( AssetsByBundleID.ContainsKey( bundleID ))
            {
                var r = new Dictionary<string, T>();
                var c = AssetsByBundleID[bundleID];
                for (int i = 0; i < c.Length; i++)
                    if (c[i].GetType() == typeof(T))
                        r[c[i].name] = (T)c[i];
                return r;
            }
            return null;
        }

        public Dictionary<string,AssetRecord> GetAssetsOfType<T>( string b_ID = null ) where T : UnityEngine.Object
        {
            var l = new Dictionary<string,AssetRecord>();
            foreach (var k in Assets.Keys)
                if (Assets[k].Asset.GetType() == typeof(T) && ( b_ID != null && b_ID == Assets[k].BundleID ))
                     l[k] = Assets[k];
            return l;
        }


        public void Awake()
        {
            Assets = new Dictionary<string, AssetRecord>();
            AssetsByBundleID = new Dictionary<string, UnityEngine.Object[]>();


            DontDestroyOnLoad(this);
        }

        public void Start()
        {
            Caching.CleanCache();
            instance = this;
        }
    }
}
