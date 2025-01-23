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
        private readonly Label dropLabel;

        public IndieBuff_WindowDragDropOverlay(VisualElement root)
        {
            this.root = root;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_WindowDropComponent.uxml");
            if (visualTree == null)
            {
                Debug.LogError("Failed to load window drop component UXML");
                return;
            }

            var element = visualTree.Instantiate();
            overlayContainer = element.Q("WindowDropZone");
            dropLabel = element.Q<Label>("WindowDropLabel");

            overlayContainer.pickingMode = PickingMode.Ignore;
            overlayContainer.style.position = Position.Absolute;
            overlayContainer.style.left = 0;
            overlayContainer.style.right = 0;
            overlayContainer.style.top = 0;
            overlayContainer.style.bottom = 0;
            overlayContainer.style.backgroundColor = new Color(0, 0, 0, 0);

            root.Add(element);
            SetupDragAndDrop();
        }

        private void SetupDragAndDrop()
        {
            root.RegisterCallback<DragEnterEvent>(OnDragEnter);
            root.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerformed);
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            if (IsDraggedObjectValid())
            {
                overlayContainer.AddToClassList("active");
                dropLabel.AddToClassList("active");
            }
            evt.StopPropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            overlayContainer.RemoveFromClassList("active");
            dropLabel.RemoveFromClassList("active");
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
            
            overlayContainer.RemoveFromClassList("active");
            dropLabel.RemoveFromClassList("active");
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