using System;

namespace Rpg.Config.Cache;

/// <summary>将多例配置的单个属性声明为主键。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CfgKeyAttribute : Attribute { }
