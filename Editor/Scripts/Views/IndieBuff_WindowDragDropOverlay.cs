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
        private readonly VisualElement dropRoot;
        private readonly VisualElement overlayContainer;
        private readonly VisualElement overlayBackground;
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

            dropRoot = visualTree.Instantiate();

            string dropStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_WindowDropComponent.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(dropStylePath);

            dropRoot.styleSheets.Add(styleSheet);

            dropRoot.style.width = new StyleLength(Length.Percent(100));
            dropRoot.style.height = new StyleLength(Length.Percent(100));
            dropRoot.style.position = Position.Absolute;
            dropRoot.style.display = DisplayStyle.None;

            overlayContainer = dropRoot.Q("DNDContainer");
            dropLabel = dropRoot.Q<Label>("DNDLabel");
            overlayBackground = dropRoot.Q("DNDBackground");

            // Remove any existing overlay first
            var existingOverlay = root.Q("DNDContainer");
            if (existingOverlay != null)
            {
                root.Remove(existingOverlay);
            }

            root.Add(dropRoot);
            overlayContainer.BringToFront();
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
                dropRoot.style.display = DisplayStyle.Flex;

            }
            evt.StopPropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            dropRoot.style.display = DisplayStyle.None;
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

            dropRoot.style.display = DisplayStyle.None;
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