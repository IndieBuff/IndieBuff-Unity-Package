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

            overlayContainer.pickingMode = PickingMode.Position;
            overlayContainer.style.position = Position.Absolute;
            overlayContainer.style.left = 0;
            overlayContainer.style.right = 0;
            overlayContainer.style.top = 0;
            overlayContainer.style.bottom = 0;
            overlayContainer.style.flexGrow = 1;
            

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
                Debug.Log("Adding 'active' class to overlay container");
                overlayContainer.AddToClassList("active");
                dropLabel.AddToClassList("active");
                overlayContainer.style.display = DisplayStyle.Flex;
                overlayContainer.BringToFront();
                overlayContainer.MarkDirtyRepaint();
            }
            evt.StopPropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            Debug.Log("Removing 'active' class to overlay container");
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

            Debug.Log("Removing 'active' class to overlay container");
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