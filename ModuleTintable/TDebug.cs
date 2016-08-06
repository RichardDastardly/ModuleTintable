using System;
using System.Text;
using UnityEngine;

namespace DLTD.Utility
{
    #region TDebug
    public static class TDebug
    {
        // debugging stuff from the start! how novel
        // dump this when we're done
        private static string dbgTag = "[DLTD Debug] ";

        public static string DbgTag
        {
            get { return dbgTag; }
            set { dbgTag = value; }
        }

        public static void Print(string dbgString)
        {
            Debug.Log(dbgTag + dbgString);
        }

        public static void Warn(string dbgString)
        {
            Debug.LogWarning(dbgTag + dbgString);
        }

        public static void Err(string dbgString)
        {
            Debug.LogError(dbgTag + dbgString);
        }
    }
    #endregion
}
