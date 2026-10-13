# Permission catalogue

Permission codes are stable lower-case `resource.action` identifiers. They are contracts and are not renamed casually.

| Area | Codes |
|---|---|
| Phase 0 reference | `foundation.view` |
| Projects (reserved) | `project.view`, `project.create`, `project.edit`, `project.archive`, `project.manage_members`, `project.baseline`, `project.export` |
| Work (reserved) | `task.view`, `task.create`, `task.edit`, `task.assign`, `task.delete`, `milestone.manage` |
| RAID (reserved) | `raid.view`, `raid.create`, `raid.edit`, `raid.assign`, `raid.escalate`, `raid.close` |
| Approval (reserved) | `approval.view`, `approval.decide`, `approval.delegate`, `workflow.manage` |
| Platform (reserved) | `report.run`, `report.design`, `audit.view`, `audit.export`, `user.manage`, `role.manage`, `settings.manage`, `integration.manage` |

Reserved codes document future contracts but do not enable or implement those modules.
