using Planova.SharedKernel.Security;

namespace Planova.Identity.Contracts;

public static class PermissionCodes
{
    public static readonly PermissionCode FoundationView = PermissionCode.Create("foundation.view");
    public static readonly PermissionCode UserManage = PermissionCode.Create("user.manage");
    public static readonly PermissionCode RoleManage = PermissionCode.Create("role.manage");
    public static readonly PermissionCode SettingsManage = PermissionCode.Create("settings.manage");
    public static readonly PermissionCode AuditView = PermissionCode.Create("audit.view");

    public static readonly IReadOnlyCollection<string> All =
    [
        "foundation.view",
        "project.view", "project.create", "project.edit", "project.archive", "project.manage_members", "project.baseline", "project.export",
        "task.view", "task.create", "task.edit", "task.assign", "task.delete", "milestone.manage",
        "raid.view", "raid.create", "raid.edit", "raid.assign", "raid.escalate", "raid.close",
        "change.view", "change.create", "change.assess", "change.submit", "change.cancel",
        "approval.view", "approval.decide", "approval.delegate", "workflow.manage",
        "governance.view", "governance.review", "exception.manage", "policy.manage", "attestation.manage",
        "resource.view", "resource.edit", "allocation.view", "allocation.manage", "capacity.manage",
        "budget.view", "budget.edit", "budget.baseline", "actual.import", "forecast.edit", "financial.approve",
        "time.enter", "time.view_team", "time.approve", "leave.request", "leave.view_team", "leave.approve",
        "portfolio.view", "portfolio.manage", "portfolio.score", "portfolio.snapshot",
        "release.view", "release.manage", "release.gate_decide",
        "report.run", "report.design", "audit.view", "audit.export", "user.manage", "role.manage", "settings.manage", "integration.manage"
    ];

    public static PermissionCode From(string value) => PermissionCode.Create(value);
}
