using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_SceneContext
    {

        private static IndieBuff_SceneContext _instance;
        internal static IndieBuff_SceneContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_SceneContext();
                }
                return _instance;
            }
        }
    }
}