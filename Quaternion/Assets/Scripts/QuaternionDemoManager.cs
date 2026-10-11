using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 四元数旋转变换演示程序 —— 总控脚本
/// 1. 生成/导入 3D 素材：内置图元、Inspector 指定的预制体、StreamingAssets/Models 下的外部 .obj 文件
/// 2. 鼠标左键点选素材（多个素材重叠时，自动选中最靠近摄像机的那个，即与鼠标"接触"的外围素材）
/// 3. 按住左键拖动 → 轨迹球式旋转素材：以摄像机视口坐标轴为旋转轴做四元数增量合成，
///    再用球面线性插值（Slerp）平滑逼近目标姿态（核心知识点）
/// 4. 选中素材时显示其本地旋转轴朝向（X红/Y绿/Z蓝）与 SLerp 插值路径大圆弧
/// 挂载方式：放到场景中任意空物体上即可
/// </summary>
public class QuaternionDemoManager : MonoBehaviour
{
    public static QuaternionDemoManager Instance { get; private set; }

    [Header("外部预制体素材（可拖入 FBX/OBJ 预制体，运行时按钮生成）")]
    public GameObject[] extraPrefabs;

    [Header("拖动旋转灵敏度（度/像素）")]
    public float dragSensitivity = 0.9f;   // 原值 0.6，提高 1.5 倍

    [Header("自动 SLerp 演示开关（也可在面板中勾选）")]
    public bool autoSlerpDemo = false;

    [Header("自动 SLerp 演示速度（次/秒）")]
    public float autoSlerpSpeed = 0.6f;

    /// <summary>当前选中的素材</summary>
    public SelectableAsset Selected { get; private set; }
    public bool HasSelection { get { return Selected != null; } }

    const float PanelWidth = 330f;   // 左上角控制面板宽度（该区域内的点击不作用于场景）

    bool _dragging;                  // 是否正按住素材拖动
    bool _mouseDownOnEmpty;          // 本次左键是否按下在空白处（区分"点空白取消选中"与"空白处拖拽旋转"）
    Vector3 _mouseDownScreenPos;     // 左键按下时的屏幕位置（与 mousePosition 同为 Vector3）
    const float ClickMaxPixels = 6f; // 判定为"单击"的最大位移（像素）
    float _slerpTime;                // 自动演示计时
    Quaternion _slerpA, _slerpB;     // 自动演示的两个关键姿态四元数
    readonly List<string> _objFiles = new List<string>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 扫描 StreamingAssets/Models 目录下的外部 .obj 素材
        string dir = Path.Combine(Application.streamingAssetsPath, "Models");
        if (Directory.Exists(dir))
        {
            foreach (string f in Directory.GetFiles(dir, "*.obj"))
                _objFiles.Add(f);
        }

