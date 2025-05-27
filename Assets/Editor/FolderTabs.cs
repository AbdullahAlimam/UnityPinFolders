using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

public class FolderTabs : EditorWindow
{
    private const string PinnedFoldersKey = "PinnedFolders";
    private static List<TabInfo> pinnedFolders = new List<TabInfo>();
    private static List<Color> RandomColors = new List<Color>() { Color.blue, Color.green, Color.white, Color.red, Color.cyan, Color.magenta,Color.yellow };
    
    private static Dictionary<string, Texture2D> availableIcons = new();
    private static string[] iconNames = new[] { "folder", "scripts", "prefabs", "scenes", "textures", "effects", "audio", "animations", "ui", "star" };


    private static Vector2 scrollPos;
    private Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();
    private int dragIndex = -1;
    private Vector2 dragStartPos;
    private bool isDragging = false;
    public static float itemHeight = 120f; // estimated height of a tab block
    
    
    [InitializeOnLoadMethod]
    private static void OnProjectLoaded()
    {
        foreach (string icon in iconNames)
        {
            availableIcons[icon] = Resources.Load<Texture2D>("FolderIcons/" + icon); // from Resources folder
        }

        LoadPinnedFolders();

        // Delay UpdateProjectTabs until Unity is ready
        EditorApplication.delayCall += () => UpdateProjectTabs();
    }



    [MenuItem("Window/Folder Tabs")]
    public static void ShowWindow()
    {
        FolderTabs window = GetWindow<FolderTabs>("Folder Tabs");
    }

    /// <summary>
    /// Adds a "Pin" option to the context menu in the Project window.
    /// </summary>
    [MenuItem("Assets/Pin Folder", priority = 1000)]
    private static void PinFolder()
    {
        string path = GetSelectedFolderPath();
        TabInfo tab = PinFolderFromPath(path);

        // ✅ Ensure the FolderTabs window is visible
        FolderTabs window = EditorWindow.GetWindow<FolderTabs>();
        window.Show();
        window.Focus();
        scrollPos.y = float.MaxValue;

        OpenNewProjectTab(tab);
    }

    [MenuItem("Assets/Pin Folder", true)]
    private static bool ValidatePinFolder()
    {
        string path = GetSelectedFolderPath();
        return !string.IsNullOrEmpty(path);
    }

