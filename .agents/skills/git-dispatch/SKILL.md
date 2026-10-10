---
name: git-dispatch
description: "原名 git-dev。当用户要用独立 git worktree 做功能开发、开开发分支，或按 git-dispatch 流程本地提交时使用。元数据在 git-dispatch 同行后面，或换行后的首行：worktreename、committype/title/message。指定 worktree 就直接在该工作树开发。未指定时只在分支名与文件夹同名的空闲 worktree 上开发，不使用当前目录检出。分支名用首次派发的 committype/title/message；同会话后续派发留在当前分支，不要新开分支。开新分支前先把该 worktree 默认分支快进到主 worktree 当前 HEAD；超前或分叉则终止。prompt 是提示词正文，简化后作为提交正文。不要用于只读 git 查询、review，或用户明确要求留在当前目录修改的任务。"
---

# Git Dispatch

用独立 worktree 做功能开发。默认不要在当前目录那份检出上改代码、开分支或提交。

每次派发先拆成元数据和 prompt。元数据决定 worktree、分支名和提交标题；prompt 是要做的任务，也是提交正文的来源。

## 元数据

`$git-dispatch` 和 `git-dispatch` 等价。除 prompt 以外的元数据，只从下面这个范围解析，两处都算，按出现顺序拼在一起：

1. `git-dispatch` 同一行后面直接跟的内容
2. 换行后的首行

三种写法等价：

```text
git-dispatch worktreename
committype/title/message
```

```text
git-dispatch
worktreename committype/title/message
```

```text
git-dispatch worktreename committype/title/message
```

这个范围之外的全部内容才是 prompt。换行后的首行一旦被收进元数据，不要再把它当成 prompt。

字段：

| 字段 | 含义 | 规则 |
| --- | --- | --- |
| `worktreename` | 目标 worktree | 可选。等于某条 worktree 的文件夹名或完整路径。 |
| `committype` | 约定式提交关键词 | 例如 `feat`、`fix`。用户写了就原样使用。没写时用 `feat`。 |
| `title` | 单个简单单词 | 例如 `draft`。用户写了就原样使用。没写就不要补。 |
| `message` | 不含空格、用 `-` 连接的单词 | 例如 `optimize-json-serialization`。不要把中文原句放进去。用户已经是这种短英语时，只整理空格和大小写。 |
| `branchname` | `title/committype/message` | 只用**首次派发**得到的这三段。没有 `title` 时写成 `feat/optimize-json-serialization`，不要插入空段。 |
| `prompt` | 提示词正文 | 元数据范围之后的全部内容。简化后作为 commit message 正文，用 prompt 原来的语言，不要整段照抄。 |

`title/committype/message` 必须是斜线三段。两段 `committype/message` 表示没有 `title`，不要理解成 `title/committype` 再自己编 `message`。

解析不出 `message` 时，首次派发先问用户，不要创建分支。

## 会话内复用（先看这条）

当前会话一旦已经选中 worktree、用首次派发的 `branchname` 开过开发分支：

- 后续再派发：留在当前 worktree、当前分支继续开发。
- `branchname` 不再变。不要新开分支，不要换 worktree，不要重新做空闲 worktree 选择，也不要 `git worktree add`。
- 这次若带了新的 `title/committype/message`，只用来写这次提交标题，不要拿去开分支。
- 这次若带了同一个 `worktreename`，仍视为继续当前分支。
- `prompt` 仍按这次正文简化成提交正文。

只有下面情况才重新选 worktree 或开新分支：

- 用户明确指定了另一个 worktree；
- 用户明确要求开始另一个功能或新开分支；
- 用户已经把该 worktree 切回与文件夹同名的空闲分支。

## 1. 先确认是 git 仓库

在当前目录运行 `git rev-parse --is-inside-work-tree`。

- 输出 `true`：继续。
- 不是仓库：停下来问用户要不要 `git init`。用户明确同意前不要初始化，也不要创建 worktree 或分支。

## 2. 列出其它 worktree

运行 `git worktree list --porcelain`。

主 worktree 是列表第一条。仓库目录名取主 worktree 路径的最后一段，不要用当前目录名。当前目录即使已经在链接 worktree 里，仓库名仍然以主 worktree 为准。

