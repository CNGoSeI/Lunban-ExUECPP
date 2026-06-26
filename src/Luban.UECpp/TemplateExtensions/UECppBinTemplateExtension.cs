// Copyright 2025 Code Philosophy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Luban.Defs;
using Luban.Types;
using Luban.UECpp.TypeVisitors;
using Scriban.Runtime;

namespace Luban.UECpp.TemplateExtensions;

public class UECppBinTemplateExtension : ScriptObject
{
    /// <summary>
    /// Generate UE-style type name: F/E prefix + name + _ + namespace (if any).
    /// E.g., DefBean(Namespace="XiuShiSys", Name="FaBaoAttr") → "FFaBaoAttr_XiuShiSys"
    /// E.g., DefEnum(Namespace="test", Name="AccessFlag") → "EAccessFlag_test"
    /// </summary>
    public static string MakeUeTypeName(DefTypeBase type)
    {
        string ns = type.Namespace;
        string name = type.Name;
        string ueName;
        if (type is DefEnum)
        {
            ueName = HasUePrefix(name, 'E') ? name : "E" + name;
        }
        else
        {
            ueName = HasUePrefix(name, 'F') ? name : "F" + name;
        }
        if (string.IsNullOrEmpty(ns))
            return ueName;
        return ueName + "_" + ns.Replace(".", "_");
    }

    public static string DeclaringTypeName(TType type)
    {
        return type.Apply(UECppDeclaringTypeNameVisitor.Ins);
    }

    public static string Deserialize(string bufName, string fieldName, TType type)
    {
        return type.Apply(UECppDeserializeVisitor.Ins, bufName, fieldName, 0);
    }

    /// <summary>
    /// Generate UE-style class name with prefix convention:
    /// Enum -> E prefix, Struct/Bean -> F prefix
    /// </summary>
    public static string MakeUeClassName(string fullName)
    {
        int index = fullName.LastIndexOf('.');
        string name = index >= 0 ? fullName.Substring(index + 1) : fullName;
        return MakeUeStructName(fullName);
    }

    /// <summary>
    /// UE F prefix convention: F followed by an uppercase letter (e.g., FShape, FCircle).
    /// A name like "FaBaoAttr" starts with F but has a lowercase second char → needs F prefix → FFaBaoAttr.
    /// </summary>
    private static bool HasUePrefix(string name, char prefix)
    {
        return name.Length >= 2 && name[0] == prefix && char.IsUpper(name[1]);
    }

    public static string MakeUeStructName(string fullName)
    {
        int index = fullName.LastIndexOf('.');
        string name = index >= 0 ? fullName.Substring(index + 1) : fullName;
        return HasUePrefix(name, 'F') ? name : "F" + name;
    }

    public static string MakeUeEnumName(string fullName)
    {
        int index = fullName.LastIndexOf('.');
        string name = index >= 0 ? fullName.Substring(index + 1) : fullName;
        return HasUePrefix(name, 'E') ? name : "E" + name;
    }

    /// <summary>
    /// Generate global-scope UE type name: F/E prefix + name + _ + namespace.
    /// Since UE5.6 UHT does not support USTRUCT/UENUM inside C++ namespace blocks,
    /// the namespace is appended after the name with underscore separator.
    /// E.g., "XiuShiSys.FaBaoAttr" → "FFaBaoAttr_XiuShiSys"
    /// E.g., "test.Circle" → "FCircle_test"
    /// For names without a dot, returns the name with F prefix.
    /// </summary>
    public static string MakeUeFullName(string fullName)
    {
        int index = fullName.LastIndexOf('.');
        if (index < 0)
            return MakeUeStructName(fullName);
        string ns = fullName.Substring(0, index);
        string name = fullName.Substring(index + 1);
        name = HasUePrefix(name, 'F') ? name : "F" + name;
        return name + "_" + ns.Replace(".", "_");
    }

    /// <summary>
    /// Global-scope UE enum name: E prefix + name + _ + namespace.
    /// E.g., "test.AccessFlag" → "EAccessFlag_test"
    /// </summary>
    public static string MakeUeFullEnumName(string fullName)
    {
        int index = fullName.LastIndexOf('.');
        if (index < 0)
            return MakeUeEnumName(fullName);
        string ns = fullName.Substring(0, index);
        string name = fullName.Substring(index + 1);
        name = HasUePrefix(name, 'E') ? name : "E" + name;
        return name + "_" + ns.Replace(".", "_");
    }

    public static string UeNamespaceBegin(string ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return "";
        }
        return string.Join("", ns.Split('.').Select(n => $"namespace {n} {{"));
    }

    public static string UeNamespaceEnd(string ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return "";
        }
        return string.Join("", ns.Split('.').Select(n => $"}}"));
    }

    /// <summary>
    /// Generate the include path for a type's header file.
    /// E.g., item.equipment.FWeaponConfig -> "item/equipment/FWeaponConfig.h"
    /// </summary>
    public static string GenIncludePath(string topModule, string ns, string name)
    {
        string fullNs = string.IsNullOrEmpty(ns) ? topModule :
                        string.IsNullOrEmpty(topModule) ? ns : $"{topModule}.{ns}";
        if (string.IsNullOrEmpty(fullNs))
        {
            return $"{name}.h";
        }
        return $"{fullNs.Replace('.', '/')}/{name}.h";
    }

    /// <summary>
    /// Generate the binary file name for a table's loader callback.
    /// E.g., "XiuShiSys.TbXiuShiCommon" → "xiushisys_tbxiushicommon"
    /// (lowercase, underscore-separated, without top module)
    /// </summary>
    public static string MakeLoaderTableName(string fullName)
    {
        return fullName.Replace(".", "_").ToLowerInvariant();
    }

    /// <summary>
    /// Check whether a DefEnum already has an item with value 0.
    /// UE requires all UENUMs to have a 0 entry for default initialization.
    /// </summary>
    public static bool HasZeroItem(DefEnum enumDef)
    {
        if (enumDef == null) return false;
        foreach (var item in enumDef.Items)
        {
            if (int.TryParse(item.Value, out int val) && val == 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Check if a string field should be treated as FName based on naming convention.
    /// Fields with names ending with "_fname" (case-insensitive) → FName.
    /// </summary>
    public static bool IsNameField(DefField field)
    {
        if (field == null) return false;
        string name = field.Name;
        return name.EndsWith("_fname", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the appropriate UE type name for a field.
    /// String fields ending with "_fname" are mapped to FName.
    /// </summary>
    public static string GetFieldDeclType(TType type, DefField field)
    {
        if (IsNameField(field) && type is TString)
        {
            return "FName";
        }
        return DeclaringTypeName(type);
    }
}
