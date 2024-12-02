using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_AddContextComponent
    {

        private VisualElement root;
        private VisualTreeAsset addContextAsset;
        private VisualElement dropArea;
        private Button getSelectedItemsButton;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();

        public IndieBuff_AddContextComponent()
        {
        }


        public void Initialize()
        {
            if (root != null) return;

            addContextAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IndieBuff/Editor/UXML/IndieBuff_AddContextComponent.uxml");
            if (addContextAsset == null)
            {
                Debug.LogError("Failed to load profile settings asset");
                return;
            }

            root = addContextAsset.Instantiate();
            root.pickingMode = PickingMode.Position;
            root.style.position = Position.Absolute;
            root.style.marginLeft = 10;
            root.style.marginRight = 10;
            root.style.maxWidth = 450;
            root.style.width = new StyleLength(Length.Percent(100));

            SetupAddContextUI();
        }

        public VisualElement GetRoot()
        {
            if (root == null) Initialize();
            return root;
        }

        private void SetupAddContextUI()
        {
            dropArea = root.Q<VisualElement>("DropZone");
            getSelectedItemsButton = root.Q<Button>("SelectItemsButton");

            getSelectedItemsButton.clicked += OnGetSelectedItemsClicked;
            dropArea.RegisterCallback<DragEnterEvent>(OnDragEnter);
            dropArea.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            dropArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropArea.RegisterCallback<DragPerformEvent>(OnDragPerformed);

            dropArea.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

        }


        private void OnGetSelectedItemsClicked()
        {
            UnityEngine.Object[] selectedObjects = Selection.objects;

            foreach (UnityEngine.Object obj in selectedObjects)
            {
                if (obj is not DefaultAsset && !processedObjects.Contains(obj))
                {
                    IndieBuff_ContextBuilder.Instance.AddContextObject(obj);
                    processedObjects.Add(obj);
                }
            }
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            if (IsDraggedObjectValid())
            {
                dropArea.style.backgroundColor = new Color(0.3f, 0.4f, 0.3f);
            }
            else
            {
                dropArea.style.backgroundColor = new Color(0.4f, 0.3f, 0.3f);
            }

        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            dropArea.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = IsDraggedObjectValid() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        }

        private void OnDragPerformed(DragPerformEvent evt)
        {
            dropArea.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            if (!IsDraggedObjectValid()) return;

            processedObjects.Clear();

            foreach (var objectReference in DragAndDrop.objectReferences)
            {
                if (!processedObjects.Contains(objectReference))
                {
                    IndieBuff_ContextBuilder.Instance.AddContextObject(objectReference);
                    processedObjects.Add(objectReference);
                }

            }
        }

        private bool IsDraggedObjectValid()
        {
            return DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences.Any(obj => obj is not DefaultAsset);
        }

        public void ClearContextItems()
        {
            IndieBuff_ContextBuilder.Instance.ClearContextObjects();
            processedObjects.Clear();
        }



    }
}