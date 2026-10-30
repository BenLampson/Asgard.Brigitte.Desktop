namespace Asgard.Brigitte.Desktop.Core;

/// <summary>桌面在线表单和本机工作区共用流程规则，线上仍由服务端最终校验。</summary>
public static class BugWorkflow
{
    /// <summary>历史状态仅允许保留；人工可从任意状态关闭，部署和人工验收仍校验前置状态。</summary>
    public static bool CanTransition(string from, string to) => BugDisplay.Statuses.ContainsKey(to) &&
        (from == to || (to switch
        {
            "fixed" or "regressed" => false,
            "pending_regression" => from is "pending_deployment" or "fixed" or "regressed",
            "human_verified" => from == "pending_regression",
            _ => true
        }));
    /// <summary>交付和结束记录必须说明依据，不能以 AI 自测代替人工验收。</summary>
    public static bool RequiresReason(string status) => status is "pending_deployment" or "pending_regression" or
        "human_verified" or "closed" or "wont_fix" or "duplicate" or "cannot_reproduce";
    /// <summary>保存时用最新状态复核，避免窗口打开后状态变化导致错误操作。</summary>
    public static void Validate(string from, string to, string? reason)
    {
        if (!CanTransition(from, to)) throw new InvalidOperationException(ClientText.T("状态已变化或流转不允许，请重新打开窗口。"));
        if (from != to && RequiresReason(to) && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(ClientText.T("请填写变更原因；部署和验收需注明环境、版本及结果。"));
    }
}
