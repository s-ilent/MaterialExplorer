using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Rendering;

public class MaterialAnalyzer : EditorWindow
{
    private static Dictionary<Material, AnalyzerData> materialDicts;
    private static List<AnalyzerData> analyzerList;
    public CustomColorPropDef customColorProps;
    Vector2 scrollPos;
    private GUIStyle matBoxStyle;
    private RenderPipeType renderPipeType;
    private Color tintColor;
    MultiColumnHeader columnHeader;
    MultiColumnHeaderState.Column[] columns;
    private int sortedColumnIndex = 0;
    private bool ascending = true;

    [MenuItem("Window/Rendering/Material Explorer", false, 3)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(MaterialAnalyzer));
    }

    void OnEnable()
    {
        matBoxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            margin = new RectOffset(0, 0, 10, 10)
        };
        EditorApplication.hierarchyChanged += OnHierarchyChange;
        FindMaterials();
        DetectRenderPipe();
        InitColumns();
    }

    void InitColumns()
    {
        columns = new MultiColumnHeaderState.Column[]
        {
            new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Material Name"),
                width = 100,
                minWidth = 100,
                maxWidth = 500,
                autoResize = true,
                headerTextAlignment = TextAlignment.Center
            },
            new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Shader"),
                width = 100,
                minWidth = 100,
                maxWidth = 500,
                autoResize = true,
                headerTextAlignment = TextAlignment.Center
            },
            new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Color"),
                width = 100,
                minWidth = 100,
                maxWidth = 500,
                autoResize = true,
                headerTextAlignment = TextAlignment.Center
            },
            new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("GameObjects"),
                width = 100,
                minWidth = 100,
                maxWidth = 500,
                autoResize = true,
                headerTextAlignment = TextAlignment.Center
            }
        };
        columnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
        {
            height = 25
        };
        columnHeader.ResizeToFit();
        columnHeader.sortingChanged += OnSortingChanged;
    }

    void OnSortingChanged(MultiColumnHeader multiColumnHeader)
    {
        sortedColumnIndex = multiColumnHeader.sortedColumnIndex;
        ascending = multiColumnHeader.IsSortedAscending(sortedColumnIndex);
        SortAnalyzerList();
    }

    void SortAnalyzerList()
    {
        switch (sortedColumnIndex)
        {
            case 0:
                analyzerList = ascending ? analyzerList.OrderBy(x => x.matName).ToList() : analyzerList.OrderByDescending(x => x.matName).ToList();
                break;
            case 1:
                analyzerList = ascending ? analyzerList.OrderBy(x => x.shader.name).ToList() : analyzerList.OrderByDescending(x => x.shader.name).ToList();
                break;
            case 2:
                analyzerList = ascending ? analyzerList.OrderBy(x => GetMaterialColor(x.mat), new ColorComparer()).ToList() : analyzerList.OrderByDescending(x => GetMaterialColor(x.mat), new ColorComparer()).ToList();
                break;
            default:
                analyzerList = ascending ? analyzerList.OrderBy(x => x.matName).ToList() : analyzerList.OrderByDescending(x => x.matName).ToList();
                break;
        }
    }

    Color GetMaterialColor(Material mat)
    {
        string colorProp = SetColorProperty(mat);
        return !string.IsNullOrEmpty(colorProp) ? mat.GetColor(colorProp) : Color.clear;
    }

    void DetectRenderPipe()
    {
        if (GraphicsSettings.currentRenderPipeline)
        {
            renderPipeType = GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition") ? RenderPipeType.HighDefinition : RenderPipeType.URP;
        }
        else
        {
            renderPipeType = RenderPipeType.Default;
        }
    }

    string SetColorProperty(Material mat)
    {
        string colorProp = renderPipeType switch
        {
            RenderPipeType.URP or RenderPipeType.HighDefinition => mat.HasProperty("_BaseColor") ? "_BaseColor" : customColorProps.CustomColorPropDefines.FirstOrDefault(mat.HasProperty),
            RenderPipeType.Default => mat.HasProperty("_Color") ? "_Color" : customColorProps.CustomColorPropDefines.FirstOrDefault(mat.HasProperty),
            _ => ""
        };
        return colorProp;
    }
