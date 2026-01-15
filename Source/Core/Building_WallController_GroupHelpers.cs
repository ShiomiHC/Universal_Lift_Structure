namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // ============================================================
    // 【尝试获取有效的组内所有单元格】
    // ============================================================
    // 验证并获取指定组的控制器单元格列表
    //
    // 【参数说明】
    // - map: 当前地图
    // - groupId: 组ID
    // - maxSize: 最大允许数量
    // - showMessage: 是否显示消息
    // - emptyGroupMessageKey: 空组提示Key
    // - cells: 输出单元格列表
    //
    // 【返回值】
    // - true: 获取成功
    // - false: 获取失败（组ID无效、组为空、或超过大小限制）
    // ============================================================
    private bool TryGetValidGroupCells(
        Map map,
        int groupId,
        int maxSize,
        bool showMessage,
        string emptyGroupMessageKey,
        out List<IntVec3> cells)
    {
        cells = null;
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();

        // 验证组件和组数据是否有效
        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out cells) ||
            cells == null ||
            cells.Count == 0)
        {
            if (showMessage && !string.IsNullOrEmpty(emptyGroupMessageKey))
            {
                MessageReject(emptyGroupMessageKey, this);
            }

            return false;
        }

        // 验证组大小是否超限
        if (cells.Count > maxSize)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupTooLarge", this, maxSize);
            }

            return false;
        }

        return true;
    }

    // ============================================================
    // 【构建唯一的根单元格列表】
    // ============================================================
    // 遍历单元格列表，提取唯一的控制器根位置（处理多格建筑去重）
    //
    // 【参数说明】
    // - map: 地图
    // - cells: 所有单元格
    // - uniqueRootCells: 输出唯一根列表
    // - seenRoots: 已访问根的集合缓存（用于去重）
    // ============================================================
    private void BuildUniqueRootCells(
        Map map,
        List<IntVec3> cells,
        List<IntVec3> uniqueRootCells,
        HashSet<IntVec3> seenRoots)
    {
        foreach (var cell in cells)
        {
            // 尝试获取该位置的控制器
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                // 如果是多格组的一部分，使用根位置；否则使用自身位置
                IntVec3 rootCell = controller.MultiCellGroupRootCell.IsValid
                    ? controller.MultiCellGroupRootCell
                    : controller.Position;

                // 首次遇到的根位置加入列表
                if (seenRoots.Add(rootCell))
                {
                    uniqueRootCells.Add(rootCell);
                }
            }
            else if (seenRoots.Add(cell))
            {
                uniqueRootCells.Add(cell);
            }
        }
    }

    // ============================================================
    // 【检查组电力状态】
    // ============================================================
    // 检查组内所有控制器是否电力充足
    //
    // 【参数说明】
    // - map: 地图
    // - uniqueRootCells: 唯一的控制器根位置列表
    // - showMessage: 是否显示不足提示
    //
    // 【返回值】
    // - true: 全部就绪或电力特性未启用
    // - false: 存在电力不足的控制器
    // ============================================================
    private bool CheckGroupPowerReady(
        Map map,
        List<IntVec3> uniqueRootCells,
        bool showMessage)
    {
        if (!PowerFeatureEnabled)
        {
            return true;
        }

        foreach (var t in uniqueRootCells)
        {
            // 只要有一个控制器电力不足，即视为组电力不足
            if (ULS_Utility.TryGetControllerAt(map, t, out var controller) &&
                !controller.IsReadyForLiftPower())
            {
                if (showMessage)
                {
                    MessageReject("ULS_GroupPowerInsufficient", controller);
                }

                return false;
            }
        }

        return true;
    }
}
