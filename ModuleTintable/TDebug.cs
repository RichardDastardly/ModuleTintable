using System;
using System.Text;
using UnityEngine;

namespace DLTD.Utility
{
    #region TDebug
    public class TDebug
    {
        // debugging stuff from the start! how novel
        // dump this when we're done
        private string dbgTag = "[DLTD Debug] ";

        public TDebug() { }
        public TDebug( string tag )
        {
            dbgTag = tag;
        }

        public string DbgTag
        {
            get { return dbgTag; }
            set { dbgTag = value; }
        }

        public void Print(string dbgString)
        {
            Debug.Log(dbgTag + dbgString);
        }

        public static void Print(string tag, string dbgString )
        {
            Debug.Log(tag + dbgString);
        }

        public void Warn(string dbgString)
        {
            Debug.LogWarning(dbgTag + dbgString);
        }

        public void Err(string dbgString)
        {
            Debug.LogError(dbgTag + dbgString);
        }
    }
    #endregion

    public class ModuleTrace : PartModule
    {
        public string mdbtag
        { 
            get { return "[ModuleTrace] " + part.name + "_" + instanceID + " "; }
        }
        public uint instanceID
        {
            get { return (part.flightID == 0) ? part.craftID : part.flightID; }
        }

        public bool updateRun = false;
        public bool fixedUpdateRun = false;
        public bool lateUpdateRun = false;

        public override void OnAwake()
        {
            updateRun = false;
            lateUpdateRun = false;
            fixedUpdateRun = false;

            Debug.Log(mdbtag + "OnAwake()");
        }

        private void Log( string logString )
        {
            Debug.Log(mdbtag + " " + HighLogic.LoadedScene.ToString() + " " + logString);
        }

        public override void OnLoad(ConfigNode node)
        {
            Log(mdbtag + "OnLoad()");
        }

        public override void OnSave(ConfigNode node)
        {
            Log(mdbtag + "OnSave()");
        }

        public void OnEditorAttach()
        {
            Log(mdbtag + "OnEditorAttach()");
        }

        public override void OnInitialize()
        {
            Log(mdbtag + "OnInitialize()");
        }

        public override void OnActive()
        {
            Log(mdbtag + "OnActive()");
        }

        public void Start()
        {
            Log(mdbtag+"Start()");
        }

        public void Update()
        {
            if( !updateRun )
            {
                updateRun = true;
                Log(mdbtag + "Update()");
            }
        }

        public void FixedUpdate()
        {
            if (!fixedUpdateRun)
            {
                fixedUpdateRun = true;
                Log(mdbtag + "FixedUpdate()");
            }
        }

        public void LateUpdate()
        {
            if (!lateUpdateRun)
            {
                lateUpdateRun = true;
                Log(mdbtag + "LateUpdate()");
            }
        }

        public void OnDestroy()
        {
            Log("[ModuleTrace] OnDestroy()");
        }
    }
}
