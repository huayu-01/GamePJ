using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace ActRoguelike.Config;

internal static class RewardEffectValidation
{
    public static IEnumerable<string> NonNegative(Resource resource)
    {
        foreach (var property in resource.GetType().GetProperties())
        {
            if (property.GetCustomAttribute<ExportAttribute>() == null) continue;
            object? value = property.GetValue(resource);
            if (value is float number && (!float.IsFinite(number) || number < 0) || value is int integer && integer < 0) yield return $"{property.Name}：必须为非负有限数值。";
        }
    }
}
