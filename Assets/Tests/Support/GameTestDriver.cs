using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Nightmare.Tests
{
    // Transitional driver for the original Assembly-CSharp components. Assertions test behavior,
    // not private implementation. Replace this boundary as typed modules become available.
    /// <summary>Bridges tests to Assembly-CSharp components through controlled reflection.</summary>
    public static class GameTestDriver
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null);
        public static object Get(object target, string name)
        {
            var type = target as Type ?? target.GetType();
            return type.GetProperty(name, Members)?.GetValue(target is Type ? null : target)
                ?? type.GetField(name, Members)?.GetValue(target is Type ? null : target);
        }
        public static void Set(object target, string name, object value)
        {
            var type = target as Type ?? target.GetType();
            var property = type.GetProperty(name, Members);
            if (property != null) property.SetValue(target is Type ? null : target, value);
            else type.GetField(name, Members).SetValue(target is Type ? null : target, value);
        }
        public static object Call(object target, string name, params object[] arguments)
        {
            var type = target as Type ?? target.GetType();
            return type.GetMethod(name, Members).Invoke(target is Type ? null : target, arguments);
        }
        public static Component Component(GameObject gameObject, string name) => gameObject.GetComponent(Type(name));
    }
}
