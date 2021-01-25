using Newtonsoft.Json;
using Obsidian.API;
using Obsidian.API.Crafting;
using Obsidian.Boss;
using Obsidian.Chat;
using Obsidian.Commands;
using Obsidian.Entities;
using Obsidian.Nbt;
using Obsidian.Nbt.Tags;
using Obsidian.Net.Packets.Play.Clientbound;
using Obsidian.PlayerData.Info;
using Obsidian.Serialization.Attributes;
using Obsidian.Util.Extensions;
using Obsidian.Util.Registry.Codecs.Dimensions;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian.Net
{
    /// <summary>
    /// Write-only buffered stream.
    /// </summary>
    [DebuggerDisplay("{ToString(),nq}")]
    public struct NetWriteStream : IDisposable
    {
        /// <summary>
        /// How many bytes were written to this stream.
        /// </summary>
        public int Length => dataLength;

        /// <summary>
        /// How many bytes can be written to this stream.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// How many bytes of data were written to <see cref="_buffer"/>.
        /// </summary>
        private int dataLength;

        /// <summary>
        /// This stream's underlying buffer.
        /// </summary>
        private byte[] _buffer;

        private const int maxVarIntSize = 5;
        private const int maxVarLongSize = 10;

        /// <summary>
        /// Creates a new instance of <see cref="NetWriteStream"/> with specified minimum size of underlying buffer.
        /// </summary>
        /// <param name="minBufferSize">Minimal length of underlying buffer.</param>
        public NetWriteStream(int minBufferSize)
        {
            if (minBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(minBufferSize));
            
            dataLength = 0;
            _buffer = ArrayPool<byte>.Shared.Rent(minBufferSize);
        }

        /// <summary>
        /// Sends buffered data asynchronously to the client.
        /// </summary>
        /// <param name="socket">Destination socket.</param>
        /// <returns>Number of bytes sent to the client if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Flush(Socket socket)
        {
            return socket.Send(_buffer, 0, dataLength, SocketFlags.None);
        }

        /// <summary>
        /// Sends buffered data asynchronously to the client.
        /// </summary>
        /// /// <param name="socket">Destination socket.</param>
        /// <returns>A task that completes with number of bytes sent to the client if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<int> FlushAsync(Socket socket)
        {
            return socket.SendAsync(_buffer.AsMemory(0, dataLength), SocketFlags.None);
        }

        /// <summary>
        /// Sends buffered data asynchronously to the client.
        /// </summary>
        /// /// <param name="socket">Destination socket.</param>
        /// <returns>A task that completes with number of bytes sent to the client if the operation was successful.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<int> FlushAsync(Socket socket, CancellationToken cancellationToken)
        {
            return socket.SendAsync(_buffer.AsMemory(0, dataLength), SocketFlags.None, cancellationToken);
        }

        /// <summary>
        /// Copies buffered data to another stream.
        /// </summary>
        /// <param name="other">Destination for copied data.</param>
        public void CopyTo(NetWriteStream other)
        {
            other.EnsureCapacity(_buffer.Length);
            Buffer.BlockCopy(_buffer, 0, other._buffer, other.dataLength, _buffer.Length);
        }

        /// <summary>
        /// Creates a new write-only <see cref="Stream"/> around this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <returns>A new write-only <see cref="Stream"/> that wraps around <see cref="NetWriteStream"/>.</returns>
        public Stream GetStream()
        {
            return new WriteStream(this);
        }

        public override string ToString()
        {
            return $"NetWriteStream({dataLength}/{_buffer.Length})";
        }

        public void Dispose()
        {
            dataLength = _buffer.Length; // Ensure that the buffer can no longer be written to
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        private void EnsureCapacity(int additionalNeededCapacity)
        {
            if (dataLength + additionalNeededCapacity >= _buffer.Length)
            {
                int newCapacity = Math.Max(_buffer.Length, 128);
                do
                {
                    newCapacity *= 2;
                }
                while (dataLength + additionalNeededCapacity >= newCapacity);

                var temp = ArrayPool<byte>.Shared.Rent(newCapacity);
                Buffer.BlockCopy(_buffer, 0, temp, 0, dataLength);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = temp;
            }
        }

        #region Write Methods
        /// <summary>
        /// Writes an array of bytes to this <see cref="NetWriteStream"/> with specified offset and length.
        /// </summary>
        /// <param name="value">Byte array to be written.</param>
        /// <param name="offset">Index of the first byte to be written.</param>
        /// <param name="length">How many bytes from the source array should be written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] value, int offset, int length)
        {
            EnsureCapacity(length);

            Buffer.BlockCopy(value, offset, _buffer, dataLength, length);
            dataLength += length;
        }

        /// <summary>
        /// Writes a span of bytes to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value">Span of bytes to be written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<byte> value)
        {
            EnsureCapacity(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                _buffer[dataLength + i] = value[i];
            }
            dataLength += value.Length;
        }

        /// <summary>
        /// Writes specified value of <see cref="sbyte"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="sbyte"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(sbyte value)
        {
            EnsureCapacity(sizeof(sbyte));

            _buffer[dataLength++] = (byte)value;
        }

        /// <summary>
        /// Writes specified value of <see cref="byte"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="byte"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnsignedByte(byte value)
        {
            EnsureCapacity(sizeof(byte));

            _buffer[dataLength++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsignedByteUnsafe(byte value)
        {
            _buffer[dataLength++] = value;
        }

        /// <summary>
        /// Writes specified value of <see cref="bool"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="bool"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool value)
        {
            EnsureCapacity(sizeof(bool));

            _buffer[dataLength++] = value ? 0x01 : 0x00;
        }

        /// <summary>
        /// Writes specified value of <see cref="ushort"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="ushort"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnsignedShort(ushort value)
        {
            EnsureCapacity(sizeof(ushort));

            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(ushort));
#endif
            dataLength += sizeof(ushort);
        }

        /// <summary>
        /// Writes specified value of <see cref="short"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="short"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShort(short value)
        {
            EnsureCapacity(sizeof(short));

            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(short));
#endif
            dataLength += sizeof(short);
        }

        /// <summary>
        /// Writes specified value of <see cref="int"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="int"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt(int value)
        {
            EnsureCapacity(sizeof(int));

            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(int));
#endif
            dataLength += sizeof(int);
        }

        /// <summary>
        /// Writes specified value of <see cref="long"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="long"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLong(long value)
        {
            EnsureCapacity(sizeof(long));

            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(long));
#endif
            dataLength += sizeof(long);
        }

        /// <summary>
        /// Writes specified value of <see cref="float"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="float"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            EnsureCapacity(sizeof(float));

            WriteFloatUnsafe(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFloatUnsafe(float value)
        {
            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(float));
#endif
            dataLength += sizeof(float);
        }

        /// <summary>
        /// Writes specified value of <see cref="double"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="double"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            EnsureCapacity(sizeof(double));

            Unsafe.WriteUnaligned(ref _buffer[dataLength], value);
#if !BIGENDIAN
            _buffer.Reverse(dataLength, sizeof(double));
#endif
            dataLength += sizeof(double);
        }

        /// <summary>
        /// Writes specified value of <see cref="string"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="string"/> value to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string value)
        {
            Debug.Assert(value.Length <= short.MaxValue);

            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteVarInt(byteCount);

            EnsureCapacity(byteCount);
            dataLength += Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, dataLength);
        }

        /// <summary>
        /// Writes specified value of <see cref="int"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="int"/> value to be written.</param>
        [WriteMethod, VarLength]
        public void WriteVarInt(int value)
        {
            EnsureCapacity(maxVarIntSize);

            WriteVarIntUnsafe(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteVarIntUnsafe(int value)
        {
            var unsigned = (uint)value;

            do
            {
                var temp = (byte)(unsigned & 127);
                unsigned >>= 7;

                if (unsigned != 0)
                    temp |= 128;

                _buffer[dataLength++] = temp;
            }
            while (unsigned != 0);
        }

        /// <summary>
        /// Writes specified value of <see cref="Enum"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Enum"/> value to be written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVarInt(Enum value)
        {
            WriteVarInt(Convert.ToInt32(value));
        }

        /// <summary>
        /// Writes specified value of <see cref="long"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="long"/> value to be written.</param>
        [WriteMethod, VarLength]
        public void WriteVarLong(long value)
        {
            EnsureCapacity(maxVarLongSize);

            var unsigned = (ulong)value;

            do
            {
                var temp = (byte)(unsigned & 127);

                unsigned >>= 7;

                if (unsigned != 0)
                    temp |= 128;


                _buffer[dataLength++] = temp;
            }
            while (unsigned != 0);
        }

        /// <summary>
        /// Writes specified <see cref="Angle"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Angle"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAngle(Angle angle)
        {
            WriteUnsignedByte(angle.Value);
        }

        /// <summary>
        /// Writes specified <see cref="ChatMessage"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="ChatMessage"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChat(ChatMessage chatMessage)
        {
            WriteString(chatMessage.ToString());
        }

        /// <summary>
        /// Writes an array of bytes to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value">Byte array to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteArray(byte[] value)
        {
            EnsureCapacity(value.Length);

            WriteByteArrayUnsafe(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByteArrayUnsafe(byte[] value)
        {
            Buffer.BlockCopy(value, srcOffset: 0, _buffer, dataLength, value.Length);
        }

        /// <summary>
        /// Writes specified <see cref="Guid"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Guid"/> to be written.</param>
        [WriteMethod]
        public void WriteGuid(Guid value)
        {
            EnsureCapacity(16);

            UUID.FromGuid(value).WriteTo(_buffer.AsSpan(dataLength, 16));
            dataLength += 16;
        }

        /// <summary>
        /// Writes specified <see cref="Position"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Position"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePosition(Position value)
        {
            var val = (long)(value.X & 0x3FFFFFF) << 38;
            val |= (long)(value.Z & 0x3FFFFFF) << 12;
            val |= (long)(value.Y & 0xFFF);

            WriteLong(val);
        }

        /// <summary>
        /// Writes specified <see cref="Position"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Position"/> to be written.</param>
        [WriteMethod, Absolute]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAbsolutePosition(Position value)
        {
            EnsureCapacity(sizeof(double) * 3);

#if BIGENDIAN
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength, sizeof(double)), (double)value.X);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + sizeof(double), sizeof(double)), (double)value.Y);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + sizeof(double) * 2, sizeof(double)), (double)value.Z);
#else
            Span<byte> span = _buffer.AsSpan(dataLength, sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.X);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + sizeof(double), sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.Y);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + sizeof(double) * 2, sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.Z);
            span.Reverse();
