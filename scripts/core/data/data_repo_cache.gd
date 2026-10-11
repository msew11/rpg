# 定义未来统一仓库管理器的查找入口。
@abstract
class_name DataRepoCache
extends Node

# 按仓库脚本类型取得对应的具体仓库。
@abstract func get_repo(repo_type: Script) -> DataRepo
