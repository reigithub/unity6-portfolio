using System.Diagnostics;
using System.Runtime.InteropServices;
using Game.Tools.CodeGen;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Game.Tools.Proto;

/// <summary>
/// Reads .proto files via protoc --descriptor_set_out and parses the resulting FileDescriptorSet
/// including custom masterdata_options extensions.
/// </summary>
public class ProtoSchemaReader
{
    // Custom option field numbers matching masterdata_options.proto
    private const int FieldExtFieldTarget = 50001;
    private const int FieldExtIndexType = 50002;
    private const int FieldExtPrimaryKeyOrder = 50003;
    private const int FieldExtSecondaryIndex = 50004;
    private const int FieldExtKeyOrder = 50005;
    private const int FieldExtNonUnique = 50006;

    private const int MsgExtTableTarget = 50001;
    private const int MsgExtTableName = 50002;
    private const int MsgExtSecondaryKeys = 50003;

    private const int IndexTypePrimary = 1;
    private const int IndexTypeSecondary = 2;

    /// <summary>
    /// Invoke protoc to compile .proto files into a FileDescriptorSet, then parse it.
    /// </summary>
    public List<TableDefinition> ReadAll(string protoDir)
    {
        var descriptorBytes = RunProtoc(protoDir);
        var descriptorSet = FileDescriptorSet.Parser.ParseFrom(descriptorBytes);
        return ParseDescriptorSet(descriptorSet, protoDir);
    }

