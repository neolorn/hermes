using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Hermes.Repository.Tests;

/// <summary>
/// Reads the public types of a built assembly from its metadata, without loading it.
/// </summary>
internal static class AssemblySurface
{
    /// <summary>The built assemblies of the packages, copied beside the test run by its project references.</summary>
    internal static IReadOnlyList<string> ShippedAssemblies()
    {
        return [.. Directory.EnumerateDirectories(RepositoryRoot.Combine("src"))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Select(name => Path.Combine(AppContext.BaseDirectory, name + ".dll"))];
    }

    internal static IReadOnlyList<PublicType> PublicTypes(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        SignatureNames names = new(metadata);
        List<PublicType> types = [];

        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (!IsVisible(metadata, type))
            {
                continue;
            }

            List<PublicMethod> methods = [];
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                MethodAttributes access = method.Attributes & MethodAttributes.MemberAccessMask;
                if (access is not (MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem))
                {
                    continue;
                }

                MethodSignature<string> signature = method.DecodeSignature(names, genericContext: null);
                methods.Add(new PublicMethod(metadata.GetString(method.Name), signature.ReturnType, signature.ParameterTypes));
            }

            TypeAttributes attributes = type.Attributes;
            types.Add(new PublicType(
                SignatureNames.Name(metadata, handle),
                Kind(metadata, type),
                (attributes & TypeAttributes.Sealed) != 0,
                (attributes & TypeAttributes.Abstract) != 0,
                methods));
        }

        return types;
    }

    /// <summary>The members of other types the assembly calls or reads, each as its type's full name and its own name.</summary>
    internal static IReadOnlyList<(string Type, string Member)> MemberReferences(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        SignatureNames names = new(metadata);
        List<(string Type, string Member)> references = [];

        foreach (MemberReferenceHandle handle in metadata.MemberReferences)
        {
            MemberReference reference = metadata.GetMemberReference(handle);
            string? type = reference.Parent.Kind switch
            {
                HandleKind.TypeReference or HandleKind.TypeDefinition => SignatureNames.Name(metadata, (EntityHandle)reference.Parent),
                HandleKind.TypeSpecification => metadata.GetTypeSpecification((TypeSpecificationHandle)reference.Parent).DecodeSignature(names, genericContext: null),
                _ => null,
            };
            if (type is not null)
            {
                references.Add((type, metadata.GetString(reference.Name)));
            }
        }

        return references;
    }

    /// <summary>The names of the assemblies the assembly references.</summary>
    internal static IReadOnlyList<string> AssemblyReferences(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        return [.. metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))];
    }

    private static bool IsVisible(MetadataReader metadata, TypeDefinition type)
    {
        TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
        return visibility switch
        {
            TypeAttributes.Public => true,
            TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem =>
                IsVisible(metadata, metadata.GetTypeDefinition(type.GetDeclaringType())),
            _ => false,
        };
    }

    private static PublicTypeKind Kind(MetadataReader metadata, TypeDefinition type)
    {
        if ((type.Attributes & TypeAttributes.Interface) != 0)
        {
            return PublicTypeKind.Interface;
        }

        string? baseType = type.BaseType.IsNil ? null : SignatureNames.Name(metadata, type.BaseType);
        return baseType switch
        {
            "System.Enum" => PublicTypeKind.Enumeration,
            "System.ValueType" => PublicTypeKind.Struct,
            "System.MulticastDelegate" => PublicTypeKind.Delegate,
            _ => PublicTypeKind.Class,
        };
    }

    /// <summary>Decodes signatures into full type names.</summary>
    private sealed class SignatureNames(MetadataReader metadata) : ISignatureTypeProvider<string, object?>
    {
        internal static string Name(MetadataReader metadata, EntityHandle handle)
        {
            return handle.Kind switch
            {
                HandleKind.TypeDefinition => Name(metadata, (TypeDefinitionHandle)handle),
                HandleKind.TypeReference => Name(metadata, (TypeReferenceHandle)handle),
                _ => handle.Kind.ToString(),
            };
        }

        internal static string Name(MetadataReader metadata, TypeDefinitionHandle handle)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Name);
            TypeDefinitionHandle declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return Name(metadata, declaring) + "." + name;
            }

            string space = metadata.GetString(type.Namespace);
            return space.Length == 0 ? name : space + "." + name;
        }

        private static string Name(MetadataReader metadata, TypeReferenceHandle handle)
        {
            TypeReference type = metadata.GetTypeReference(handle);
            string name = metadata.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return Name(metadata, (TypeReferenceHandle)type.ResolutionScope) + "." + name;
            }

            string space = metadata.GetString(type.Namespace);
            return space.Length == 0 ? name : space + "." + name;
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType + "[" + new string(',', shape.Rank - 1) + "]";
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "delegate*";
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType + "<" + string.Join(",", typeArguments) + ">";
        }

        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return "!!" + index;
        }

        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return "!" + index;
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return "System." + typeCode;
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return Name(metadata, handle);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return Name(metadata, handle);
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }
    }
}