    private void OnGUI()
    {
        #region UI Drawing
        List<int> tabsToRemove = new List<int>();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.BeginVertical();

        GUILayout.Label("Pinned Folders", EditorStyles.boldLabel);

        if (pinnedFolders.Count == 0)
        {
            GUILayout.Label("No folders pinned.");
        }
        else
        {
            Event e = Event.current;

            if (e.type == EventType.ContextClick)
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Expand All"), false, () =>
                {
                    foreach (int key in foldoutStates.Keys.ToList())
                        foldoutStates[key] = true;
                });

                menu.AddItem(new GUIContent("Collapse All"), false, () =>
                {
                    foreach (int key in foldoutStates.Keys.ToList())
                        foldoutStates[key] = false;
                });

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Remove All Tabs"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Confirm", "Are you sure you want to remove all pinned folders?", "Yes", "Cancel"))
                    {
                        pinnedFolders.Clear();
                        SavePinnedFolders();
                    }
                });

                menu.ShowAsContext();
                e.Use();
            }

            for (int i = 0; i < pinnedFolders.Count; i++)
            {
                TabInfo ti = pinnedFolders[i];
                if (!foldoutStates.ContainsKey(i)) foldoutStates[i] = true;

                EditorGUILayout.BeginVertical("box");
                GUILayout.Space(1); // Force layout element
                Rect boxRect = GUILayoutUtility.GetLastRect();


                // Drag handle zone (left of the box)
                Rect dragRect = new Rect(boxRect.x + 200, boxRect.y + 1, 100, 20);
                EditorGUI.LabelField(dragRect, "≡≡≡≡≡≡≡≡≡≡≡≡", EditorStyles.boldLabel); // visual handle
                EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.MoveArrow);

                // Start drag
                if (e.type == EventType.MouseDown && dragRect.Contains(e.mousePosition))
                {
                    dragIndex = i;
                    dragStartPos = e.mousePosition;
                    isDragging = true;
                    e.Use();
                }

                // During drag
                if (isDragging && e.type == EventType.MouseDrag)
                {
                    float dragDelta = e.mousePosition.y - dragStartPos.y;
                    int targetIndex = Mathf.Clamp(dragIndex + (int)(dragDelta / itemHeight), 0, pinnedFolders.Count - 1);

                    if (targetIndex != dragIndex)
                    {
                        var draggedItem = pinnedFolders[dragIndex];
                        pinnedFolders.RemoveAt(dragIndex);
                        pinnedFolders.Insert(targetIndex, draggedItem);

                        dragIndex = targetIndex;
                        dragStartPos.y = e.mousePosition.y;
                        GUI.FocusControl(null);
                        Repaint();
                    }
                    e.Use();
                }

                // End drag
                if (e.type == EventType.MouseUp && isDragging)
                {
                    isDragging = false;
                    dragIndex = -1;
                    SavePinnedFolders();
                    e.Use();
                }

                GUILayout.BeginHorizontal();

                // Foldout with delete button at top-right
                Rect foldoutRect = GUILayoutUtility.GetRect(new GUIContent(ti.Title), EditorStyles.foldout);
                foldoutStates[i] = EditorGUI.Foldout(foldoutRect, foldoutStates[i], ti.Title, true);

                // Remove button (right)
                GUIStyle removeStyle = new GUIStyle(GUI.skin.button);
                removeStyle.fixedWidth = 22;

                if (GUILayout.Button("✕", removeStyle))
                {
                    tabsToRemove.Add(i);
                }

                GUILayout.Space(3);
                GUILayout.EndHorizontal();
                GUILayout.Space(5);

                if (foldoutStates[i])
                {
                    ti.FolderPath = EditorGUILayout.TextField("Path", ti.FolderPath);
                    ti.Color = EditorGUILayout.ColorField("Color", ti.Color);
                    ti.Title = EditorGUILayout.TextField("Title", ti.Title);

                    GUILayout.Label("Choose Icon:");

                    EditorGUILayout.BeginHorizontal();
                    foreach (var kvp in availableIcons)
                    {
                        string iconName = kvp.Key;
                        Texture2D iconTex = kvp.Value;

                        GUIStyle style = (ti.IconName == iconName) ? EditorStyles.helpBox : GUI.skin.button;

                        if (GUILayout.Button(iconTex, style, GUILayout.Width(24), GUILayout.Height(24)))
                        {
                            ti.IconName = iconName;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Save Changes & Apply"))
            {
                SavePinnedFolders();
                UpdateProjectTabs();
                TriggerScriptRecompile();
            }


        }

        if (tabsToRemove.Count > 0)
        {
            foreach (int removeIndex in tabsToRemove.OrderByDescending(i => i))
            {
                pinnedFolders.RemoveAt(removeIndex);
            }
            SavePinnedFolders();
        }
        #endregion

        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag folders here to pin", EditorStyles.helpBox);

        if (evt.type ==  EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        string path = AssetDatabase.GetAssetPath(draggedObject);

                        if (AssetDatabase.IsValidFolder(path))
                        {
                            PinFolderFromPath(path);
                        }
                    }

                    evt.Use();
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private static TabInfo PinFolderFromPath(string path)
    {
        if (!pinnedFolders.Exists(p => p.FolderPath == path))
        {
            Color defaultColor = RandomColors[Random.Range(0, RandomColors.Count)];
            TabInfo tab = new TabInfo(path, "", -1, "folder", defaultColor);
            pinnedFolders.Add(tab);
            SavePinnedFolders();
            return tab;
        }
        return null;
    }


    /// <summary>
    /// Gets the selected folder path.
    /// </summary>
    private static string GetSelectedFolderPath()
    {
        if (Selection.activeObject != null)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }
        }
        return string.Empty;
    }

    #region Load / Save Data
    /// <summary>
    /// Saves the pinned folders to EditorPrefs.
    /// </summary>
    private static void SavePinnedFolders()
    {
        List<string> serializedData = new List<string>();
        foreach (TabInfo tab in pinnedFolders)
        {
            string colorString = $"{tab.Color.r},{tab.Color.g},{tab.Color.b},{tab.Color.a}";
            serializedData.Add($"{tab.FolderPath}|{tab.TargetWindowId}|{colorString}|{tab.Title}|{tab.IconName}");
        }

        string data = string.Join(";", serializedData);
        EditorPrefs.SetString(PinnedFoldersKey, data);
    }

    /// <summary>
    /// Loads the pinned folders from EditorPrefs.
    /// </summary>
    private static void LoadPinnedFolders()
    {
        pinnedFolders.Clear();

        if (EditorPrefs.HasKey(PinnedFoldersKey))
        {
            string data = EditorPrefs.GetString(PinnedFoldersKey);
            string[] serializedData = data.Split(';');

            foreach (string entry in serializedData)
            {
                string[] parts = entry.Split('|');
                if (parts.Length == 5 &&
                int.TryParse(parts[1], out int windowId))
                {
                    string[] colorParts = parts[2].Split(',');
                    if (colorParts.Length == 4 &&
                        float.TryParse(colorParts[0], out float r) &&
                        float.TryParse(colorParts[1], out float g) &&
                        float.TryParse(colorParts[2], out float b) &&
                        float.TryParse(colorParts[3], out float a))
                    {
                        Color color = new Color(r, g, b, a);
                        pinnedFolders.Add(new TabInfo(parts[0], parts[3], windowId, parts[4], color));
                    }
                }
            }
        }
    }
    #endregion


    private static void UpdateProjectTabs(EditorWindow windowToUpdate = null, TabInfo tabForUpdate = null)
    {
        System.Type projectBrowserType = System.Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
        if (projectBrowserType == null) return;

        EditorWindow[] windows = Resources.FindObjectsOfTypeAll(projectBrowserType) as EditorWindow[];

        if (windowToUpdate != null)
        {
            windows = new EditorWindow[1] { windowToUpdate };
        }

        for (int i = 0; i < Mathf.Min(windows.Length, pinnedFolders.Count); i++)
        {
            EditorWindow window = windows[windows.Length - i - 1];

            TabInfo tab = (windowToUpdate == null || tabForUpdate == null) ? pinnedFolders[i] : tabForUpdate;

            tab.TargetWindowId = window.GetInstanceID();

            // Set title with colored icon
            Texture2D originalIcon = availableIcons[tab.IconName];
            Texture2D resized = ResizeTexture(originalIcon);
            Texture2D coloredIcon = ChangeTextureColor(resized, tab.Color);

            window.titleContent = new GUIContent(Path.GetFileName(tab.Title), tab.IconName == "textures" ? resized : coloredIcon);

            // Set view mode to TwoColumns
            FieldInfo viewModeField = projectBrowserType.GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (viewModeField != null)
            {
                object twoColumn = System.Enum.Parse(viewModeField.FieldType, "TwoColumns");
                viewModeField.SetValue(window, twoColumn);
            }

            // Find and set the 'isLocked' property
            PropertyInfo isLockedProp = projectBrowserType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (isLockedProp != null)
            {
                isLockedProp.SetValue(window, true);
            }

            try
            {
                // Call ShowFolderContents to apply folder
                MethodInfo showMethod = projectBrowserType.GetMethod("ShowFolderContents", BindingFlags.Instance | BindingFlags.NonPublic);

                if (showMethod != null)
                { 
                    int id = AssetDatabase.LoadAssetAtPath<Object>(tab.FolderPath).GetInstanceID();
                    showMethod.Invoke(window, new object[] { id, true });
                }
            }
            catch
            {

            }

            window.Repaint();
            window.Focus();
        }

        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void OpenNewProjectTab(TabInfo tab)
    {
        if (tab == null)
            return;

        System.Type projectBrowserType = System.Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
        if (projectBrowserType == null) return;

        // Find an existing ProjectBrowser to dock next to
        System.Type[] desiredDockNextTo = new[] { projectBrowserType };

        // Dock the new window using internal CreateWindow<T>(title, dockNextTo)
        MethodInfo createWindowMethod = typeof(EditorWindow).GetMethod("CreateWindow", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(System.Type[]) }, null);
        if (createWindowMethod != null)
        {
            EditorWindow docked = (EditorWindow)createWindowMethod.MakeGenericMethod(projectBrowserType)
                .Invoke(null, new object[] { tab.Title, desiredDockNextTo });

            // Optional: Call Init() method
            MethodInfo initMethod = projectBrowserType.GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
            initMethod?.Invoke(docked, null);

            try
            {
                DelayUpdateProjectTabs(docked, tab);
            }
            catch { }
        }
        else
        {
            // Create a new ProjectBrowser instance
            EditorWindow newWindow = ScriptableObject.CreateInstance(projectBrowserType) as EditorWindow;
            newWindow.titleContent = new GUIContent(tab.Title);
            Debug.LogWarning("Fallback: could not dock, using floating window.");
            newWindow.Show();
        }
    }

    private static double delayedStartTime;
    private static EditorWindow delayedWindow;
    private static TabInfo delayedTab;

    private static void DelayUpdateProjectTabs(EditorWindow docked, TabInfo tab)
    {
        delayedWindow = docked;
        delayedTab = tab;
        delayedStartTime = EditorApplication.timeSinceStartup + 1.0; // 1 second delay
        EditorApplication.update += DelayedUpdateCallback;
    }

    private static void DelayedUpdateCallback()
    {
        if (EditorApplication.timeSinceStartup >= delayedStartTime)
        {
            EditorApplication.update -= DelayedUpdateCallback;

            if (delayedWindow != null && delayedTab != null)
            {
                UpdateProjectTabs(delayedWindow, delayedTab);
            }

            delayedWindow = null;
            delayedTab = null;
        }
    }

    private static Texture2D ChangeTextureColor(Texture2D sourceTexture, Color newColor)
    {
        Texture2D newTexture = new Texture2D(sourceTexture.width, sourceTexture.height);


        for (int x = 0; x < sourceTexture.width; x++)
            for (int y = 0; y < sourceTexture.height; y++)
            {
                Color color = sourceTexture.GetPixel(x, y);
                newColor.a = color.a;
                newTexture.SetPixel(x, y, newColor);
            }

        newTexture.Apply();
        return newTexture;
    }

    private static Texture2D ResizeTexture(Texture2D source)
    {
        int width = 14;
        int height = 14;
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    /// <summary>
    /// Unity caches tab titles and icons, which makes it difficult to update them reliably at runtime.
    /// This workaround creates or updates a dummy `.cs` file to force Unity to recompile,
    /// which clears internal caches and refreshes tab visuals like icons and colors.
    /// </summary>
    private static void TriggerScriptRecompile()
    {
        string dummyPath = "Assets/Editor/ForceRecompile.cs";

        if (!File.Exists(dummyPath))
            File.WriteAllText(dummyPath, "// Force recompile script\n");

        File.SetLastWriteTimeUtc(dummyPath, System.DateTime.UtcNow);

        AssetDatabase.ImportAsset(dummyPath, ImportAssetOptions.ForceUpdate);
    }
}