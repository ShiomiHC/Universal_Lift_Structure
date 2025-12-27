namespace Universal_Lift_Structure;

/// 枚举意图：定义“升降触发方式”的全局控制模式。
/// - Remote：超凡遥控器，Gizmo 直接触发升/降（立即生效）。
/// - Console：控制台，Gizmo 仅提交请求，等待殖民者对控制台执行 flick 后触发升/降。
/// - Manual：手动开关，Gizmo 仅提交请求，等待殖民者对控制器本体执行 flick 后触发升/降。

public enum LiftControlMode
{
    Remote,
    Console,
    Manual
}
