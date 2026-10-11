using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 挂在每个可交互素材上（由 QuaternionDemoManager 在生成时自动添加）：
/// · 选中时生成黄色外轮廓描边（反壳法，配合 OutlineHighlight.shader）
/// · 每帧用四元数球面线性插值（Slerp）把当前姿态平滑逼近 targetRotation
///   —— 这是拖动旋转"顺滑手感"的核心：拖动只更新目标四元数，实际姿态由 Slerp 追赶
/// </summary>
[DisallowMultipleComponent]
public class SelectableAsset : MonoBehaviour
{
    [Header("旋转平滑度（越大越快贴合目标姿态）")]
    public float rotateSmoothing = 12f;

    [Header("描边颜色 / 宽度（物体空间单位）")]
    public Color outlineColor = new Color(1f, 0.85f, 0f, 1f);
    public float outlineThickness = 0.025f;

    /// <summary>拖动旋转的目标姿态（四元数增量合成的结果）</summary>
    [System.NonSerialized] public Quaternion targetRotation;

    readonly List<GameObject> _outlineParts = new List<GameObject>();
    static Material _outlineMat;

    void Awake()
    {
        targetRotation = transform.rotation;
    }

    /// <summary>选中/取消选中：创建或移除黄色描边</summary>
    public void SetSelected(bool on)
    {
        if (on) CreateOutline();
        else RemoveOutline();
    }

    /// <summary>直接设定姿态并同步目标姿态（自动 SLerp 演示用）</summary>
    public void ApplyImmediate(Quaternion q)
    {
        transform.rotation = q;
        targetRotation = q;
    }

    void LateUpdate()
    {
        // Slerp 平滑：插值因子随帧时长指数收敛，帧率无关
        // 点积接近 1（夹角小于约 0.8°）时视为已到达，停止计算
        if (Quaternion.Dot(transform.rotation, targetRotation) < 0.9999f)
        {
            float k = Mathf.Clamp01(rotateSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
        }
    }

    /// <summary>物体世界包围盒最长边（用于坐标轴线长度）</summary>
    public float BoundsMaxAxis
    {
        get
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r == null) return 1f;
            Vector3 s = r.bounds.size;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }
    }

    /// <summary>物体世界包围盒半径（用于 SLerp 路径圆弧半径）</summary>
    public float BoundsRadius
    {
        get
        {
            Renderer r = GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.extents.magnitude : 0.7f;
        }
    }

    // ==================== 黄色外轮廓描边（反壳法） ====================
    void CreateOutline()
    {
        RemoveOutline();
        EnsureOutlineMaterial();
        if (_outlineMat == null) return;

        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = null;
            if (rend is SkinnedMeshRenderer)
                mesh = ((SkinnedMeshRenderer)rend).sharedMesh;
            else
            {
                MeshFilter mf = rend.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            if (mesh == null) continue;

            // 复制一份网格，用描边材质沿法线外扩渲染，只露出轮廓边缘
            GameObject go = new GameObject("outline");
            go.transform.SetParent(rend.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _outlineMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _outlineParts.Add(go);
        }
        _outlineMat.SetColor("_OutlineColor", outlineColor);
        _outlineMat.SetFloat("_OutlineThickness", outlineThickness);
    }

    void RemoveOutline()
    {
        foreach (GameObject go in _outlineParts)
            if (go != null) Destroy(go);
        _outlineParts.Clear();
    }

    void OnDestroy()
    {
        RemoveOutline();
    }

    static void EnsureOutlineMaterial()
    {
        if (_outlineMat != null) return;
        Shader sh = Shader.Find("Custom/OutlineHighlight");
        if (sh == null)
        {
            Debug.LogWarning("找不到 Custom/OutlineHighlight shader，请确认 OutlineHighlight.shader 已放入项目 Assets 中");
            return;
        }
        _outlineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
    }
}
