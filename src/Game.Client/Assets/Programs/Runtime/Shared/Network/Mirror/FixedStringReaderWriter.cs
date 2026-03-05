using Mirror;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Shared.Network.Mirror
{
    /// <summary>
    /// Mirror Weaver 向け FixedString カスタム Reader/Writer。
    /// unsafe ポインタによるゼロ GC シリアライズ。
    /// </summary>
    public static class FixedStringReaderWriter
    {
        // --- FixedString64Bytes ---

        public static void WriteFixedString64Bytes(this NetworkWriter writer, FixedString64Bytes value)
        {
            ushort byteLength = (ushort)value.Length;
            writer.WriteUShort(byteLength);
            unsafe
            {
                byte* ptr = value.GetUnsafePtr();
                for (int i = 0; i < byteLength; i++)
                    writer.WriteByte(ptr[i]);
            }
        }

        public static FixedString64Bytes ReadFixedString64Bytes(this NetworkReader reader)
        {
            ushort byteLength = reader.ReadUShort();
            var result = new FixedString64Bytes();
            unsafe
            {
                byte* ptr = result.GetUnsafePtr();
                for (int i = 0; i < byteLength; i++)
                    ptr[i] = reader.ReadByte();

                // 内部 length フィールド（構造体先頭の ushort）を直接設定
                *(ushort*)UnsafeUtility.AddressOf(ref result) = byteLength;
            }
            return result;
        }

        // --- FixedString128Bytes ---

        public static void WriteFixedString128Bytes(this NetworkWriter writer, FixedString128Bytes value)
        {
            ushort byteLength = (ushort)value.Length;
            writer.WriteUShort(byteLength);
            unsafe
            {
                byte* ptr = value.GetUnsafePtr();
                for (int i = 0; i < byteLength; i++)
                    writer.WriteByte(ptr[i]);
            }
        }

        public static FixedString128Bytes ReadFixedString128Bytes(this NetworkReader reader)
        {
            ushort byteLength = reader.ReadUShort();
            var result = new FixedString128Bytes();
            unsafe
            {
                byte* ptr = result.GetUnsafePtr();
                for (int i = 0; i < byteLength; i++)
                    ptr[i] = reader.ReadByte();

                *(ushort*)UnsafeUtility.AddressOf(ref result) = byteLength;
            }
            return result;
        }
    }
}
