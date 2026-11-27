using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Swordfish.IO;
using WaywardBeyond.Bodies;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Items;
using WaywardBeyond.Skills;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins the public field and property surface of content definition types to <see cref="ContentVersion.CURRENT"/>.</summary>
public class ContentVersionTests
{
    private const uint PINNED_CONTENT_VERSION = 1;
    private const string PINNED_SCHEMA_FINGERPRINT = "0F629213FC48BECC9E859A126ECD40F7CC7888E2949AE35B0A29E44B57D9298E";

    private static readonly Type[] _definitionTypes =
    [
        typeof(ItemDefinitions),
        typeof(ItemDefinition),
        typeof(PlaceableDefinition),
        typeof(ToolDefinition),
        typeof(ModelDefinition),
        typeof(BrickDefinitions),
        typeof(BrickDefinition),
        typeof(BrickTextures),
        typeof(BrickColliderDefinition),
        typeof(PaintDefinitions),
        typeof(PaintDefinition),
        typeof(SkillDefinitions),
        typeof(SkillDefinition),
        typeof(BodyDefinitions),
        typeof(BodyDefinition),
        typeof(MaterialDefinition),
    ];

    /// <summary>Verifies that every content definition type is publicly accessible.</summary>
    [Fact]
    public void AllContentDefinitionTypesArePublic()
    {
        for (var i = 0; i < _definitionTypes.Length; i++)
        {
            Type type = _definitionTypes[i];
            Assert.True(type.IsPublic, $"Content definition type {type.FullName} must be public.");
        }
    }

    /// <summary>Verifies that the schema fingerprint matches the pinned constant for <see cref="ContentVersion.CURRENT"/>.</summary>
    [Fact]
    public void DefinitionSchemaFingerprintMatchesContentVersion()
    {
        Assert.True(
            ContentVersion.CURRENT == PINNED_CONTENT_VERSION,
            $"ContentVersion.CURRENT is {ContentVersion.CURRENT}, but ContentVersionTests pins {PINNED_CONTENT_VERSION}. " +
            "Update PINNED_CONTENT_VERSION and PINNED_SCHEMA_FINGERPRINT in ContentVersionTests."
        );

        string actualFingerprint = ComputeSchemaFingerprint();
        Assert.True(
            string.Equals(PINNED_SCHEMA_FINGERPRINT, actualFingerprint, StringComparison.Ordinal),
            $"Content definition schema fingerprint changed for ContentVersion.CURRENT = {ContentVersion.CURRENT}. " +
            $"Expected {PINNED_SCHEMA_FINGERPRINT}, got {actualFingerprint}. " +
            "When definition schemas change, bump ContentVersion.CURRENT and update PINNED_CONTENT_VERSION and PINNED_SCHEMA_FINGERPRINT in ContentVersionTests."
        );
    }

    private static string ComputeSchemaFingerprint()
    {
        var typeEntries = new List<(string CanonicalName, Type Type)>(_definitionTypes.Length);
        for (var i = 0; i < _definitionTypes.Length; i++)
        {
            Type type = _definitionTypes[i];
            typeEntries.Add((FormatTypeName(type), type));
        }

        typeEntries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.CanonicalName, right.CanonicalName));

        var builder = new StringBuilder();
        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        for (var i = 0; i < typeEntries.Count; i++)
        {
            (string canonicalName, Type type) = typeEntries[i];
            builder.Append(canonicalName);
            builder.Append('\n');

            var members = new List<string>();
            FieldInfo[] fields = type.GetFields(FLAGS);
            for (var f = 0; f < fields.Length; f++)
            {
                FieldInfo field = fields[f];
                members.Add($"field:{field.Name}:{FormatTypeName(field.FieldType)}");
            }

            PropertyInfo[] properties = type.GetProperties(FLAGS);
            for (var p = 0; p < properties.Length; p++)
            {
                PropertyInfo property = properties[p];
                members.Add($"property:{property.Name}:{FormatTypeName(property.PropertyType)}");
            }

            members.Sort(StringComparer.Ordinal);
            for (var m = 0; m < members.Count; m++)
            {
                builder.Append("  ");
                builder.Append(members[m]);
                builder.Append('\n');
            }
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsArray)
        {
            string elementName = FormatTypeName(type.GetElementType()!);
            int rank = type.GetArrayRank();
            return rank == 1 ? $"{elementName}[]" : $"{elementName}[{new string(',', rank - 1)}]";
        }

        if (type.IsGenericType)
        {
            Type genericDefinition = type.GetGenericTypeDefinition();
            string baseName = genericDefinition.FullName ?? genericDefinition.Name;
            int arityIndex = baseName.IndexOf('`');
            if (arityIndex >= 0)
            {
                baseName = baseName[..arityIndex];
            }

            Type[] genericArgs = type.GetGenericArguments();
            var formattedArgs = new string[genericArgs.Length];
            for (var i = 0; i < genericArgs.Length; i++)
            {
                formattedArgs[i] = FormatTypeName(genericArgs[i]);
            }

            return $"{baseName}<{string.Join(",", formattedArgs)}>";
        }

        return type.FullName ?? type.Name;
    }
}
