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
            get { return "[ModuleTrace] " + part.name + " Instance: " + instanceID + " "; }
        }
        public uint instanceID
        {
            get { return (part.craftID == 0) ? part.flightID : part.craftID; }
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

        public override void OnLoad(ConfigNode node)
        {
            Debug.Log(mdbtag + "OnLoad()");
        }

        public override void OnSave(ConfigNode node)
        {
            Debug.Log(mdbtag + "OnSave()");
        }

        public void OnEditorAttach()
        {
            Debug.Log(mdbtag + "OnEditorAttach()");
        }

        public override void OnInitialize()
        {
            Debug.Log(mdbtag + "OnInitialize()");
        }

        public override void OnActive()
        {
            Debug.Log(mdbtag + "OnActive()");
        }

        public void Start()
        {
            Debug.Log(mdbtag+"Start()");
        }

        public void Update()
        {
            if( !updateRun )
            {
                updateRun = true;
                Debug.Log(mdbtag + "Update()");
            }
        }

        public void FixedUpdate()
        {
            if (!fixedUpdateRun)
            {
                fixedUpdateRun = true;
                Debug.Log(mdbtag + "FixedUpdate()");
            }
        }

        public void LateUpdate()
        {
            if (!lateUpdateRun)
            {
                lateUpdateRun = true;
                Debug.Log(mdbtag + "LateUpdate()");
            }
        }

        public void OnDestroy()
        {
            Debug.Log(mdbtag + "OnDestroy()");
        }
    }
}
