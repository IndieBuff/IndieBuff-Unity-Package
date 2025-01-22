using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;

namespace IndieBuff.Editor
{
    public class IndieBuff_WindowDragDropOverlay : IDisposable
    {
        private readonly VisualElement root;
        private readonly VisualElement overlayContainer;

        public IndieBuff_WindowDragDropOverlay(VisualElement root)
        {
            this.root = root;
            
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_WindowDropComponent.uxml");
            if (visualTree == null)
            {
                Debug.LogError("Failed to load window drop component UXML");
                return;
            }

            overlayContainer = visualTree.Instantiate().Q("WindowDropZone");
            
            // Set to None so it doesn't interfere with drag events
            overlayContainer.pickingMode = PickingMode.Ignore;
            overlayContainer.style.position = Position.Absolute;
            
            // Use unitySliceIndex instead of zIndex
            overlayContainer.style.unitySliceTop = 99999;
            
            // Initialize with invisible background
            overlayContainer.style.backgroundColor = new Color(0, 0, 0, 0);
            
            root.Add(overlayContainer);
            
            // Register drag and drop callbacks on root instead of overlay
            SetupDragAndDrop();
        }

        private void SetupDragAndDrop()
        {
            // Register events on root to catch all drag operations
            root.RegisterCallback<DragEnterEvent>(OnDragEnter);
            root.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerformed);
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            Debug.Log("Drag Enter");
            overlayContainer.AddToClassList("active");
            
            if (IsDraggedObjectValid())
            {
                // Darken background on valid drag
                overlayContainer.style.backgroundColor = new Color(0, 0, 0, 0.75f);
            }
           
            evt.StopPropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            Debug.Log("Drag Leave");
            // Make background invisible when leaving window
            overlayContainer.RemoveFromClassList("active");
            overlayContainer.style.backgroundColor = new Color(0, 0, 0, 0);
            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = IsDraggedObjectValid() ? 
                DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnDragPerformed(DragPerformEvent evt)
        {
            if (!IsDraggedObjectValid()) return;

            foreach (var objectReference in DragAndDrop.objectReferences)
            {
                if (objectReference is not DefaultAsset)
                {
                    IndieBuff_UserSelectedContext.Instance.AddContextObject(objectReference);
                }
            }
            
            // Make background invisible after successful drop
            overlayContainer.RemoveFromClassList("active");
            overlayContainer.style.backgroundColor = new Color(0, 0, 0, 0);
            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
        }

        private bool IsDraggedObjectValid()
        {
            return DragAndDrop.objectReferences.Length > 0 && 
                   DragAndDrop.objectReferences.Any(obj => obj is not DefaultAsset);
        }

        public void Dispose()
        {
            if (root != null)
            {
                root.UnregisterCallback<DragEnterEvent>(OnDragEnter);
                root.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
                root.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
                root.UnregisterCallback<DragPerformEvent>(OnDragPerformed);
            }
        }
    }
}