        // 初始生成一个立方体，方便立即测试
        SpawnPrimitive(PrimitiveType.Cube);
    }

    void Update()
    {
        HandleMouseInput();

        if (Input.GetKeyDown(KeyCode.Delete))
            DeleteSelected();

        // 自动 SLerp 演示：在两个姿态四元数之间做球面线性插值
        if (autoSlerpDemo && Selected != null && !_dragging)
        {
            _slerpTime += Time.deltaTime;
            float t = Mathf.PingPong(_slerpTime * autoSlerpSpeed, 1f);
            Selected.ApplyImmediate(Quaternion.Slerp(_slerpA, _slerpB, t));
        }
    }

    // ==================== 鼠标交互：点选 + 按住拖动旋转 ====================
    void HandleMouseInput()
    {
        // 忽略控制面板区域（屏幕左侧）内的点击
        bool overPanel = Input.mousePosition.x < PanelWidth;

        if (Input.GetMouseButtonDown(0) && !overPanel)
        {
            _mouseDownScreenPos = Input.mousePosition;
            _mouseDownOnEmpty = false;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 取所有命中并按距离排序：多个素材重叠时，选中最外围（距离摄像机最近）的那个
            RaycastHit[] hits = Physics.RaycastAll(ray);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            SelectableAsset sel = null;
            if (hits.Length > 0)
                sel = hits[0].collider.GetComponentInParent<SelectableAsset>();

            if (sel != null)
            {
                Select(sel);              // 点到素材：选中它并准备拖拽
                _dragging = true;
            }
            else
            {
                // 点到空白：不立即取消选中；
                // 若已有选中素材，则在空白处按住拖拽同样旋转它，松开时再按位移判断是否为"单击取消选中"
                _mouseDownOnEmpty = true;
                _dragging = Selected != null;
            }
        }

        // 按住左键拖动 → 素材跟随鼠标方向旋转（轨迹球）
        if (Input.GetMouseButton(0) && _dragging && Selected != null)
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(dx) > 0f || Mathf.Abs(dy) > 0f)
            {
                // 旋转轴取摄像机视口坐标轴（世界系），符合直觉的轨迹球手感
                Vector3 camRight = Camera.main.transform.right;
                Vector3 camUp = Camera.main.transform.up;

                // 鼠标右移 → 绕视口上轴旋转；鼠标上移 → 绕视口右轴旋转
                Quaternion delta = Quaternion.AngleAxis(-dx * dragSensitivity, camUp)
                                 * Quaternion.AngleAxis( dy * dragSensitivity, camRight);

                // 四元数世界系增量合成：目标姿态 = 增量姿态 × 当前目标姿态
                Selected.targetRotation = delta * Selected.targetRotation;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            // 只有空白处的"单击"（几乎无位移）才取消选中；空白处拖拽属于旋转操作
            if (_mouseDownOnEmpty &&
                (Input.mousePosition - _mouseDownScreenPos).sqrMagnitude < ClickMaxPixels * ClickMaxPixels)
            {
                Select(null);
            }

            if (_dragging && autoSlerpDemo && Selected != null)
                RestartSlerpDemo();   // 拖动结束后从当前姿态无缝继续自动演示
            _dragging = false;
            _mouseDownOnEmpty = false;
        }
    }

    public void Select(SelectableAsset sel)
    {
        if (Selected == sel) return;
        if (Selected != null) Selected.SetSelected(false);
        Selected = sel;
        if (Selected != null)
        {
            Selected.SetSelected(true);
            RestartSlerpDemo();
        }
    }

    /// <summary>以当前选中素材的姿态为起点，构造一对演示用四元数（绕 (1,1,1) 轴转 120°）</summary>
    void RestartSlerpDemo()
    {
        _slerpTime = 0f;
        if (Selected != null)
        {
            _slerpA = Selected.transform.rotation;
            _slerpB = _slerpA * Quaternion.AngleAxis(120f, new Vector3(1f, 1f, 1f).normalized);
        }
    }

    // ==================== 素材生成 ====================
    Vector3 SpawnPoint
    {
        get
        {
            Camera cam = Camera.main;
            // 固定生成在摄像机前方：多次生成会重叠，用于演示"重叠时选中最外围素材"
            return cam != null ? cam.transform.position + cam.transform.forward * 4f
                               : Vector3.forward * 4f;
        }
    }

    void Register(GameObject go, string name)
    {
        go.name = name;
        go.transform.position = SpawnPoint;
        if (go.GetComponent<SelectableAsset>() == null)
            go.AddComponent<SelectableAsset>();
        EnsureColliders(go);
    }

    /// <summary>预制体可能不带碰撞体，补齐以便射线检测</summary>
    static void EnsureColliders(GameObject root)
    {
        if (root.GetComponentInChildren<Collider>() != null) return;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>();
    }

    /// <summary>按 PrimitiveType 枚举直接生成图元（Cube / Sphere / Cylinder / Capsule）</summary>
    public void SpawnPrimitive(PrimitiveType pt)
    {
        GameObject go = GameObject.CreatePrimitive(pt);
        Register(go, "图元_" + pt);
    }

    public void SpawnPrefab(int index)
    {
        if (extraPrefabs == null || index < 0 || index >= extraPrefabs.Length || extraPrefabs[index] == null)
            return;
        GameObject go = Instantiate(extraPrefabs[index]);
        Register(go, extraPrefabs[index].name);
    }

    public void SpawnObj(int index)
    {
        if (index < 0 || index >= _objFiles.Count) return;
        LoadAndSpawnObj(_objFiles[index]);
    }

    /// <summary>加载指定路径的 .obj 并生成素材（供文件对话框与 StreamingAssets 列表共用）</summary>
    void LoadAndSpawnObj(string path)
    {
        Mesh mesh = ObjLoader.Load(path);
        if (mesh == null) return;

        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = ObjLoader.DefaultMaterial;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        Register(go, mesh.name);
    }

    /// <summary>打开系统文件选择对话框，从任意目录导入 .obj 素材（编辑器环境）</summary>
    void ImportObjFromDialog()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("选择 OBJ 模型文件", "", "obj");
        if (string.IsNullOrEmpty(path)) return;
        LoadAndSpawnObj(path);
#else
        Debug.LogWarning("文件选择对话框仅在 Unity 编辑器中可用；打包运行时请将 .obj 放入 Assets/StreamingAssets/Models 后用列表按钮导入");
