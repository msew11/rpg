using System;

namespace Rpg.Config.Cache;

/// <summary>声明具体配置缓存对应的 JSON 文件名。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CfgFileAttribute : Attribute
{
    /// <summary>配置目录内唯一匹配的文件名。</summary>
    public string FileName { get; }

    /// <summary>记录文件名，合法性由缓存初始化统一检查。</summary>
    public CfgFileAttribute(string fileName) { FileName = fileName; }
}
