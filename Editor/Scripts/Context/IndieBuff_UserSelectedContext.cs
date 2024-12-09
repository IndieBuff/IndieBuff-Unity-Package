using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_UserSelectedContext
    {
        internal Action onUserSelectedContextUpdated;
        private List<UnityEngine.Object> _contextObjects;
        private static IndieBuff_UserSelectedContext _instance;

        internal static IndieBuff_UserSelectedContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_UserSelectedContext();
                }
                return _instance;
            }
        }

        internal bool AddContextObject(UnityEngine.Object contextObject)
        {
            if (contextObject is not DefaultAsset && !_contextObjects.Contains(contextObject))
            {
                _contextObjects.Add(contextObject);
                onUserSelectedContextUpdated?.Invoke();
                return true;
            }

            return false;
        }

        internal bool RemoveContextObject(int index)
        {
            if (index >= 0 && index < _contextObjects.Count)
            {
                _contextObjects.RemoveAt(index);
                onUserSelectedContextUpdated?.Invoke();
                return true;
            }
            return false;
        }

        internal bool ClearContextObjects()
        {
            _contextObjects.Clear();
            onUserSelectedContextUpdated?.Invoke();
            return true;
        }

        internal Dictionary<string, object> BuildUserContext()
        {
            IndieBuff_ContextGraphBuilder builder = new IndieBuff_ContextGraphBuilder(_contextObjects, 1000);
            builder.StartContextBuild();

            return builder.GetContextData();
        }

    }
}