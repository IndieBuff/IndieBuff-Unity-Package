using System;

[Serializable]
public class AssetNode
{
    public string Name;
    public string Path;
    public string Type;
    public DateTime LastModified;
    public DateTime Added;
    public float RelevancyScore = 1.0f;
}
