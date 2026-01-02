using MasterMemory;

// WARN: namespaceの末尾をMasterMemoryにすると自動生成コードと衝突するヨ...
[assembly: MasterMemoryGeneratorOptions(
    Namespace = "Game.Core.MasterData",
    IsReturnNullIfKeyNotFound = false,
    PrefixClassName = ""
)]

namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit
    {
    }
}

// public class _MasterMemoryGeneratorOptions : MonoBehaviour
// {
// }

// table definition marked by MemoryTableAttribute.
// database-table must be serializable by MessagePack-CSsharp
// [MemoryTable("SampleMaster"), MessagePackObject(true)]
// public sealed partial class SampleMaster
// {
//     // index definition by attributes.
//     [PrimaryKey]
//     public int Id { get; set; }
//
//     // secondary index can add multiple(discriminated by index-number).
//     [SecondaryKey(0), NonUnique]
//     [SecondaryKey(1, keyOrder: 1), NonUnique]
//     public int Age { get; set; }
//
//     [SecondaryKey(2), NonUnique]
//     [SecondaryKey(1, keyOrder: 0), NonUnique]
//     public int Gender { get; set; }
//
//     public string Name { get; set; }
// }

// public static class Initializer
// {
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     public static void SetupMessagePackResolver()
//     {
//         // Create CompositeResolver
//         StaticCompositeResolver.Instance.Register(new[]
//         {
//             MasterMemoryResolver.Instance, // set MasterMemory generated resolver
//             StandardResolver.Instance      // set default MessagePack resolver
//         });
//
//         // Set as default
//         var options = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);
//         MessagePackSerializer.DefaultOptions = options;
//     }
// }