#endif
            dataLength += sizeof(double) * 3;
        }

        /// <summary>
        /// Writes specified <see cref="PositionF"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="PositionF"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePositionF(PositionF value)
        {
            var val = (long)((int)value.X & 0x3FFFFFF) << 38;
            val |= (long)((int)value.Z & 0x3FFFFFF) << 12;
            val |= (long)((int)value.Y & 0xFFF);

            WriteLong(val);
        }

        /// <summary>
        /// Writes specified <see cref="PositionF"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="PositionF"/> to be written.</param>
        [WriteMethod, Absolute]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAbsolutePositionF(PositionF value)
        {
            EnsureCapacity(sizeof(double) * 3);

#if BIGENDIAN
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength, sizeof(double)), (double)value.X);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + sizeof(double), sizeof(double)), (double)value.Y);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + sizeof(double) * 2, sizeof(double)), (double)value.Z);
#else
            Span<byte> span = _buffer.AsSpan(dataLength, sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.X);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + sizeof(double), sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.Y);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + sizeof(double) * 2, sizeof(double));
            BitConverter.TryWriteBytes(span, (double)value.Z);
            span.Reverse();
#endif
            dataLength += sizeof(double) * 3;
        }

        /// <summary>
        /// Writes specified <see cref="BossBarAction"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="BossBarAction"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBossBarAction(BossBarAction value)
        {
            var data = value.ToArray();
            EnsureCapacity(maxVarIntSize + data.Length);

            WriteVarIntUnsafe(value.Action);
            WriteByteArrayUnsafe(data);
        }

        /// <summary>
        /// Writes specified <see cref="Tag"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Tag"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTag(Tag value)
        {
            WriteString(value.Name);

            EnsureCapacity((value.Entries.Count + 1) * maxVarIntSize);

            WriteVarIntUnsafe(value.Count);
            for (int i = 0; i < value.Entries.Count; i++)
            {
                WriteVarIntUnsafe(value.Entries[i]);
            }
        }

        /// <summary>
        /// Writes specified <see cref="CommandNode"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="CommandNode"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCommandNode(CommandNode value)
        {
            value.CopyTo(this);
        }

        /// <summary>
        /// Writes specified <see cref="ItemStack"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="ItemStack"/> to be written.</param>
        [WriteMethod]
        public void WriteItemStack(ItemStack value)
        {
            value ??= new ItemStack(0, 0) { Present = true };
            WriteBoolean(value.Present);
            if (value.Present)
            {
                var item = value.GetItem();

                WriteVarInt(item.Id);
                WriteByte((sbyte)value.Count);

                NbtWriter writer = new(GetStream(), string.Empty);
                ItemMeta meta = value.ItemMeta;

                if (meta.HasTags())
                {
                    writer.WriteByte("Unbreakable", meta.Unbreakable ? 1 : 0);

                    if (meta.Durability > 0)
                        writer.WriteInt("Damage", meta.Durability);

                    if (meta.CustomModelData > 0)
                        writer.WriteInt("CustomModelData", meta.CustomModelData);

                    if (meta.CanDestroy is not null)
                    {
                        writer.BeginList("CanDestroy", NbtTagType.String, meta.CanDestroy.Count);

                        foreach (var block in meta.CanDestroy)
                            writer.WriteString(block);

                        writer.EndList();
                    }

                    if (meta.Name is not null)
                    {
                        writer.BeginCompound("display");

                        writer.WriteString("Name", JsonConvert.SerializeObject(new List<ChatMessage> { (ChatMessage)meta.Name }));

                        if (meta.Lore is not null)
                        {
                            writer.BeginList("Lore", NbtTagType.String, meta.Lore.Count);

                            foreach (var lore in meta.Lore)
                                writer.WriteString(JsonConvert.SerializeObject(new List<ChatMessage> { (ChatMessage)lore }));

                            writer.EndList();
                        }

                        writer.EndCompound();
                    }
                    else if (meta.Lore is not null)
                    {
                        writer.BeginCompound("display");

                        writer.BeginList("Lore", NbtTagType.String, meta.Lore.Count);

                        foreach (var lore in meta.Lore)
                            writer.WriteString(JsonConvert.SerializeObject(new List<ChatMessage> { (ChatMessage)lore }));

                        writer.EndList();

                        writer.EndCompound();
                    }
                }

                writer.WriteString("id", item.UnlocalizedName);
                writer.WriteByte("Count", (byte)value.Count);

                writer.EndCompound();
                writer.Finish();
            }
        }

        /// <summary>
        /// Writes specified <see cref="Entity"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Entity"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntity(Entity value)
        {
            value.Write(this);
            WriteUnsignedByte(0xff);
        }

        /// <summary>
        /// Writes entity metadata header to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="index">Index of metadata element to be written.</param>
        /// <param name="entityMetadataType">Type of metadata to be written.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntityMetadataType(byte index, EntityMetadataType entityMetadataType)
        {
            EnsureCapacity(1 + maxVarIntSize);

            WriteUnsignedByteUnsafe(index);
            WriteVarIntUnsafe((int)entityMetadataType);
        }

        /// <summary>
        /// Writes specified <see cref="Velocity"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="Velocity"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVelocity(Velocity value)
        {
            EnsureCapacity(6);

#if BIGENDIAN
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength, 2), value.X);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + 2, 2), value.Y);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + 4, 2), value.Z);
#else
            Span<byte> span = _buffer.AsSpan(dataLength, 2);
            BitConverter.TryWriteBytes(span, value.X);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + 2, 2);
            BitConverter.TryWriteBytes(span, value.Y);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + 4, 2);
            BitConverter.TryWriteBytes(span, value.Z);
            span.Reverse();
