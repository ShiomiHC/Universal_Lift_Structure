// ============================================================
// 【全局引用文件】
// ============================================================
// 此文件使用 C# 10.0 的 global using 特性，为整个项目导入常用命名空间
//
// 【global using 的作用】
// - 在此文件中声明的 global using 会自动应用到项目的所有 C# 文件
// - 其他文件无需重复写 using 语句，可直接使用这些命名空间中的类型
//
// 【优势】
// 1. 减少代码重复：避免每个文件都写相同的 using 语句
// 2. 统一管理：所有全局引用集中在一个文件中，便于维护
// 3. 提高可读性：减少文件头部的样板代码
//
// 【注意事项】
// - 仅包含项目中频繁使用的命名空间
// - 特定用途的命名空间应在各自文件中局部引用
// ============================================================

// .NET 基础库
// 提供核心类型和基础功能

global using System; // 基本类型、异常、委托等
global using System.Collections.Generic; // 泛型集合（List、Dictionary、HashSet 等）
global using System.Linq; // LINQ 查询扩展方法

// Harmony 补丁库
// 用于运行时修改游戏原版代码
global using HarmonyLib; // Harmony 补丁框架，支持 Prefix、Postfix、Transpiler 等

// RimWorld 核心库
global using RimWorld; // RimWorld 高层 API（游戏逻辑、UI、AI 等）
global using Verse; // Verse 底层引擎（地图、物体、渲染、序列化等）

// Unity 引擎
// 提供图形、物理、输入等底层功能
global using UnityEngine; // Unity 引擎核心（Vector3、Rect、Color、Texture2D 等）