    private static byte[] RunProtoc(string protoDir)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"masterdata_{Guid.NewGuid():N}.bin");
        try
        {
            var absoluteProtoDir = Path.GetFullPath(protoDir);
            var protoFiles = Directory.GetFiles(absoluteProtoDir, "*.proto", SearchOption.AllDirectories);
            if (protoFiles.Length == 0)
            {
                throw new InvalidOperationException($"No .proto files found in {absoluteProtoDir}");
            }

            var (protocPath, wktIncludePath) = FindProtoc();
            var relativeFiles = protoFiles
                .Select(f => Path.GetRelativePath(absoluteProtoDir, f).Replace('\\', '/'))
                .ToArray();

            var psi = new ProcessStartInfo(protocPath)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add($"--descriptor_set_out={tempFile}");
            psi.ArgumentList.Add("--include_imports");
            psi.ArgumentList.Add($"-I{absoluteProtoDir}");
            if (wktIncludePath != null)
            {
                psi.ArgumentList.Add($"-I{wktIncludePath}");
            }

            foreach (var file in relativeFiles)
            {
                psi.ArgumentList.Add(file);
            }

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start protoc");
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"protoc failed (exit code {process.ExitCode}): {stderr}");
            }

            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static (string ProtocPath, string? WktIncludePath) FindProtoc()
    {
        // Try Google.Protobuf.Tools NuGet package first
        var nugetResult = FindProtocFromNuGet();
        if (nugetResult != null)
        {
            return nugetResult.Value;
        }

        // Fall back to PATH
        var protocOnPath = "protoc";
        try
        {
            var psi = new ProcessStartInfo(protocOnPath, "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0)
            {
                return (protocOnPath, null);
            }
        }
        catch
        {
            // Not found on PATH
        }

        throw new InvalidOperationException(
            "protoc not found. Install Google.Protobuf.Tools NuGet package or add protoc to PATH.");
    }

    private static (string ProtocPath, string? WktIncludePath)? FindProtocFromNuGet()
    {
        // Search common NuGet package paths for Google.Protobuf.Tools
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetDir = Path.Combine(userProfile, ".nuget", "packages", "google.protobuf.tools");

        if (!Directory.Exists(nugetDir))
        {
            return null;
        }

        // Get the latest version directory
        var versionDirs = Directory.GetDirectories(nugetDir).OrderByDescending(d => d).ToArray();
        foreach (var versionDir in versionDirs)
        {
            var platform = GetProtocPlatform();
            if (platform == null)
            {
                continue;
            }

            var protocPath = Path.Combine(versionDir, "tools", platform, "protoc" + GetProtocExtension());
            if (File.Exists(protocPath))
            {
                // WKT includes (google/protobuf/*.proto) are in the tools/ directory
                var wktPath = Path.Combine(versionDir, "tools");
                return (protocPath, wktPath);
            }
        }

        return null;
    }

    private static string? GetProtocPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture == Architecture.X64 ? "windows_x64" : "windows_x86";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux_aarch_64" : "linux_x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "macosx_arm64" : "macosx_x64";
        }

        return null;
    }

    private static string GetProtocExtension()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
    }

    private static List<TableDefinition> ParseDescriptorSet(FileDescriptorSet descriptorSet, string protoDir)
    {
        var tables = new List<TableDefinition>();

        foreach (var fileProto in descriptorSet.File)
        {
            // Skip google/protobuf built-in files and the options file itself
            if (fileProto.Name.StartsWith("google/", StringComparison.Ordinal) ||
                fileProto.Name.Contains("masterdata_options", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var msgProto in fileProto.MessageType)
            {
                var table = ParseMessage(msgProto, fileProto.Name);
                if (table != null)
                {
                    tables.Add(table);
                }
            }
        }

        return tables;
    }

    private static TableDefinition? ParseMessage(DescriptorProto msg, string protoFile)
    {
        var options = msg.Options;
        if (options == null)
        {
            return null;
        }

        // Extract message-level custom options by serializing Options and parsing raw bytes
        int tableTarget = 0;
        string? tableName = null;
        var secondaryKeys = new List<SecondaryKeyDefinition>();

        var optionsBytes = SerializeMessage(options);
        if (optionsBytes.Length > 0)
        {
            tableTarget = GetRawFieldVarint(optionsBytes, MsgExtTableTarget);
            tableName = GetRawFieldString(optionsBytes, MsgExtTableName);
            secondaryKeys = GetSecondaryKeyDefs(optionsBytes);
        }

        // Use message name as fallback for table_name
        tableName ??= msg.Name;

        var fields = new List<FieldDefinition>();
        foreach (var fieldProto in msg.Field)
        {
            fields.Add(ParseField(fieldProto));
        }

        return new TableDefinition
        {
            TableName = tableName,
            ProtoFile = protoFile,
            DeployTarget = tableTarget,
            Fields = fields,
            SecondaryKeys = secondaryKeys,
        };
    }

    private static FieldDefinition ParseField(FieldDescriptorProto field)
    {
        bool isOptional = field.Proto3Optional;
        int fieldTarget = 0;
        int indexType = 0;
        int primaryKeyOrder = 0;
        int secondaryIndex = 0;
        int keyOrder = 0;
        bool nonUnique = false;

        var options = field.Options;
        if (options != null)
        {
            var optBytes = SerializeMessage(options);
            if (optBytes.Length > 0)
            {
                fieldTarget = GetRawFieldVarint(optBytes, FieldExtFieldTarget);
                indexType = GetRawFieldVarint(optBytes, FieldExtIndexType);
                primaryKeyOrder = GetRawFieldVarint(optBytes, FieldExtPrimaryKeyOrder);
                secondaryIndex = GetRawFieldVarint(optBytes, FieldExtSecondaryIndex);
                keyOrder = GetRawFieldVarint(optBytes, FieldExtKeyOrder);
                nonUnique = GetRawFieldVarint(optBytes, FieldExtNonUnique) != 0;
            }
        }

        var secondaryKeys = new List<SecondaryKeyInfo>();
        if (indexType == IndexTypeSecondary)
        {
            secondaryKeys.Add(new SecondaryKeyInfo
            {
                Index = secondaryIndex,
                KeyOrder = keyOrder,
                NonUnique = nonUnique,
            });
        }

        string csharpType = MapProtoTypeToCSharp(field.Type, isOptional);
        string protoTypeName = field.Type.ToString().Replace("TYPE_", string.Empty).ToLowerInvariant();
        if (protoTypeName == "int32")
        {
            protoTypeName = "int32";
        }

        return new FieldDefinition
        {
            ProtoName = field.Name,
            CSharpName = NameConverter.ToPascalCase(field.Name),
            CSharpType = csharpType,
            ProtoType = protoTypeName,
            IsOptional = isOptional,
            DeployTarget = fieldTarget,
            FieldNumber = field.Number,
            IsPrimaryKey = indexType == IndexTypePrimary,
            PrimaryKeyOrder = primaryKeyOrder,
            SecondaryKeys = secondaryKeys,
        };
    }

    private static string MapProtoTypeToCSharp(FieldDescriptorProto.Types.Type protoType, bool isOptional)
    {
        string baseType = protoType switch
        {
            FieldDescriptorProto.Types.Type.Int32 or
            FieldDescriptorProto.Types.Type.Sint32 or
            FieldDescriptorProto.Types.Type.Sfixed32 => "int",

            FieldDescriptorProto.Types.Type.Int64 or
            FieldDescriptorProto.Types.Type.Sint64 or
            FieldDescriptorProto.Types.Type.Sfixed64 => "long",

            FieldDescriptorProto.Types.Type.Uint32 or
            FieldDescriptorProto.Types.Type.Fixed32 => "uint",

            FieldDescriptorProto.Types.Type.Uint64 or
            FieldDescriptorProto.Types.Type.Fixed64 => "ulong",

            FieldDescriptorProto.Types.Type.Float => "float",
            FieldDescriptorProto.Types.Type.Double => "double",
            FieldDescriptorProto.Types.Type.Bool => "bool",
            FieldDescriptorProto.Types.Type.String => "string",
            FieldDescriptorProto.Types.Type.Bytes => "byte[]",
            _ => "object",
        };

        // nullable for optional value types (string is already nullable by nature in C#)
        if (isOptional && baseType != "string" && baseType != "byte[]" && baseType != "object")
        {
            return baseType + "?";
        }

        return baseType;
    }

    #region Raw protobuf field extraction helpers

    /// <summary>
    /// Serialize an IMessage to raw bytes for custom option extraction.
    /// </summary>
    private static byte[] SerializeMessage(IMessage message)
    {
        var ms = new MemoryStream();
        var cos = new CodedOutputStream(ms);
        message.WriteTo(cos);
        cos.Flush();
        return ms.ToArray();
    }

    private static void SkipField(CodedInputStream cis, int wireType)
    {
        switch (wireType)
        {
            case 0: cis.ReadUInt64(); break;
            case 1: cis.ReadFixed64(); break;
            case 2: cis.ReadBytes(); break;
            case 5: cis.ReadFixed32(); break;
            default: cis.SkipLastField(); break;
        }
    }

    private static int GetRawFieldVarint(byte[] rawBytes, int fieldNumber)
    {
        var cis = new CodedInputStream(rawBytes);
        while (!cis.IsAtEnd)
        {
            uint tag = cis.ReadTag();
            int fn = (int)(tag >> 3);
            int wt = (int)(tag & 0x7);

            if (fn == fieldNumber && wt == 0)
            {
                return (int)cis.ReadUInt64();
            }

            SkipField(cis, wt);
        }

        return 0;
    }

    private static string? GetRawFieldString(byte[] rawBytes, int fieldNumber)
    {
        var cis = new CodedInputStream(rawBytes);
        while (!cis.IsAtEnd)
        {
            uint tag = cis.ReadTag();
            int fn = (int)(tag >> 3);
            int wt = (int)(tag & 0x7);

            if (fn == fieldNumber && wt == 2)
            {
                return cis.ReadBytes().ToStringUtf8();
            }

            SkipField(cis, wt);
        }

        return null;
    }

    private static List<ByteString> GetRawFieldLengthDelimited(byte[] rawBytes, int fieldNumber)
    {
        var result = new List<ByteString>();
        var cis = new CodedInputStream(rawBytes);
        while (!cis.IsAtEnd)
        {
            uint tag = cis.ReadTag();
            int fn = (int)(tag >> 3);
            int wt = (int)(tag & 0x7);

            if (fn == fieldNumber && wt == 2)
            {
                result.Add(cis.ReadBytes());
                continue;
            }

            SkipField(cis, wt);
        }

        return result;
    }

    private static List<SecondaryKeyDefinition> GetSecondaryKeyDefs(byte[] optionsBytes)
    {
        var result = new List<SecondaryKeyDefinition>();
        var bytesEntries = GetRawFieldLengthDelimited(optionsBytes, MsgExtSecondaryKeys);

        foreach (var bytes in bytesEntries)
        {
            var skDef = ParseSecondaryKeyDef(bytes);
            if (skDef != null)
            {
                result.Add(skDef);
            }
        }

        return result;
    }

    private static SecondaryKeyDefinition? ParseSecondaryKeyDef(ByteString data)
    {
        string? fieldName = null;
        int secondaryIndex = 0;
        int keyOrder = 0;
        bool nonUnique = false;

        var input = new CodedInputStream(data.ToByteArray());
        while (!input.IsAtEnd)
        {
            uint tag = input.ReadTag();
            int fieldNumber = (int)(tag >> 3);

            switch (fieldNumber)
            {
                case 1:
                    fieldName = input.ReadString();
                    break;
                case 2:
                    secondaryIndex = input.ReadInt32();
                    break;
                case 3:
                    keyOrder = input.ReadInt32();
                    break;
                case 4:
                    nonUnique = input.ReadBool();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        if (fieldName == null)
        {
            return null;
        }

        return new SecondaryKeyDefinition
        {
            FieldName = fieldName,
            SecondaryIndex = secondaryIndex,
            KeyOrder = keyOrder,
            NonUnique = nonUnique,
        };
    }

    #endregion
}
