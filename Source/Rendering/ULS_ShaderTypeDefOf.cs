namespace Universal_Lift_Structure;

// ============================================================
// 【ShaderTypeDef 引用容器】
// ============================================================
// 此类使用 RimWorld 的 DefOf 系统，提供对 XML 定义的 ShaderTypeDef 的静态引用
//
// 【ShaderTypeDef 说明】
// ShaderTypeDef 是 RimWorld 对 Unity Shader 的封装
// 允许通过 XML 定义和配置 Shader，而不是硬编码在 C# 中
//
// 【Shader 的作用】
// Shader（着色器）是 GPU 上运行的图形渲染程序
// 控制物体的显示效果（颜色、纹理、透明度、特效等）
//
// 【本项目中的应用】
// ULS_GhostEdgeDotted：用于绘制虚影的边缘虚线效果
// 用途：当建筑被收纳时，在其原位置显示半透明的虚影轮廓
// 效果：使用点状虚线边缘，区别于实体建筑的实线边缘
//
// 【对应 XML】
// Defs/ShaderTypeDefs/ULS_GhostEdgeDotted.xml
// 定义了 Shader 的路径、渲染队列、混合模式等参数
// ============================================================
[DefOf]
public static class ULS_ShaderTypeDefOf
{
    // ============================================================
    // 【静态构造函数】
    // ============================================================
    // 在类首次被访问时调用，确保 DefOf 系统正确初始化
    // ============================================================
    static ULS_ShaderTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ShaderTypeDefOf));
    }

    // ============================================================
    // 【ShaderTypeDef 引用字段】
    // ============================================================

    // 虚影边缘虚线着色器
    // 用途：为收纳建筑的虚影绘制点状虚线边缘效果
    // 应用场景：
    //   - 建筑被升降机收纳后，在原位置显示虚影
    //   - 虚影使用半透明材质，边缘使用点状虚线，便于区分
    // 关联渲染：ULS_GhostRenderer 中使用此 Shader 创建材质
    // 对应 XML：Defs/ShaderTypeDefs/ULS_GhostEdgeDotted.xml
    public static ShaderTypeDef ULS_GhostEdgeDotted;
}