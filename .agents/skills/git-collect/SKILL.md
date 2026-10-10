---
name: git-collect
description: "当用户要把独立 worktree 上的开发分支合回主 worktree，或调用 git-collect 时使用。调用形式：git-collect worktreename。必须指定非主 worktree 的名字，且该 worktree 当前必须占用开发分支。用 cherry-pick 把开发分支相对其默认分支（与文件夹同名）的超前提交合进主 worktree，完成后把该 worktree 切回默认分支。未给出 worktreename 时不要执行。不要用于只读 git 查询、review、在当前目录直接开发，或往独立 worktree 派发新任务。"
---

# Git Collect

把指定独立 worktree 上的开发提交 cherry-pick 回主 worktree，再把该 worktree 切回空闲状态。

不要在当前目录这份检出上猜目标。合入目标永远是主 worktree；被收集的永远是用户点名的那条独立 worktree。

## 调用

`$git-collect` 和 `git-collect` 等价。`worktreename` 只从下面这个范围解析，两处都算，按出现顺序拼在一起：

1. `git-collect` 同一行后面直接跟的内容
2. 换行后的首行

三种写法等价：

```text
git-collect worktreename
```

```text
git-collect
worktreename
```

```text
git-collect
worktreename
其余说明
```

这个范围之外的内容只是说明，不要当成第二个 worktree，也不要当成要新写的提交说明。

字段：

| 字段 | 含义 | 规则 |
| --- | --- | --- |
| `worktreename` | 被收集的独立 worktree | **必填**。等于某条 worktree 的文件夹名或完整路径。 |
| 主 worktree | `git worktree list` 的第一条 | 合入目标。不要把它的文件夹名当成 `worktreename`。 |
| 默认分支 | 与独立 worktree 文件夹同名的分支 | 例如路径 `D:/proj/rpg-wt/rpg01` 的默认分支是 `rpg01`。 |
| 开发分支 | 该 worktree 当前占用的分支 | 分支名不等于文件夹名，也不是 detached。常见形态是 `title/committype/message` 或 `committype/message`。 |

未解析出 `worktreename`：立刻停止，不要执行，不要自己挑 worktree，不要问要不要用当前目录。

## Worktree 事实

独立开发约定如下，本流程直接使用，不要再去加载其它 git 技能。

- 主 worktree 是 `git worktree list --porcelain` 的第一条。仓库目录名取主 worktree 路径的最后一段，不要用当前目录名。
- 比较路径时去掉末尾斜杠；Windows 上忽略大小写。
- 空闲 worktree：当前分支名等于文件夹名。
- 占用中：当前分支名不等于文件夹名，或处于 detached HEAD。
- 独立 worktree 通常在主 worktree 的同级目录 `<仓库目录名>-wt/<仓库目录名><两位序号>`，例如主目录 `D:/proj/rpg` 对应 `D:/proj/rpg-wt/rpg01`，默认分支 `rpg01`。
- 开发分支一般从该默认分支长出，因此待收集提交通常是 `<默认分支>..<开发分支>`。
- 所有 git 命令都带 `git -C <路径>`，不要依赖当前工作目录碰巧是目标。
- 执行命令前先确认实际 Shell 和 `$PSVersionTable.PSVersion`。Windows PowerShell 5.1 不要用 `&&`。一次调用只做一个明确 git 命令。

## 1. 先确认是 git 仓库

在当前目录运行 `git rev-parse --is-inside-work-tree`。

- 输出 `true`：继续。
- 不是仓库：停下来告诉用户，不要 `git init`。

## 2. 列出 worktree

运行 `git worktree list --porcelain`。

- 第一条是主 worktree，记下它的路径和当前分支。
- 主 worktree 的名字是其路径最后一段。
- 分支字段形如 `refs/heads/<分支名>`。出现 `detached` 而不是 `branch` 时，这条 worktree 没有开发分支。

## 3. 校验 worktreename

用第 2 步的列表匹配 `worktreename`，不要猜。

- 匹配到主 worktree 的文件夹名或路径：停止。不要把主 worktree 当收集源。
- 列表里找不到这个名字或路径：停止。不要自己新建 worktree，也不要改用别的 worktree。
- 匹配到的独立 worktree 当前是空闲的（分支名等于文件夹名）：停止。必须占用开发分支才能收集。
- 匹配到的独立 worktree 处于 detached HEAD：停止。只收集真实的开发分支。
- 通过后告诉用户：独立 worktree 路径、开发分支、默认分支、主 worktree 路径及其当前分支。

