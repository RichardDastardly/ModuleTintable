using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// This whole file due a revision - use existing KSP classes where possible

namespace DLTD.Utility
{
    /// <summary>
    /// DLTD_Utilities_AssetManagement_Constant - constants for AssetManagement in DLTD.Utility namespace
    /// </summary>
    public static class DLTD_U_AM_Constant
    {
        public const string pathSep = "/";
        public const int bundleUnloadFramecount = 30;
        public const string mfg = "DLTD";
        public const string preloadBundleDir = "Preload"; // in global Packages
    }

     /*
     * Structures:
     *  Base: base game dir
     *  GameData: base + "GameData"
     *  MfgDir: GameData + mod manufacturer
     *  Mod: MfgDir + mod base - may be null
     *  
     *  Assets: for bundles/generic
     *  |- Packages: asset bundles and attributes
     *  
     *  Parts: anything KSP classifies as a part
     *  Plugins: dll
     *  |- PluginData: dll runtime data
     * 
     * Paths start at MfgDir unless otherwise noted
     */
    public class KSPPaths
    {
        public static string BuildPath( params string [] pathMembers )
        {
            return string.Join(DLTD_U_AM_Constant.pathSep.ToString(), pathMembers);
        }

        public static string Base;
        public string LocalDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("\\", DLTD_U_AM_Constant.pathSep); }
        }

        public static string FullPath( string pathRelative )
        {
            return BuildPath(Base, pathRelative);
        }

        public static string GameData
        {
            get { return "GameData"; }
        }

        public static string GDRelative(string mfgRelativePath)
        {
            return BuildPath(GameData, mfgRelativePath);
        }

        private string _mfg = DLTD_U_AM_Constant.mfg;
        public string MFGDir
        {
            get { return _mfg; }
            set { _mfg = value; }
        }
           
        private string _modsub;
        public string Mod
        {
            get {   return (_modsub != "") ? BuildPath( MFGDir, _modsub ) : MFGDir; }
            set { _modsub = value; }
        }

        public string Plugins
        {
            get { return BuildPath( Mod, "Plugins" ); }
        }

        private string _pdLivesIn = "";
        public string PluginDataIsBelow
        {
            set { _pdLivesIn = value; }
        }

        public string PluginData
        {
            get { return BuildPath( Plugins, _pdLivesIn, "PluginData"); }
        }

        public string Assets
        {
            get { return BuildPath( Mod, "Assets" ); }
        }

        public string Packages
        {
            get { return BuildPath(Assets, "Packages"); }
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
        private string _container;
        public string url
        {
            set { _container = value;  }
            get { return KSPPaths.BuildPath(_container, Asset.name); } // this isn't quite right because we want the asset filename, find out how to get that out of a bundle
        }


        public AssetRecord( UnityEngine.Object asset, string URL, string b_ID = "root" )
        {
            Asset = asset;
            BundleID = b_ID;
            url = URL;
        }
    }

    public enum BundleState {  Unloaded, BundleLoading, AssetLoading, AttributesLoading, BundleWaitingForUnload, BundleReadyForUnload, Final };
    public enum BundleType {  Bundle, Directory };
    public delegate void BundleRecordStateChange(BundleRecord b);

    // revert back to delegate? use delegate for OnStateChange and OnFinalize, don't have to subscribe to both
    public interface IAssetBundleClient
    {
        void OnBundleStateChange(BundleRecord b);
    }

    public class BundleRecord
    {
        private List<IAssetBundleClient> _clients;
        public List<IAssetBundleClient> Clients
        {
            get { return _clients; }
        }

        public void RegisterClient( IAssetBundleClient c )
        {
            Clients.Add(c);
        }

        private BundleState _state;
        public BundleState State
        {
            get { return _state; }
            set {
                _state = value;
                OnStateChange();
            }
        }

        public BundleType bundleType = BundleType.Bundle;

        public bool Finalized
        {
            get { return _state == BundleState.Final; }
        }

        public BundleRecordStateChange EveryStateChange;
        public BundleRecordStateChange OnFinalize;

        protected virtual void OnStateChange()
        {
            // Can't decide, have them both for now
             for (int i = 0; i < _clients.Count; i++)
                _clients[i].OnBundleStateChange(this);

            EveryStateChange?.Invoke(this);
            if (Finalized)
                OnFinalize?.Invoke(this);
        }

        public string BundleID;

        private KSPPaths bundlePath;
        public string BundleFN;
        public string BundleLoc
        {
            get { return KSPPaths.BuildPath( KSPPaths.GDRelative( bundlePath.Packages ), BundleFN ); }
        }
        public string BundleLocFull
        {
            get { return KSPPaths.FullPath(BundleLoc); }
        }

        public int TTL = DLTD_U_AM_Constant.bundleUnloadFramecount;

        public string BundleLocForWWW
        {
            get { return "file:///" + BundleLocFull; }
        }
        public UnityEngine.Object[] Assets;
        public ConfigNode Attributes;

        public BundleRecord( KSPPaths loc, string bundleFN, string b_ID = "root", BundleState initialState = BundleState.Unloaded )
        {
            BundleID = b_ID;
            BundleFN = bundleFN;
            bundlePath = loc;
            _clients = new List<IAssetBundleClient>();
            State = initialState;
            TTL = DLTD_U_AM_Constant.bundleUnloadFramecount;
        }
    }

    delegate IEnumerator AssetLoader(BundleRecord b);
    delegate UnityEngine.Object AssetCreator(byte[] raw);
    delegate void AssetPostLoad( UnityEngine.Object obj);

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class AssetManager : MonoBehaviour
    {

        TDebug dbg;
        // do some error checking please...

        private const string attributeTag = "ASSET_ATTRIBUTE";
        private const string attributeExt = ".atr";

        private static KSPPaths globalPaths;

        private Dictionary<BundleType, AssetLoader> Loaders;
        private Dictionary<Type, AssetPostLoad> AssetPostLoaders;

        public Dictionary<string, AssetRecord> Assets;
        public Dictionary<string, BundleRecord> AssetsByBundleID;
        public List<BundleRecord> UnloadQueue;

        public static AssetManager instance;

        protected Dictionary<string, AssetCreator> typeCreators; // extension -> type

        // turn this into attribute + small class pairs at some point
        private void InitialiseLoaders()
        {
            Loaders = new Dictionary<BundleType, AssetLoader>();
            Loaders.Add(BundleType.Bundle, LoadAssetBundleFromBundle);
            Loaders.Add(BundleType.Directory, LoadAssetBundleFromDirectory);

            typeCreators = new Dictionary<string, AssetCreator>();
            typeCreators.Add("shader", (assetData) =>
            {

               var shaderData = Convert.ToBase64String(assetData);

               // borrowed from EVE/rbray
               if (SystemInfo.graphicsDeviceVersion.Contains("OpenGL"))
               {
                   shaderData = shaderData.Replace("Offset 0, 0", "Offset -.25, -.25");
               }

               var tempMat = new Material(shaderData);
               return tempMat.shader;
            });

            AssetCreator imgLoader = (assetData) =>
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(assetData);
                return tex;
            };

            typeCreators.Add("tga", imgLoader);
            typeCreators.Add("png", imgLoader);
            typeCreators.Add("jpg", imgLoader);
            typeCreators.Add("jpeg", imgLoader);
            typeCreators.Add("dds", imgLoader); // not sure this works

            AssetPostLoaders = new Dictionary<Type, AssetPostLoad>();
            //AssetPostLoaders.Add(typeof(Shader), (s) =>
            //    {
            //        // sadly this does not force the shader into unity's internal list
            //        var m = new Material((s as Shader));
            //    }
            //);
        }

        private void BundledAssetsFilterAndAttribs(BundleRecord bundleRec )
        {
            AssetsByBundleID[bundleRec.BundleID] = bundleRec;

            for (int i = 0; i < bundleRec.Assets.Length; i++)
            {

 //               dbg.Print("Asset " + bundleRec.Assets[i].name + " is a " + bundleRec.Assets[i].GetType().ToString());

                var assetName = bundleRec.Assets[i].name;
                var assetRec = new AssetRecord(bundleRec.Assets[i], bundleRec.BundleLoc, bundleRec.BundleID);
                Assets[assetName] = assetRec;

                if( AssetPostLoaders.ContainsKey( bundleRec.Assets[i].GetType()))
                {
                    AssetPostLoaders[bundleRec.Assets[i].GetType()](bundleRec.Assets[i]);
                }
            }

            bundleRec.State = BundleState.AttributesLoading;
            LoadAssetAttributes(bundleRec);
        }

        private IEnumerator LoadAssetBundleFromBundle( BundleRecord bundleRec )
        {
            while (!Caching.ready)
                yield return null;

            //            var bundleLoadRequest = AssetBundle.LoadFromFileAsync(b.BundleLoc);
            //            var bundle = AssetBundle.CreateFromFile(b.BundleLoc);

            bundleRec.State = BundleState.BundleLoading;
            using (WWW www = new WWW(bundleRec.BundleLocForWWW))
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.Log(www.error);
                    yield break;
                }

                                         //dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " loaded from disk, preparing to load assets.");

                var bundle = www.assetBundle;
                bundleRec.State = BundleState.AssetLoading;
                var assetsLoadRequest = bundle.LoadAllAssetsAsync();

                yield return assetsLoadRequest;

                                         //dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " assets loaded.");

                bundleRec.Assets = assetsLoadRequest.allAssets;

                BundledAssetsFilterAndAttribs(bundleRec);

                bundleRec.State = BundleState.BundleWaitingForUnload;

                UnloadQueue.Add(bundleRec);

                while (bundleRec.State != BundleState.BundleReadyForUnload)
                    yield return bundleRec;

                dbg.Print("LoadAssetBundle: unloading bundle " + bundleRec.BundleID);
                bundle.Unload(false);
                bundleRec.State = BundleState.Final;
          }
        }

        private IEnumerator LoadAssetBundleFromDirectory(BundleRecord bundleRec)
        {
            while (!Caching.ready)
                yield return null;

            //            var bundleLoadRequest = AssetBundle.LoadFromFileAsync(b.BundleLoc);
            //            var bundle = AssetBundle.CreateFromFile(b.BundleLoc);

            bundleRec.State = BundleState.BundleLoading;
            bundleRec.State = BundleState.AssetLoading;
            if (bundleRec.Assets == null)
                bundleRec.Assets = new UnityEngine.Object[10];

            var _brAssetI = 0;

            foreach (string asset in Directory.GetFiles(bundleRec.BundleLoc))
            {
                AssetCreator assetCreator;
                if (typeCreators.TryGetValue(Path.GetExtension(asset).ToLower(), out assetCreator))
                {
                    using (FileStream assetStream = new FileStream(asset, FileMode.Open))
                    {
                        var assetData = new byte[assetStream.Length];
                        var _leftToRead = assetStream.Length;
                        var _readLength = (assetStream.Length > int.MaxValue) ? int.MaxValue : (int)assetStream.Length;

                        while (_leftToRead > 0)
                        {
                            assetStream.Read(assetData, 0, _readLength);
                            _leftToRead -= _readLength;
                            _readLength = (_leftToRead > int.MaxValue) ? int.MaxValue : (int)_leftToRead;
                        }
                        bundleRec.Assets[_brAssetI++] = assetCreator(assetData);
                    }
                }
                yield return asset;
            }

            //                         dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " loaded from disk, preparing to load assets.");

            //                         dbg.Print("LoadAssetBundle: bundle " + bundleRec.BundleID + " assets loaded.");

            BundledAssetsFilterAndAttribs(bundleRec);

            bundleRec.State = BundleState.BundleWaitingForUnload;
            bundleRec.State = BundleState.Final;

        }


        private void LoadAssetAttributes( BundleRecord bundleRec)
        {
            var cfgFile = bundleRec.BundleLocFull + attributeExt;

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

        public static BundleRecord CreateModBundle ( KSPPaths modPaths, string bundleFN, string bundleID )
        {
            return new BundleRecord(modPaths, bundleFN, bundleID);
        }

        public BundleRecord LoadModBundle( BundleRecord bundleRec )
        {
            StartCoroutine(Loaders[bundleRec.bundleType](bundleRec));
            //          StartCoroutine(LoadAssetAttributes(bundleRec));
            return bundleRec;
        }

        public BundleRecord LoadModBundle( KSPPaths modPaths, string bundleFN, string bundleID, IAssetBundleClient client = null )
        {
 //           dbg.Print("LoadModBundle: " + modPaths.Mod + " " + bundleFN + " " + bundleID);
            var bundleRec = new BundleRecord(modPaths, bundleFN, bundleID);
            bundleRec.Clients.Add(client);
            return LoadModBundle(bundleRec);
        }

        public BundleRecord LoadModBundle(KSPPaths modPaths, string bundleFN, string bundleID, BundleRecordStateChange handler = null )
        {
            //           dbg.Print("LoadModBundle: " + modPaths.Mod + " " + bundleFN + " " + bundleID);
            var bundleRec = new BundleRecord( modPaths, bundleFN, bundleID);
            if (handler != null )
                bundleRec.EveryStateChange += handler;
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

        private void LoadGlobalPreloadBundles()
        {
            foreach (string bundleFile in Directory.GetFiles(KSPPaths.FullPath(KSPPaths.GDRelative(globalPaths.Packages)))) // you know, I think this is a bit overcomplicated
            {
                if (Path.GetExtension( bundleFile ) == "")
                {
                    var fn = Path.GetFileName(bundleFile);
                    dbg.Print("Preloading " + fn);
                    LoadModBundle(CreateModBundle(globalPaths, fn, fn));
                }
            }
        }

        public void Awake()
        {
            Assets = new Dictionary<string, AssetRecord>();
            AssetsByBundleID = new Dictionary<string, BundleRecord>();
            UnloadQueue = new List<BundleRecord>();
            globalPaths = new KSPPaths();

            dbg = new TDebug("[DLTD AssetManager] ");

            InitialiseLoaders();
            LoadGlobalPreloadBundles();

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
                        UnloadQueue[i].State = BundleState.BundleReadyForUnload;
                        UnloadQueue[i].TTL = DLTD_U_AM_Constant.bundleUnloadFramecount;
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
