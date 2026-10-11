using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 运行时 .obj 外部素材加载器
/// 支持：v / vt / vn / f；三角形与多边形面；负索引；f v、f v//vn、f v/vt、f v/vt/vn 四种格式
/// 加载后网格自动归一化：中心平移到原点、最长边缩放到 1.5 个单位，便于演示
/// 用法：把 .obj 文件放入 Assets/StreamingAssets/Models/，运行时通过面板按钮导入
/// </summary>
public static class ObjLoader
{
    static Material _defaultMat;

    /// <summary>导入网格使用的默认材质</summary>
    public static Material DefaultMaterial
    {
        get
        {
            if (_defaultMat == null)
            {
                Shader sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                if (sh != null) _defaultMat = new Material(sh);
            }
            return _defaultMat;
        }
    }

    public static Mesh Load(string path)
    {
        try
        {
            Mesh m = Parse(File.ReadAllLines(path));
            if (m != null) m.name = Path.GetFileNameWithoutExtension(path);
            return m;
        }
        catch (Exception e)
        {
            Debug.LogWarning("OBJ 加载失败: " + path + "\n" + e.Message);
            return null;
        }
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    /// <summary>OBJ 1-based 索引（含负数）→ 列表 1-based 索引</summary>
    static int Idx(string s, int count)
    {
        int i = int.Parse(s, CultureInfo.InvariantCulture);
        return i > 0 ? i : count + i + 1;
    }

    static Mesh Parse(string[] lines)
    {
        var v  = new List<Vector3>();   // 顶点
        var vt = new List<Vector2>();   // 纹理坐标
        var vn = new List<Vector3>();   // 法线
        var outV  = new List<Vector3>();  // 展开后的顶点（每个面角独立，处理简单可靠）
        var outUv = new List<Vector2>();
        var outN  = new List<Vector3>();
        var tris  = new List<int>();
        bool hasUv = false, hasN = false;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length < 2 || line[0] == '#') continue;
            string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (p[0] == "v")
            {
                v.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
            }
            else if (p[0] == "vt")
            {
                vt.Add(new Vector2(F(p[1]), F(p[2])));
            }
            else if (p[0] == "vn")
            {
                vn.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
            }
            else if (p[0] == "f")
            {
                // 解析每个面角 (顶点, 纹理, 法线)
                var corners = new List<Vector3Int>(p.Length - 1);
                for (int i = 1; i < p.Length; i++)
                {
                    string[] seg = p[i].Split('/');
                    int vi = Idx(seg[0], v.Count);
                    int ti = seg.Length > 1 && seg[1].Length > 0 ? Idx(seg[1], vt.Count) : 0;
                    int ni = seg.Length > 2 && seg[2].Length > 0 ? Idx(seg[2], vn.Count) : 0;
                    corners.Add(new Vector3Int(vi, ti, ni));
                }
                // 扇形三角化；OBJ 逆时针为正面、Unity 顺时针为正面 → 反转绕序
                for (int i = 1; i + 1 < corners.Count; i++)
                {
                    tris.Add(AddCorner(corners[0],     v, vt, vn, outV, outUv, outN, ref hasUv, ref hasN));
                    tris.Add(AddCorner(corners[i + 1], v, vt, vn, outV, outUv, outN, ref hasUv, ref hasN));
                    tris.Add(AddCorner(corners[i],     v, vt, vn, outV, outUv, outN, ref hasUv, ref hasN));
                }
            }
        }

        if (outV.Count < 3 || tris.Count == 0) return null;

        var mesh = new Mesh();
        mesh.indexFormat = outV.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(outV);
        if (hasUv && outUv.Count == outV.Count) mesh.SetUVs(0, outUv);
        if (hasN && outN.Count == outV.Count) mesh.SetNormals(outN);
        mesh.SetTriangles(tris, 0, calculateBounds: false);
        if (!hasN) mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 归一化：中心移到原点，最长边缩放到 1.5（均匀缩放不影响法线方向）
        Bounds b = mesh.bounds;
        float maxAxis = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxAxis > 1e-5f)
        {
            float s = 1.5f / maxAxis;
            Vector3[] verts = outV.ToArray();
            for (int i = 0; i < verts.Length; i++)
                verts[i] = (verts[i] - b.center) * s;
            mesh.SetVertices(new List<Vector3>(verts));
            mesh.RecalculateBounds();
        }
        return mesh;
    }

    static int AddCorner(Vector3Int c, List<Vector3> v, List<Vector2> vt, List<Vector3> vn,
                         List<Vector3> outV, List<Vector2> outUv, List<Vector3> outN,
                         ref bool hasUv, ref bool hasN)
    {
        outV.Add(v[c.x - 1]);
        if (c.y > 0 && c.y <= vt.Count) { outUv.Add(vt[c.y - 1]); hasUv = true; }
        else outUv.Add(Vector2.zero);
        if (c.z > 0 && c.z <= vn.Count) { outN.Add(vn[c.z - 1]); hasN = true; }
        else outN.Add(Vector3.zero);
        return outV.Count - 1;
    }
}
