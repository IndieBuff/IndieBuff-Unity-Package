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
        private IndieBuff_ContextStatePersistence _statePersistence;
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
        private IndieBuff_UserSelectedContext()
        {
            _contextObjects = new List<UnityEngine.Object>();
            _consoleLogs = new List<IndieBuff_LogEntry>();
            _statePersistence = new IndieBuff_ContextStatePersistence(this);
        }

        // restore state of context objects and console logs
        internal void RestoreStateIfNeeded()
        {
            _statePersistence.RestoreStateIfNeeded();
        }

        internal bool AddContextObject(UnityEngine.Object contextObject)
        {
            if (contextObject is not DefaultAsset && !_contextObjects.Contains(contextObject))
            {
                _contextObjects.Add(contextObject);
                onUserSelectedContextUpdated?.Invoke();
                _statePersistence.SaveState();
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
                _statePersistence.SaveState();
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
                _statePersistence.SaveState();
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
                _statePersistence.SaveState();
                return true;
            }
            return false;
        }
        internal bool ClearContextObjects()
        {
            _contextObjects.Clear();
            _consoleLogs.Clear();
            onUserSelectedContextUpdated?.Invoke();
            _statePersistence.SaveState();
            return true;
        }

        internal void OnDestroy()
        {
            _statePersistence.Cleanup();
        }

        internal Task<Dictionary<string, object>> BuildUserContext()
        {
            IndieBuff_ContextGraphBuilder builder = new IndieBuff_ContextGraphBuilder(_contextObjects, includeConsoleLogs: true);
            return builder.StartContextBuild();
        }
    }
}