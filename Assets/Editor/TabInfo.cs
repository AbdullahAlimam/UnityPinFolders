using System.IO;
using UnityEngine;

public class TabInfo
{
    public string Title;
    public string FolderPath;
    public int TargetWindowId;
    public Color Color;
    public string IconName = "folder"; // default icon

    public TabInfo(string folderPath, string title, int windowId, string iconName, Color color)
    {
        FolderPath = folderPath;

        if (string.IsNullOrEmpty(title))
            Title = Path.GetFileName(folderPath);
        else
            Title = title;

        IconName = iconName;
        TargetWindowId = windowId;
        Color = color;
    }
}