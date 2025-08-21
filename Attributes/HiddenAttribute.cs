using System;

namespace Morpheus.Attributes;

/// <summary>
/// Marks a command as hidden. Does not change runtime behavior by itself.
/// The help generator and tooling should skip methods annotated with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HiddenAttribute : Attribute
{
}
