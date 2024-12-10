using System.Collections.Generic;
using System.IO;

namespace IndieBuff.Editor
{

    internal class IndieBuff_SceneMetadata
    {
        public string Guid { get; set; }
        public List<IndieBuff_SerializedObjectIdentifier> Objects { get; set; } = new List<IndieBuff_SerializedObjectIdentifier>();
        public HashSet<string> ComponentTypes { get; set; } = new HashSet<string>();
        public Dictionary<string, HashSet<string>> TaggedObjects { get; set; } = new Dictionary<string, HashSet<string>>();
        public HashSet<string> GameObjectNames { get; set; } = new HashSet<string>();

        public long LastModified { get; set; }

        public void SerializeTo(BinaryWriter writer)
        {
            writer.Write(Guid);
            writer.Write(LastModified);
            writer.Write(Objects.Count);
            foreach (var obj in Objects)
            {
                writer.Write(obj.assetGuid);
                writer.Write(obj.localIdentifier);
                writer.Write(obj.fileId);
                writer.Write(obj.componentName ?? "");
            }

            writer.Write(ComponentTypes.Count);
            foreach (var type in ComponentTypes)
                writer.Write(type);

            writer.Write(TaggedObjects.Count);
            foreach (var kvp in TaggedObjects)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Count);
                foreach (var obj in kvp.Value)
                    writer.Write(obj);
            }

            writer.Write(GameObjectNames.Count);
            foreach (var name in GameObjectNames)
                writer.Write(name);
        }

        public static IndieBuff_SceneMetadata DeserializeFrom(BinaryReader reader)
        {
            var metadata = new IndieBuff_SceneMetadata
            {
                Guid = reader.ReadString(),
                LastModified = reader.ReadInt64()
            };

            int objCount = reader.ReadInt32();
            for (int i = 0; i < objCount; i++)
            {
                metadata.Objects.Add(new IndieBuff_SerializedObjectIdentifier
                {
                    assetGuid = reader.ReadString(),
                    localIdentifier = reader.ReadInt64(),
                    fileId = reader.ReadInt32(),
                    componentName = reader.ReadString()
                });
            }

            int typeCount = reader.ReadInt32();
            for (int i = 0; i < typeCount; i++)
                metadata.ComponentTypes.Add(reader.ReadString());

            int tagCount = reader.ReadInt32();
            for (int i = 0; i < tagCount; i++)
            {
                var tag = reader.ReadString();
                var count = reader.ReadInt32();
                var objects = new HashSet<string>();
                for (int j = 0; j < count; j++)
                    objects.Add(reader.ReadString());
                metadata.TaggedObjects[tag] = objects;
            }

            int nameCount = reader.ReadInt32();
            for (int i = 0; i < nameCount; i++)
                metadata.GameObjectNames.Add(reader.ReadString());

            return metadata;
        }
    }
}