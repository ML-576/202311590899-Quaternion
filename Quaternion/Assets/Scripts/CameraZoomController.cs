using UnityEngine;

/// <summary>
/// 摄像机滚轮缩放：无论是否选中素材，鼠标中键（滚轮）均控制视角距离缩放
/// —— 沿视线方向推近 / 拉远，并限制与场景原点的距离范围
/// 挂载方式：挂到主摄像机（Main Camera）上
/// </summary>
public class CameraZoomController : MonoBehaviour
{
    [Header("每格滚轮的移动距离")]
    public float zoomSpeed = 3f;

    [Header("距场景原点的距离范围")]
    public float minDistance = 1.5f;
    public float maxDistance = 40f;

    void LateUpdate()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        Vector3 pos = transform.position + transform.forward * (scroll * zoomSpeed);
        float d = pos.magnitude;
        if (d < 0.001f) return;
        d = Mathf.Clamp(d, minDistance, maxDistance);
        transform.position = pos.normalized * d;
    }
}