#endif
    }

    public void DeleteSelected()
    {
        if (Selected == null) return;
        Destroy(Selected.gameObject);
        Selected = null;
    }

    // ==================== 左上角 OnGUI 控制面板 ====================
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, PanelWidth - 10, Screen.height - 20));
        GUILayout.Box("四元数旋转变换演示（SLerp）", GUILayout.Width(300));

        GUILayout.Label("生成内置素材：");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("立方体")) SpawnPrimitive(PrimitiveType.Cube);
        if (GUILayout.Button("球体"))   SpawnPrimitive(PrimitiveType.Sphere);
        if (GUILayout.Button("圆柱体")) SpawnPrimitive(PrimitiveType.Cylinder);
        if (GUILayout.Button("胶囊体")) SpawnPrimitive(PrimitiveType.Capsule);
        GUILayout.EndHorizontal();

        if (extraPrefabs != null && extraPrefabs.Length > 0)
        {
            GUILayout.Label("生成预制体素材：");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < extraPrefabs.Length; i++)
            {
                string label = extraPrefabs[i] != null ? extraPrefabs[i].name : "空" + i;
                if (GUILayout.Button(label)) SpawnPrefab(i);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Label("导入外部素材：");
        // 运行时文件选择对话框：从电脑任意位置选取 .obj 导入，无需提前放入 Models 目录
        if (GUILayout.Button("浏览导入 .obj 模型…"))
            ImportObjFromDialog();

        GUILayout.Label("StreamingAssets/Models 下的 .obj：");
        if (_objFiles.Count == 0)
            GUILayout.Label("  （该目录未找到 .obj，可忽略，直接用上方按钮导入）");
        else
            for (int i = 0; i < _objFiles.Count; i++)
                if (GUILayout.Button("导入 " + Path.GetFileName(_objFiles[i])))
                    SpawnObj(i);

        GUILayout.Space(6);
        bool demo = GUILayout.Toggle(autoSlerpDemo, "自动 SLerp 演示（需选中素材）");
        if (demo != autoSlerpDemo)
        {
            autoSlerpDemo = demo;
            RestartSlerpDemo();
        }

        if (Selected != null)
        {
            Quaternion q = Selected.transform.rotation;
            Vector3 e = q.eulerAngles;
            GUILayout.Space(6);
            GUILayout.Label("已选中：" + Selected.name);
            GUILayout.Label(string.Format("四元数 (x,y,z,w)：({0:F2}, {1:F2}, {2:F2}, {3:F2})", q.x, q.y, q.z, q.w));
            GUILayout.Label(string.Format("欧拉角：({0:F1}, {1:F1}, {2:F1})", e.x, e.y, e.z));
            if (autoSlerpDemo)
                GUILayout.Label(string.Format("SLerp 插值参数 t = {0:F2}",
                    Mathf.PingPong(_slerpTime * autoSlerpSpeed, 1f)));
            if (GUILayout.Button("删除选中素材（Delete）"))
                DeleteSelected();
        }
        else
        {
            GUILayout.Space(6);
            GUILayout.Label("未选中素材");
        }

        GUILayout.Space(6);
        GUILayout.Label("操作说明：");
        GUILayout.Label("· 左键点击素材 = 选中（黄色描边 + 坐标轴）");
        GUILayout.Label("· 左键按住拖动 = 轨迹球旋转（Slerp 平滑）");
        GUILayout.Label("· 空白处单击左键 = 取消选中");
        GUILayout.Label("· 空白处按住左键拖拽 = 旋转选中素材");
        GUILayout.Label("· 滚轮 = 视角拉近 / 拉远");
        GUILayout.EndArea();
    }

    // ==================== 旋转轴朝向 + SLerp 路径 可视化 ====================
    static Material _lineMat;

    void OnRenderObject()
    {
        if (Selected == null) return;
        EnsureLineMaterial();

        GL.PushMatrix();
        _lineMat.SetPass(0);

        Transform t = Selected.transform;
        float len = Selected.BoundsMaxAxis * 1.6f;

        // 1. 当前素材的旋转轴朝向：本地 X/Y/Z 轴（红/绿/蓝），随旋转实时变化
        GL.Begin(GL.LINES);
        GL.Color(Color.red);   GL.Vertex(t.position); GL.Vertex(t.position + t.right   * len);
        GL.Color(Color.green); GL.Vertex(t.position); GL.Vertex(t.position + t.up      * len);
        GL.Color(Color.blue);  GL.Vertex(t.position); GL.Vertex(t.position + t.forward * len);
        GL.End();

        // 2. 自动演示时：绘制 Slerp 插值路径（姿态在单位球面上扫出的大圆弧）
        if (autoSlerpDemo)
        {
            GL.Begin(GL.LINE_STRIP);
            GL.Color(new Color(0f, 1f, 1f, 0.9f));
            float r = Selected.BoundsRadius * 1.6f;
            Vector3 v = Vector3.up * r;
            for (int i = 0; i <= 40; i++)
            {
                Quaternion qi = Quaternion.Slerp(_slerpA, _slerpB, i / 40f);
                GL.Vertex(t.position + qi * v);
            }
            GL.End();
        }

        GL.PopMatrix();
    }

    static void EnsureLineMaterial()
    {
        if (_lineMat != null) return;
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _lineMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _lineMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _lineMat.SetInt("_Cull", (int)CullMode.Off);
        _lineMat.SetInt("_ZWrite", 0);
    }
}
