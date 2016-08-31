using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DLTD.Utility
{
    public static class DLTD_Utility_AssetManagment_Constant
    {
        public const string pathSep = "/";
        public const int bundleUnloadFramecount = 30;
    }

    public class KSPPaths
    {
        public string Base;
        public string LocalDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("\\", DLTD_Utility_AssetManagment_Constant.pathSep); }
        }
           
        private string _modsub;
        public string Mod
        {
            get { return Base + DLTD_Utility_AssetManagment_Constant.pathSep + "GameData" + DLTD_Utility_AssetManagment_Constant.pathSep +  _modsub; }
            set { _modsub = value; }
        }

        public string ModRelative
        {
            get { return _modsub; }
        }

        private string _pdLivesIn = "";
        public string PluginDataIsBelow
        {
            set { _pdLivesIn = value + DLTD_Utility_AssetManagment_Constant.pathSep; }
        }

        public string PluginData
        {
            get { return Mod + DLTD_Utility_AssetManagment_Constant.pathSep + _pdLivesIn + "PluginData"; }
        }

        public string Packages
        {
            get { return Mod + DLTD_Utility_AssetManagment_Constant.pathSep + "Packages"; }
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

    public enum BundleState {  Unloaded, BundleLoading, AssetLoading, AttributesLoading, BundleWaitingForUnload, BundleReadyForUnload, Final }
    public delegate void BundleStateChangeEvent(BundleRecord b);

    public class BundleRecord
    {
        public BundleStateChangeEvent BundleStateChanged;

        public virtual void OnStateChange()
        {
            BundleStateChanged?.Invoke(this);
        }

        private BundleState _state;
        public BundleState state
        {
            get { return _state; }
            set {
                _state = value;
                OnStateChange();
            }
        }

        public bool Finalized
        {
            get { return _state == BundleState.Final; }
        }

        public string BundleID;
        public string BundleLoc;
        public int TTL = DLTD_Utility_AssetManagment_Constant.bundleUnloadFramecount;

        public string BundleLocForWWW
        {
            get { return "file:///" + BundleLoc; }
        }
        public UnityEngine.Object[] Assets;
        public ConfigNode Attributes;

        public BundleRecord( string loc, string b_ID = "root", BundleState initialState = BundleState.Unloaded )
        {
            BundleID = b_ID;
            BundleLoc = loc.Replace("\\", DLTD_Utility_AssetManagment_Constant.pathSep);
            state = initialState;
            TTL = DLTD_Utility_AssetManagment_Constant.bundleUnloadFramecount;
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class AssetManager : MonoBehaviour
    {

        TDebug dbg;
        // do some error checking please...

        private const string attributeTag = "ASSET_ATTRIBUTE";
        private const string attributeExt = ".atr";

        public Dictionary<string, AssetRecord> Assets;
        public Dictionary<string, BundleRecord> AssetsByBundleID;
        public List<BundleRecord> UnloadQueue;

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

  //                         dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " loaded from disk, preparing to load assets.");

                var bundle = www.assetBundle;
                bundleRec.state = BundleState.AssetLoading;
                var assetsLoadRequest = bundle.LoadAllAssetsAsync();

                yield return assetsLoadRequest;

  //                         dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " assets loaded.");

                bundleRec.Assets = assetsLoadRequest.allAssets;

                AssetsByBundleID[bundleRec.BundleID] = bundleRec;

                for (int i = 0; i < bundleRec.Assets.Length; i++)
                {
                    var assetName = bundleRec.Assets[i].name;
                    var assetRec = new AssetRecord(bundleRec.Assets[i], bundleRec.BundleID);
                    //                dbg.Print("LoadAssetBundle: " + n);
                    Assets[assetName] = assetRec;
                }

                bundleRec.state = BundleState.AttributesLoading;
                LoadAssetAttributes(bundleRec);

                bundleRec.state = BundleState.BundleWaitingForUnload;

                UnloadQueue.Add(bundleRec);

                while (bundleRec.state != BundleState.BundleReadyForUnload)
                    yield return bundleRec;
                dbg.Print("LoadAssetBundle: unloading bundle " + bundleRec.BundleID);
                bundle.Unload(false);
                bundleRec.state = BundleState.Final;
            }
        }



        private void LoadAssetAttributes( BundleRecord bundleRec)
        {
            var cfgFile = bundleRec.BundleLoc + attributeExt;

            if (!File.Exists(cfgFile))
                return;

  //          dbg.Print("Loading attributes for bundle " + bundleRec.BundleID + " from " + cfgFile);
            bundleRec.Attributes = ConfigNode.Load(cfgFile);

            foreach (ConfigNode node in bundleRec.Attributes.GetNodes(attributeTag))
            {
                var name = node.GetValue("name");
  //              dbg.Print("Attribute loader looking for [" + name + "]");
                if (Assets.ContainsKey(name))
                {
   //                 dbg.Print("Attribute loader found " + name);
                    Assets[name].Attributes = node;
                }
            }

        }

        public BundleRecord LoadModBundle( BundleRecord bundleRec )
        {
            StartCoroutine(LoadAssetBundle(bundleRec));
            //          StartCoroutine(LoadAssetAttributes(bundleRec));
            return bundleRec;
        }

        public BundleRecord LoadModBundle( KSPPaths modPaths, string bundleFN, string bundleID, BundleStateChangeEvent bundleCB = null )
        {
 //           dbg.Print("LoadModBundle: " + modPaths.Mod + " " + bundleFN + " " + bundleID);
            var bundleRec = new BundleRecord(modPaths.Packages + DLTD_Utility_AssetManagment_Constant.pathSep + bundleFN, bundleID);
            bundleRec.BundleStateChanged += bundleCB;
            return LoadModBundle(bundleRec);
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
            UnloadQueue = new List<BundleRecord>();

            dbg = new TDebug("[DLTD AssetManager] ");

            DontDestroyOnLoad(this);
        }

        public void Start()
        {
            Caching.CleanCache();
            instance = this;
        }

        public void FixedUpdate()
        {
            if( UnloadQueue.Count > 0 )
                for( int i = 0; i < UnloadQueue.Count; i++ )
                    if( UnloadQueue[i].TTL == 0 )
                    {
                        UnloadQueue[i].state = BundleState.BundleReadyForUnload;
                        UnloadQueue[i].TTL = DLTD_Utility_AssetManagment_Constant.bundleUnloadFramecount;
                        UnloadQueue.RemoveAt(i);
                        if (UnloadQueue.Count == 0)
                            UnloadQueue.TrimExcess();
                    }
                    else
                    {
                        UnloadQueue[i].TTL--;
                    }
           
        }
    }
}
