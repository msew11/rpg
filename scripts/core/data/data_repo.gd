# 定义具体数据仓库必须声明的类型和承载形态。
@abstract
class_name DataRepo
extends RefCounted

# 标识具体仓库存储一个实例还是一组实例。
enum StorageKind { SINGLE, MULTIPLE }


# 声明具体仓库承载的数据脚本类型。
@abstract func get_data_type() -> Script


# 声明具体仓库的实例数量形态。
@abstract func get_storage_kind() -> StorageKind