#endif
            dataLength += 6;
        }

        /// <summary>
        /// Writes specified <see cref="MixedCodec"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="MixedCodec"/> to be written.</param>
        [WriteMethod]
        public void WriteMixedCodec(MixedCodec value)
        {
            var dimensions = new NbtCompound(value.Dimensions.Name)
            {
                new NbtString("type", value.Dimensions.Name)
            };

            var list = new NbtList("value", NbtTagType.Compound);

            foreach (var (_, codec) in value.Dimensions)
            {
                codec.Write(list);
            }

            dimensions.Add(list);

            #region biomes
            var biomeCompound = new NbtCompound(value.Biomes.Name)
            {
                new NbtString("type", value.Biomes.Name)
            };

            var biomes = new NbtList("value", NbtTagType.Compound);

            foreach (var (_, biome) in value.Biomes)
            {
                biome.Write(biomes);
            }

            biomeCompound.Add(biomes);
            #endregion

            var compound = new NbtCompound(string.Empty)
            {
                dimensions,
                biomeCompound
            };
            var nbt = new NbtFile(compound);

            nbt.SaveToStream(GetStream(), NbtCompression.None);
        }

        /// <summary>
        /// Writes specified <see cref="DimensionCodec"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="DimensionCodec"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDimensionCodec(DimensionCodec value)
        {
            var nbt = new NbtFile(value.ToNbt());
            nbt.SaveToStream(GetStream(), NbtCompression.None);
        }

        /// <summary>
        /// Writes specified <see cref="SoundPosition"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="SoundPosition"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSoundPosition(SoundPosition value)
        {
            EnsureCapacity(12);

#if BIGENDIAN
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength, 4), value.X);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + 4, 4), value.Y);
            BitConverter.TryWriteBytes(_buffer.AsSpan(dataLength + 8, 4), value.Z);
