using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// The reflection <c>07</c> MSG-ENG-004 rules out, read from what a built assembly calls: a
/// lookup or call of members by name through <c>System.Reflection</c>, <c>Activator</c> or
/// <c>Type.GetType</c>, and <c>System.Reflection.Metadata</c>, which only the tests read.
/// Reading an assembly's embedded resources is allowed.
/// </summary>
internal static class ReflectionRules
{
    private static readonly string[] LookupTypes = ["System.Type", "System.Reflection.TypeInfo", "System.Reflection.IReflect", "System.Reflection.Assembly", "System.Reflection.Module"];

    private static readonly string[] Lookups =
    [
        "GetMethod", "GetMethods", "GetProperty", "GetProperties", "GetField", "GetFields", "GetMember", "GetMembers",
        "GetEvent", "GetEvents", "GetConstructor", "GetConstructors", "GetNestedType", "GetNestedTypes", "GetInterface",
        "GetDefaultMembers", "FindMembers", "InvokeMember", "GetDeclaredMethod", "GetDeclaredMethods", "GetDeclaredProperty",
        "GetDeclaredField", "GetDeclaredEvent", "GetDeclaredNestedType", "GetType", "CreateInstance",
    ];

    private static readonly string[] WholeTypes = ["System.Activator", "System.Reflection.RuntimeReflectionExtensions", "System.Reflection.TypeExtensions"];

    internal static IEnumerable<string> Violations(string assembly, IEnumerable<(string Type, string Member)> members, IEnumerable<string> assemblies)
    {
        foreach ((string type, string member) in members.Distinct())
        {
            bool lookup = LookupTypes.Contains(type, StringComparer.Ordinal) && Lookups.Contains(member, StringComparer.Ordinal);
            if (lookup || WholeTypes.Contains(type, StringComparer.Ordinal))
            {
                yield return $"{assembly} looks members up by name: {type}.{member}.";
            }
        }

        if (assemblies.Contains("System.Reflection.Metadata", StringComparer.Ordinal))
        {
            yield return $"{assembly} references System.Reflection.Metadata, which only the tests read.";
        }
    }
}