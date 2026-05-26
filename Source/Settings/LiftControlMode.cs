namespace Universal_Lift_Structure;

public enum LiftControlMode
{
    // 远程：玩家直接点击 Gizmo 触发，无需小人到场
    Remote = 0,

    // 控制台：小人须走到 ULS_LiftConsole 前才能触发
    Console = 1,

    // 手动：玩家点击后创建 Job，小人走到控制器前执行扳动动作
    Manual = 2
}