分支字段形如 `refs/heads/<分支名>`，拿 `<分支名>` 与文件夹名比较。出现 `detached` 而不是 `branch` 时，这条 worktree 不是空闲的。

比较路径时去掉末尾斜杠；Windows 上忽略大小写。

## 3. 选定 worktree

用第 2 步的列表匹配 `worktreename`，不要猜。

当前会话已经有选定的 worktree 时，按文首「会话内复用」执行，不要走进自动挑选。

### 用户指定了 worktree

直接使用匹配到的那条路径。

- 不要要求它当前分支名等于文件夹名。
- 不要因为它是当前目录那条就拒绝。
- 列表里找不到这个名字或路径：停下来问用户，不要自己新建，也不要改用别的 worktree。
- 告诉用户用了哪条路径和当前分支。

### 用户没指定 worktree

候选里排除路径与当前工作目录相同的那一条。只在空闲 worktree 上开发。

空闲的唯一标准：该 worktree 当前分支名与它的文件夹名完全相同。

- 分支名不同、detached HEAD，或路径就是当前目录：都算占用，不要用。
- 有多条空闲：用序号最小的那条，并告诉用户选了哪条路径和分支。
- 一条都没有：不要自己创建。先告诉用户准备这样建，并等用户同意：
  - 父目录是主 worktree 的同级目录 `<仓库目录名>-wt`。这个目录已经存在就继续用，不要另起一个。
  - 文件夹和空闲分支都叫 `<仓库目录名><两位序号>`。例如主目录 `D:/proj/rpg` 的下一条若是 `D:/proj/rpg-wt/rpg01`，空闲分支就叫 `rpg01`。
  - 序号从 `01` 起，避开父目录里已有的同名文件夹和已有同名分支，取最小空号。两位序号用尽就停下来问用户。
  - 起点提交由用户指定。用户没指定时，用主 worktree 当前 HEAD，并在提问里写明这个提交。

用户同意后才执行：

```powershell
git worktree add -b <文件夹名> <父目录>/<文件夹名> <起点>
```

新建分支名与文件夹名相同，这条 worktree 视为空闲。

## 4. 只在需要时开开发分支

已经按「会话内复用」留在当前分支时，跳过本节，不要 `switch -c`。

否则在选中的 worktree 上先看工作区，再决定是否开分支。新开发分支必须从「已与主 worktree 当前 HEAD 同步」的默认分支长出。

```powershell
git -C <worktree路径> status --porcelain
git -C <主worktree路径> rev-parse --abbrev-ref HEAD
git -C <主worktree路径> rev-parse HEAD
git -C <worktree路径> rev-parse HEAD
git -C <worktree路径> merge-base --is-ancestor HEAD <主worktree HEAD>
git -C <worktree路径> merge --ff-only <主worktree HEAD>
git -C <worktree路径> switch -c <branchname>
```

判定工作区是否干净时，以 `git status --porcelain` 为准：

- 空输出：干净。
- 只有 `??` 未跟踪项，且路径以 `~` 结尾（编辑器备份，如 `Foo.kt~`）：视为干净。不要停下来，不要删除这些文件，也不要把它们加入暂存区或提交。
- 其它已跟踪变更、冲突、或其它未跟踪文件：视为不干净。停下来告诉用户，不要在脏工作区上开分支，也不要同步默认分支。

若目标仓库没有忽略 `*~`，`status --porcelain` 可能列出这些备份；按上面规则视为干净。
- 同名分支已经存在：停下来问用户。不要删除、重建，也不要切到别的 worktree 上的同名分支。
- 用户指定的 worktree 当前已经在一条开发分支上（分支名不等于文件夹名，也不是 detached）：不要再开新分支，留在当前分支开发。这次仍按元数据写提交标题，按 prompt 写提交正文。
- 用户明确要求开始另一个功能或新开分支，但当前还不在默认分支：停下来让用户先切回默认分支，或先走 git-collect。不要代为切回，也不要从开发分支直接 `switch -c`。
- 空闲 worktree（当前分支名等于文件夹名）：先完成本节的默认分支同步，再用首次派发的 `branchname` 开分支。

### 开新分支前同步默认分支