## 4. 确认提交范围

在收集前分别检查两侧工作区：

```powershell
git -C <独立worktree路径> status --porcelain
git -C <主worktree路径> status --porcelain
```

判定工作区是否干净时，以 `git status --porcelain` 为准：

- 空输出：干净。
- 只有 `??` 未跟踪项，且路径以 `~` 结尾（编辑器备份，如 `Foo.kt~`）：视为干净。不要停下来，不要 stash，不要删除这些文件，也不要把它们加入暂存区或提交。
- 其它已跟踪变更、冲突、或其它未跟踪文件：视为不干净。停止。不要 stash，不要在脏工作区 cherry-pick 或切分支。

若目标仓库没有忽略 `*~`，`status --porcelain` 可能列出这些备份；按上面规则视为干净。
- 主 worktree 处于 detached HEAD：停止。
- 确认独立 worktree 的默认分支存在：`git show-ref --verify refs/heads/<文件夹名>`。不存在就停止。

待收集提交：

```powershell
git -C <主worktree路径> rev-list --reverse --no-merges <默认分支>..<开发分支>
```

- 结果为空：开发分支没有相对默认分支的超前提交。停止，不要切回默认分支。
- 范围内有 merge commit：停止，不要 `cherry-pick -m`。
- 用 `git cherry <主worktree当前HEAD> <开发分支>` 去掉主 worktree 里已经有等价补丁的提交（`-` 行）。
- 去掉后一个都不剩：说明这些提交已经在主 worktree 上。告诉用户，然后跳到第 6 步切回默认分支，不要空 cherry-pick。
- 其余提交按时间从旧到新，作为即将 cherry-pick 的清单。执行前在回复里写明 SHA 和标题，然后直接 cherry-pick，不要等确认。

## 5. 只在主 worktree 上 cherry-pick

合入目标是主 worktree **当前分支**，不要把开发分支 checkout 到主 worktree，也不要 merge 整条开发分支。

```powershell
git -C <主worktree路径> cherry-pick <sha1> <sha2> ...
```

- 按第 4 步清单一次给出从旧到新的 SHA。
- 保留原提交说明，不要改写，不要加 `-x`，不要 `--amend`。
- 冲突：对主 worktree 执行 `git cherry-pick --abort`。它只取消当前这次冲突中的提交，已经成功落到主 worktree 的提交保留。停下来报告已成功和失败的 SHA。独立 worktree 仍留在开发分支，不要切回默认分支。
- 成功后检查主 worktree 工作区干净，并核对这些提交已经出现在主 worktree 当前分支。
- 不要 push。
- 不要删除开发分支。

## 6. 完成后把独立 worktree 切回默认分支

只有 cherry-pick 成功，或第 4 步确认提交已在主 worktree 上之后，才切换：

```powershell
git -C <独立worktree路径> switch <文件夹名>
```

- 切回的是与文件夹同名的默认分支，这条 worktree 重新视为空闲。
- 切换失败（例如未跟踪文件挡住）：停下来报告，不要 `--force`，不要 `reset --hard`。
- 切换成功后确认当前分支等于文件夹名。
- 开发分支作为普通本地分支保留，不要删。

## 禁止

- 未给出 `worktreename` 时不要执行，也不要改用当前目录或任意空闲 worktree。
- 不要收集主 worktree。
- 不要收集空闲 worktree 或 detached HEAD。
- 找不到指定 worktree 时不要新建，也不要改用其它 worktree。
- 不要 merge、rebase、checkout 开发分支到主 worktree。
- 不要在脏工作区上 cherry-pick 或切分支。
- cherry-pick 失败时不要切回默认分支。
- 不要 push。
- 不要删除开发分支。
- 不要 `git init`，不要 `git worktree add`。
- 不要把本流程当成在独立 worktree 上继续开发或本地提交的入口。

## 文件改动边界

### 通常需要修改

- 主 worktree 当前分支：新增由 cherry-pick 产生的本地提交
- 指定独立 worktree 的当前分支：从开发分支切回与文件夹同名的默认分支

### 禁止修改或只读参考

- 未指定的其它 worktree 检出
- 独立 worktree 上的开发分支引用：保留，不删除
- 遵循目标仓库的 `AGENTS.md`；在本项目中不提交 `.godot/` 缓存和 `build/` 导出文件