#else
            Span<byte> span = _buffer.AsSpan(dataLength, 4);
            BitConverter.TryWriteBytes(span, value.X);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + 4, 4);
            BitConverter.TryWriteBytes(span, value.Y);
            span.Reverse();

            span = _buffer.AsSpan(dataLength + 8, 4);
            BitConverter.TryWriteBytes(span, value.Z);
            span.Reverse();
#endif
            dataLength += 12;
        }

        /// <summary>
        /// Writes specified <see cref="PlayerInfoAction"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="PlayerInfoAction"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePlayerInfoAction(PlayerInfoAction value)
        {
            value.Write(this);
        }

        /// <summary>
        /// Writes dictionary of <see cref="IRecipe"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value">Dictionary of <see cref="IRecipe"/> to be written.</param>
        [WriteMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRecipes(Dictionary<string, IRecipe> recipes)
        {
            WriteVarInt(recipes.Count);
            foreach (var (name, recipe) in recipes)
                WriteRecipe(name, recipe);
        }

        /// <summary>
        /// Writes specified <see cref="IRecipe"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="name">Recipe name to be written.</param>
        /// <param name="recipe"><see cref="IRecipe"/> to be written.</param>
        public void WriteRecipe(string name, IRecipe recipe)
        {
            WriteString(recipe.Type);

            WriteString(name);

            if (recipe is ShapedRecipe shapedRecipe)
            {
                var patterns = shapedRecipe.Pattern;

                int width = patterns[0].Length, height = patterns.Count;

                WriteVarInt(width);
                WriteVarInt(height);

                WriteString(shapedRecipe.Group ?? string.Empty);

                var ingredients = new List<ItemStack>[width * height];

                var y = 0;
                foreach (var pattern in patterns)
                {
                    var x = 0;
                    foreach (var c in pattern)
                    {
                        if (char.IsWhiteSpace(c))
                            continue;

                        var index = x + (y * width);

                        var key = shapedRecipe.Key[c];

                        foreach (var item in key)
                        {
                            if (ingredients[index] is null)
                                ingredients[index] = new List<ItemStack> { item };
                            else
                                ingredients[index].Add(item);
                        }

                        x++;
                    }
                    y++;
                }

                foreach (var items in ingredients)
                {
                    if (items == null)
                    {
                        WriteVarInt(1);
                        WriteItemStack(ItemStack.Air);
                        continue;
                    }

                    WriteVarInt(items.Count);

                    foreach (var itemStack in items)
                        WriteItemStack(itemStack);
                }

                WriteItemStack(shapedRecipe.Result.First());
            }
            else if (recipe is ShapelessRecipe shapelessRecipe)
            {
                var ingredients = shapelessRecipe.Ingredients;

                WriteString(shapelessRecipe.Group ?? string.Empty);

                WriteVarInt(ingredients.Count);
                foreach (var ingredient in ingredients)
                {
                    WriteVarInt(ingredient.Count);
                    foreach (var item in ingredient)
                        WriteItemStack(item);
                }

                var result = shapelessRecipe.Result.First();

                WriteItemStack(result);
            }
            else if (recipe is SmeltingRecipe smeltingRecipe)
            {
                WriteString(smeltingRecipe.Group ?? string.Empty);


                WriteVarInt(smeltingRecipe.Ingredient.Count);
                foreach (var i in smeltingRecipe.Ingredient)
                    WriteItemStack(i);

                WriteItemStack(smeltingRecipe.Result.First());

                WriteFloat(smeltingRecipe.Experience);
                WriteVarInt(smeltingRecipe.Cookingtime);
            }
            else if (recipe is CuttingRecipe cuttingRecipe)
            {
                WriteString(cuttingRecipe.Group ?? string.Empty);

                WriteVarInt(cuttingRecipe.Ingredient.Count);
                foreach (var item in cuttingRecipe.Ingredient)
                    WriteItemStack(item);


                var result = cuttingRecipe.Result.First();

                result.Count = (short)cuttingRecipe.Count;

                WriteItemStack(result);
            }
            else if (recipe is SmithingRecipe smithingRecipe)
            {
                WriteVarInt(smithingRecipe.Base.Count);
                foreach (var item in smithingRecipe.Base)
                    WriteItemStack(item);

                WriteVarInt(smithingRecipe.Addition.Count);
                foreach (var item in smithingRecipe.Addition)
                    WriteItemStack(item);

                WriteItemStack(smithingRecipe.Result.First());
            }
        }

        /// <summary>
        /// Writes specified <see cref="ParticleData"/> to this <see cref="NetWriteStream"/>.
        /// </summary>
        /// <param name="value"><see cref="ParticleData"/> to be written.</param>
        [WriteMethod]
        public void WriteParticleData(ParticleData value)
        {
            if (value is null || value == ParticleData.None)
                return;

            switch (value.ParticleType)
            {
                case ParticleType.Block:
                    WriteVarInt(value.GetDataAs<int>());
                    break;

                case ParticleType.Dust:
                    EnsureCapacity(16);

                    var (red, green, blue, scale) = value.GetDataAs<(float, float, float, float)>();
                    WriteFloatUnsafe(red);
                    WriteFloatUnsafe(green);
                    WriteFloatUnsafe(blue);
                    WriteFloatUnsafe(scale);
                    break;

                case ParticleType.FallingDust:
                    WriteVarInt(value.GetDataAs<int>());
                    break;

                case ParticleType.Item:
                    WriteItemStack(value.GetDataAs<ItemStack>());
                    break;
            }
        }
        #endregion

        private sealed class WriteStream : Stream
        {
            private NetWriteStream stream;

            public WriteStream(NetWriteStream stream)
            {
                this.stream = stream;
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override bool CanTimeout => false;

            public override long Length => stream.Length;

            public override long Position { get => stream.Length; set => throw new NotSupportedException(); }

            public override void Flush()
            {
                return;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                stream.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                stream.Write(buffer);
            }

            public override void WriteByte(byte value)
            {
                stream.WriteUnsignedByte(value);
            }

            public override void CopyTo(Stream destination, int bufferSize)
            {
                destination.Write(stream._buffer, 0, bufferSize);
            }

            protected override void Dispose(bool disposing)
            {
                stream.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct UUID
        {
            private readonly int _a;
            private readonly short _b;
            private readonly short _c;
            private readonly byte _d;
            private readonly byte _e;
            private readonly byte _f;
            private readonly byte _g;
            private readonly byte _h;
            private readonly byte _i;
            private readonly byte _j;
            private readonly byte _k;

            public static UUID FromGuid(Guid guid)
            {
                return Unsafe.As<Guid, UUID>(ref guid);
            }

            public void WriteTo(Span<byte> destination)
            {
                // Write as BigEndian
                // https://github.com/dotnet/runtime/blob/796848aec642d1f8cc2c0d2ee6329ac6ecbd247e/src/libraries/System.Private.CoreLib/src/System/Guid.cs#L763
                destination[15] = _k;
                BinaryPrimitives.WriteInt32LittleEndian(destination, _a);
                BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(4), _b);
                BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(6), _c);
                destination[8] = _d;
                destination[9] = _e;
                destination[10] = _f;
                destination[11] = _g;
                destination[12] = _h;
                destination[13] = _i;
                destination[14] = _j;
            }
        }
    }

    internal static partial class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse(this byte[] array, int index, int count)
        {
            ref byte first = ref array[index];
            ref byte last = ref array[index + count - 1];
            do
            {
                byte temp = first;
                first = last;
                last = temp;
                first = ref Unsafe.Add(ref first, 1);
                last = ref Unsafe.Add(ref last, -1);
            }
            while (Unsafe.IsAddressLessThan(ref first, ref last));
        }
    }
}