void OnGUI()
{
    if (analyzerList.Count > 0)
    {
        GUILayout.BeginVertical();
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true); 

        Rect headerRect = GUILayoutUtility.GetRect(0, columnHeader.height, GUILayout.ExpandWidth(true));
        columnHeader.OnGUI(headerRect, 0);

        foreach (AnalyzerData data in analyzerList)
        {
            GUILayout.BeginHorizontal(matBoxStyle);
            Rect previewTextureRect = GUILayoutUtility.GetRect(128, 128, 128, 128, GUILayout.Width(128));
            GUI.DrawTexture(previewTextureRect, AssetPreview.GetAssetPreview(data.mat));

            GUILayout.BeginVertical(GUILayout.Width((position.width - (128+32)) / 2)); // Adjust width to account for padding
            Material temp = (Material)EditorGUILayout.ObjectField(data.mat, typeof(Material), false, GUILayout.ExpandWidth(true));
            EditorGUILayout.ObjectField(data.shader, typeof(Shader), false, GUILayout.ExpandWidth(true));
            string colorProp = SetColorProperty(data.mat);

            if (!string.IsNullOrEmpty(colorProp))
            {
                EditorGUI.BeginChangeCheck();
                tintColor = EditorGUILayout.ColorField(data.mat.GetColor(colorProp), GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data.mat, "Base color change on " + data.mat.name);
                    data.mat.SetColor(colorProp, tintColor);
                }
            }

            string keywords = string.Join(" ", data.shaderKeywords);
            GUILayout.TextArea(keywords, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width((position.width - (128+32)) / 2)); // Adjust width to account for padding
            data.ShowGameObject = EditorGUILayout.Foldout(data.ShowGameObject, "Show " + data.gameObjects.Count + " GameObjects", EditorStyles.foldout);
            if (data.ShowGameObject)
            {
                foreach (GameObject go in data.gameObjects)
                {
                    EditorGUILayout.ObjectField(go, typeof(GameObject), false, GUILayout.ExpandWidth(true));
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
}


    void OnHierarchyChange()
    {
        FindMaterials();
    }

    private void FindMaterials()
    {
        materialDicts = new Dictionary<Material, AnalyzerData>();
        analyzerList = new List<AnalyzerData>();
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (GameObject go in allObjects)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                foreach (Material mat in rend.sharedMaterials)
                {
                    if (mat != null)
                    {
                        if (!materialDicts.ContainsKey(mat))
                        {
                            AnalyzerData data = new AnalyzerData(mat.name, mat, go, mat.shaderKeywords, mat.shader);
                            materialDicts.Add(mat, data);
                        }
                        else
                        {
                            materialDicts[mat].gameObjects.Add(go);
                        }
                    }
                }
            }
        }

        analyzerList = materialDicts.Values.OrderBy(x => x.matName).ToList();
        SortAnalyzerList();
    }
}

public class AnalyzerData
{
    public string matName;
    public Material mat;
    public HashSet<GameObject> gameObjects;
    public string[] shaderKeywords;
    public Shader shader;
    public bool ShowGameObject;

    public AnalyzerData(string matName, Material mat, GameObject gameObject, string[] shaderKeywords, Shader shader)
    {
        this.matName = matName;
        this.mat = mat;
        this.gameObjects = new HashSet<GameObject> { gameObject };
        this.shaderKeywords = shaderKeywords;
        this.shader = shader;
        ShowGameObject = false;
    }
}

public enum RenderPipeType
{
    HighDefinition,
    URP,
    Default
}

public class ColorComparer : IComparer<Color>
{
    public int Compare(Color x, Color y)
    {
        if (x.r != y.r) return x.r.CompareTo(y.r);
        if (x.g != y.g) return x.g.CompareTo(y.g);
        if (x.b != y.b) return x.b.CompareTo(y.b);
        return x.a.CompareTo(y.a);
    }
}
