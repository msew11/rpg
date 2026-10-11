# 定义具体数据仓库必须声明的类型和承载形态。
@abstract
class_name DataRepo
extends RefCounted

# 标识具体仓库存储一个实例还是一组实例。
enum StorageKind {
	# 仓库只保存一个数据实例。
	SINGLE,
	# 仓库保存多个同类型数据实例。
	MULTIPLE
}


# 声明具体仓库承载的数据脚本类型。
@abstract func get_data_type() -> Script


# 声明具体仓库的实例数量形态。
@abstract func get_storage_kind() -> StorageKind
