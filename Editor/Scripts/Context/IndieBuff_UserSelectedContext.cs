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
    internal class IndieBuff_UserSelectedContext
    {
        internal Action onUserSelectedContextUpdated;
        private List<UnityEngine.Object> _contextObjects;
        internal List<UnityEngine.Object> UserContextObjects
        {
            get { return _contextObjects; }
        }

        private List<IndieBuff_LogEntry> _consoleLogs;
        internal List<IndieBuff_LogEntry> ConsoleLogs
        {
            get { return _consoleLogs; }
        }
        private IndieBuff_UserSelectedContext()
        {
            _contextObjects = new List<UnityEngine.Object>();
            _consoleLogs = new List<IndieBuff_LogEntry>();
        }

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

        internal bool AddConsoleLog(IndieBuff_LogEntry logEntry)
        {
            if (logEntry != null && !_consoleLogs.Contains(logEntry))
            {
                _consoleLogs.Add(logEntry);
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

        internal bool RemoveConsoleLog(int index)
        {
            if (index >= 0 && index < _consoleLogs.Count)
            {
                _consoleLogs.RemoveAt(index);
                onUserSelectedContextUpdated?.Invoke();
                return true;
            }
            return false;
        }
        internal bool ClearContextObjects()
        {
            _contextObjects.Clear();
            _consoleLogs.Clear();
            onUserSelectedContextUpdated?.Invoke();
            return true;
        }

        internal Task<Dictionary<string, object>> BuildUserContext()
        {
            IndieBuff_ContextGraphBuilder builder = new IndieBuff_ContextGraphBuilder(_contextObjects, includeConsoleLogs: true);
            return builder.StartContextBuild();
        }
    }
}