默认分支是与该 worktree 文件夹同名的分支。要对齐的是主 worktree（`git worktree list --porcelain` 第一条）**当前所在分支的 HEAD**，不要用 remote，也不要自己猜主干名。

当前就在默认分支、工作区干净时：

- 两个 HEAD 相同：已经同步，继续 `switch -c <branchname>`。
- 默认分支是主 worktree HEAD 的祖先：只允许快进。在该 worktree 上 `git merge --ff-only <主worktree HEAD>`，成功后再开新分支。
- 否则（默认分支超前，或两边已经分叉）：停止。不要 merge、rebase、reset，也不要开新分支。告诉用户主 worktree 路径及其当前分支/SHA、该 worktree 默认分支/SHA。

不要改主 worktree 的分支或工作区。不要 fetch remote 来代替这次同步。新建 worktree 的起点已经是主 worktree HEAD 时，创建后仍按本节检查一次再开开发分支。

之后的文件修改和 git 命令都针对这个 worktree 路径。没指定 worktree 时，不要改当前目录那份检出。不要把分支切回文件夹同名分支。

## 5. 任务完成后只做本地提交

一个任务做完后，在该 worktree 的当前开发分支上提交。提交标题用这次元数据的 `committype`、`title`、`message`，格式固定为约定式提交，不要写成斜线。

有 `title` 时：

```text
feat(draft): optimize-json-serialization

用 @param:JsonInclude 收紧 JSON 序列化。
```

没有 `title` 时：

```text
fix: null-pointer

归纳后的正文
```

- 标题里的类型、title、message 与这次解析出的字段相同。`message` 保留其中的 `-`。
- `branchname` 仍是首次派发的那条，即使这次提交标题不同。
- 空一行后写正文。正文是 prompt 的简化，用 prompt 原来的语言，不要整段照抄，也不要改变任务含义。
- 没有 prompt 就只提交标题，不要补空正文。
- 只加入这次任务相关的文件。
- 没有改动就不要做空提交。
- 不要加 emoji，也不要把类型或 title 改成仓库里其它提交风格。
- 不要 push，除非用户这次明确要求。
- 不要 `commit --amend`，除非用户明确要求。
- 提交后仍留在开发分支，不要切回文件夹同名分支。

## 6. 完成由用户切分支表示

用户自己把该 worktree 切回与文件夹同名的分支，才表示这轮开发完成。不要代做这次切换。

用户还没切回去就再次派发：留在当前开发分支上继续改。不要为此新开分支，也不要换 worktree。

## 禁止

- 不要把 `git-dispatch` 之后的元数据范围当成 prompt，也不要把 prompt 正文解析成元数据。
- 用户没指定 worktree 时，不要在当前目录所在的 worktree 上开发、开分支或提交。
- 用户没指定 worktree 时，不要把分支名不是文件夹名的 worktree 当成空闲。
- 用户指定了 worktree 时，不要改用其它空闲 worktree。
- 同会话已经用首次派发的 `branchname` 开过分支后，不要因为新的 `title/committype/message` 再开分支或换 worktree。
- 用户同意前不要 `git init`，也不要 `git worktree add`。
- 不要自己切回空闲分支。
- 不要在默认分支与主 worktree 当前 HEAD 未快进对齐时开新分支。超前或分叉时不要 merge、rebase、reset 强行对齐。
- 不要从开发分支直接开新分支。
- 不要 push。
- 不要改写或补造用户给出的 `committype` 和 `title`。没给 `title` 时不要加上 `(title)`，分支名里也不要插入空段。
- 不要把说明原句原样用作 `message`、分支名或提交标题。

## 文件改动边界

### 通常需要修改

- 选中 worktree 里本次任务相关的业务源码
- 该 worktree 上的本地提交

### 禁止修改或只读参考

- 当前目录所在 worktree 的检出：用户没指定它时，不在上面开发、开分支或提交
- 分支名与文件夹名不同的 worktree：用户没指定它时视为占用，不在上面改代码
- 遵循目标仓库的 `AGENTS.md`；在本项目中不提交 `.godot/` 缓存和 `build/` 导出文件，完成功能后运行 `tools/check.ps1`，并按 OpenSpec 流程记录与归档 change
