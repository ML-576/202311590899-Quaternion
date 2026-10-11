#将文件内`CameraZoomController`做主摄像机的组件，并创建一个空对象并用`QuaternionDemoManager`做其组件
`SelectableAsset`“素材”组件：黄色描边的创建/移除 + 每帧 Slerp 平滑旋转，Manager 生成每个素材（图元/预制体/OBJ）时自动`AddComponent` ，运行时在 Hierarchy 展开素材能看到它
`ObjLoader`静态工具类：解析`.obj` 文本生成网格，点“浏览导入”或 Models 列表按钮时被 Manager 调用
`OutlineHighlight`着色器资源：反壳法描边--选中素材时生成黄色外轮廓
