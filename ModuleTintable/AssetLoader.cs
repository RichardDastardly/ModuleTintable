using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tintable
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class AssetLoader : MonoBehaviour
    {
        // Shaders
        private static Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();
        private static List<string> ReplacementShaderNames = new List<string>();

        public static bool shadersLoaded { get; private set; } = false;


        // Keep the tinted replacement shaders named KSP/Tinted <oldshader> & we don't need a translator dictionary
        public static Shader FetchRepacementShader( string KSPShader )
        {
            if (!shadersLoaded)
                return null;

            Shader rval;
 //           TDebug.Print("Attempting to fetch replacement shader for " + KSPShader.Substring(4));
            // assume shader string passed in is "KSP/<shader>" for now
            if( Shaders.TryGetValue(KSPShader.Substring(4), out rval))
            {
                return rval;
            }
            return null;
        }

        public static bool IsReplacementShader( string KSPShader )
        {
            if (!shadersLoaded)
                return false;
            return ReplacementShaderNames.Contains(KSPShader);
        }

        // the proper loader is bugged! so surprised. Time to cobble together something, mostly cribbed
        // from InfernalRobotics

        private AssetBundle TABundle;

        public IEnumerator LoadBundle(string FileLoc)
        {
            while (!Caching.ready)
                yield return null;
            using (WWW www = WWW.LoadFromCacheOrDownload(FileLoc, 1))
            {
                yield return www;
                TABundle = www.assetBundle;

                LoadBundledAssets();
            }
        }

        private void LoadBundledAssets()
        {
            var BundleShaders = TABundle.LoadAllAssets<Shader>();

            for( int i = 0; i < BundleShaders.Length; i++ )
            {
                if( BundleShaders[i] != null )
                {
                    string ShaderShortName = BundleShaders[i].name.Substring(11);
                    // this is horribly inflexible, improve it later

//                    TDebug.Print("Loading shader " + BundleShaders[i].name + " as "+ShaderShortName);
                    Shaders.Add(ShaderShortName, BundleShaders[i] );
                    ReplacementShaderNames.Add(BundleShaders[i].name);
                }
            }
            shadersLoaded = true;
        }

        public void LoadBundleFromDisk( string FileLoc )
        {
            TABundle = AssetBundle.CreateFromFile(FileLoc);
            LoadBundledAssets();
        }

        public void Start()
        {
//            TDebug.Print("AssetLoader.Start() - grepping and loading shaders");

            var assemblyFile = Assembly.GetExecutingAssembly().Location;
            var BundlePath = "file://" + assemblyFile.Replace(new FileInfo(assemblyFile).Name, "").Replace("\\", "/");

//            TDebug.Print("Loading bundles from BundlePath: " + BundlePath);

            //need to clean cache
            Caching.CleanCache();

            StartCoroutine(LoadBundle(BundlePath + "shaders.ksp"));

            //     GetAssetDefinitionsWithType currently only returns one definition, and then bugs out and attempts to load the bundle again.
            //           KSPAssets.Loaders.AssetLoader.LoadAssets(cbLoadShadersFromBundle,
            //                   KSPAssets.Loaders.AssetLoader.GetAssetDefinitionsWithType("DLTD/Tinter/shaders", "Shader" ));
        }

        public void OnDestroy()
        {
            if( TABundle )
            {
                TDebug.Print("Unloading assets.");
                TABundle.Unload(false);
            }
        }

        // Currently redundant.
        //void cbLoadShadersFromBundle(KSPAssets.Loaders.AssetLoader.Loader Loader )
        //{
        //    TDebug.Print("Attempting to load shaders...");
        //    for (int i = 0; i < Loader.definitions.Length; ++i)
        //    {
        //        UnityEngine.Object obj = Loader.objects[i];
        //        if (obj == null)
        //            continue;
        //        Shader bundledShader = obj as Shader;
        //        string ShaderName = Loader.definitions[i].name;
        //        if (bundledShader != null && ShaderName != null) // is an empty string null?
        //        {
        //            TDebug.Print("Loading shader " + ShaderName);
        //            Shaders.Add(ShaderName, bundledShader);
        //        }
        //    }
        //}
    }
}
