using System;
using System.Reflection;
using UnityEngine;

// Keep the lightweight compile harness independent of URP's package-only assemblies.
/// <summary>Bridges URP settings through reflection without introducing package-only dependencies into the compile harness.</summary>
public static class UrpAccess
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public static Type Type(string name) { return System.Type.GetType("UnityEngine.Rendering.Universal." + name + ", Unity.RenderPipelines.Universal.Runtime", true); }
    public static object Get(object target, string name)
    {
        var type = target.GetType();
        var field = type.GetField(name, Flags);
        return field != null ? field.GetValue(target) : type.GetProperty(name, Flags).GetValue(target, null);
    }
    public static void Set(object target, string name, object value)
    {
        var type = target.GetType();
        var field = type.GetField(name, Flags);
        var property = type.GetProperty(name, Flags);
        var expected = field != null ? field.FieldType : property.PropertyType;
        if (expected.IsEnum && value is string) value = Enum.Parse(expected, (string)value);
        if (field != null) field.SetValue(target, value); else property.SetValue(target, value, null);
    }
    public static void Parameter(object effect, string name, object value)
    {
        var parameter = Get(effect, name);
        Set(parameter, "overrideState", true);
        Set(parameter, "value", value);
    }
    public static Component CameraData(Camera camera)
    {
        var type = Type("UniversalAdditionalCameraData");
        return camera.GetComponent(type) ?? camera.gameObject.AddComponent(type);
    }
    public static object Effect(ScriptableObject profile, string name)
    {
        foreach (var effect in (System.Collections.IEnumerable)Get(profile, "components"))
            if (effect.GetType().Name == name) return effect;
        throw new InvalidOperationException("Missing URP effect " + name + " in " + profile.name);
    }
}
