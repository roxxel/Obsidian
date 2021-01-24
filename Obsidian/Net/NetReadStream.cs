using Newtonsoft.Json;
using Obsidian.API;
using Obsidian.Chat;
using Obsidian.Nbt;
using Obsidian.Nbt.Tags;
using Obsidian.Serialization.Attributes;
using Obsidian.Util.Extensions;
using Obsidian.Util.Registry;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Obsidian.Net
{
    [DebuggerDisplay("{ToString(),nq}")]
    public struct NetReadStream : IDisposable
    {
        /// <summary>
        /// How many bytes of data were read from the <see cref="_buffer"/>.
        /// </summary>
        private int dataLength;

        /// <summary>
        /// This stream's underlying buffer.
        /// </summary>
        private byte[] _buffer;

        public static NetReadStream Fill(byte[] buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            return new NetReadStream
            {
                _buffer = buffer
            };
        }

        public static async ValueTask<NetReadStream> FillAsync(Client client)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));
            
            var stream = new NetReadStream();
            var socket = client.tcp.Client;

            int length = ReadLength(socket);
            stream._buffer = ArrayPool<byte>.Shared.Rent(length);
            await socket.ReceiveAsync(stream._buffer, SocketFlags.None);

            return stream;
        }

        public override string ToString()
        {
            return $"NetReadStream({dataLength}/{_buffer.Length})";
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        public Stream GetStream()
        {
            return new ReadStream(this);
        }

        private static int ReadLength(Socket socket)
        {
            int numRead = 0;
            int result = 0;
            Span<byte> span = stackalloc byte[1];
            do
            {
                socket.Receive(span);
                int value = span[0] & 0b01111111;
                result |= value << (7 * numRead);

                numRead++;
            } while ((span[0] & 0b10000000) != 0);

            return result;
        }

        #region Read Methods
        public int Read(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(_buffer, dataLength, buffer, offset, length);
            dataLength += length;
            return length;
        }

        public int Read(Span<byte> span)
        {
            _buffer.AsSpan(dataLength, span.Length).CopyTo(span);
            dataLength += span.Length;
            return span.Length;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadByte()
        {
            return (sbyte)_buffer[dataLength++];
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadUnsignedByte()
        {
            return _buffer[dataLength++];
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            return _buffer[dataLength++] == 0x01;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUnsignedShort()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<ushort>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(ushort));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<ushort>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(ushort);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<short>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(short));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<short>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(short);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<int>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(int));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(int);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<long>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(long));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(long);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUnsignedLong()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<ulong>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(ulong));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(ulong);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<float>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(float));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<float>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(float);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<double>(ref _buffer[dataLength]);
#else
            var span = _buffer.AsSpan(dataLength, sizeof(double));
            span.Reverse();
            var value = Unsafe.ReadUnaligned<double>(ref MemoryMarshal.GetReference(span));
#endif
            dataLength += sizeof(double);
            return value;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            var length = ReadVarInt();
            var value = Encoding.UTF8.GetString(_buffer, dataLength, length);
            dataLength += length;
            return value;
        }

        [ReadMethod, VarLength]
        public int ReadVarInt()
        {
            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                read = _buffer[dataLength++];
                int value = read & 0b01111111;
                result |= value << (7 * numRead);

                numRead++;
            } while ((read & 0b10000000) != 0);

            return result;
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadUnsignedByteArray()
        {
            int length = ReadVarInt();

            return length != 0 ? _buffer.Subarray(dataLength, length) : Array.Empty<byte>();
        }

        [ReadMethod, VarLength]
        public long ReadVarLong()
        {
            int numRead = 0;
            long result = 0;
            byte read;
            do
            {
                read = _buffer[dataLength++];
                int value = (read & 0b01111111);
                result |= (long)value << (7 * numRead);

                numRead++;
            } while ((read & 0b10000000) != 0);

            return result;
        }

        [ReadMethod]
        public Position ReadPosition()
        {
            ulong value = ReadUnsignedLong();

            long x = (long)(value >> 38);
            long y = (long)(value & 0xFFF);
            long z = (long)(value << 26 >> 38);

            if (x >= Math.Pow(2, 25))
                x -= (long)Math.Pow(2, 26);

            if (y >= Math.Pow(2, 11))
                y -= (long)Math.Pow(2, 12);

            if (z >= Math.Pow(2, 25))
                z -= (long)Math.Pow(2, 26);

            return new Position
            {
                X = (int)x,
                Y = (int)y,
                Z = (int)z,
            };
        }

        [ReadMethod, Absolute]
        public Position ReadAbsolutePosition()
        {
            return new Position
            {
                X = (int)ReadDouble(),
                Y = (int)ReadDouble(),
                Z = (int)ReadDouble()
            };
        }

        [ReadMethod]
        public PositionF ReadPositionF()
        {
            ulong value = this.ReadUnsignedLong();

            long x = (long)(value >> 38);
            long y = (long)(value & 0xFFF);
            long z = (long)(value << 26 >> 38);

            if (x >= Math.Pow(2, 25))
                x -= (long)Math.Pow(2, 26);

            if (y >= Math.Pow(2, 11))
                y -= (long)Math.Pow(2, 12);

            if (z >= Math.Pow(2, 25))
                z -= (long)Math.Pow(2, 26);

            return new PositionF
            {
                X = x,
                Y = y,
                Z = z,
            };
        }

        [ReadMethod, Absolute]
        public PositionF ReadAbsolutePositionF()
        {
            return new PositionF
            {
                X = (float)ReadDouble(),
                Y = (float)ReadDouble(),
                Z = (float)ReadDouble()
            };
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SoundPosition ReadSoundPosition()
        {
            return new SoundPosition(ReadInt(), ReadInt(), ReadInt());
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Angle ReadAngle()
        {
            return new Angle(ReadUnsignedByte());
        }

        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Velocity ReadVelocity()
        {
            return new Velocity(ReadShort(), ReadShort(), ReadShort());
        }

        [ReadMethod]
        public Guid ReadGuid()
        {
            return Guid.Parse(ReadString());
        }

        [ReadMethod]
        public ItemStack ReadItemStack()
        {
            var present = ReadBoolean();

            if (present)
            {
                var item = Registry.GetItem((short)ReadVarInt());

                var slot = new ItemStack(item.Type, ReadUnsignedByte())
                {
                    Present = present
                };

                var reader = new NbtReader(GetStream());

                while (reader.ReadToFollowing())
                {
                    var itemMetaBuilder = new ItemMetaBuilder();

                    if (reader.IsCompound)
                    {
                        var root = (NbtCompound)reader.ReadAsTag();

                        foreach (var tag in root)
                        {
                            switch (tag.Name.ToUpperInvariant())
                            {
                                case "ENCHANTMENTS":
                                    {
                                        var enchantments = (NbtList)tag;

                                        foreach (var enchant in enchantments)
                                        {
                                            if (enchant is NbtCompound compound)
                                            {
                                                var id = compound.Get<NbtString>("id").Value;

                                                itemMetaBuilder.AddEnchantment(id.ToEnchantType(), compound.Get<NbtShort>("lvl").Value);
                                            }
                                        }

                                        break;
                                    }

                                case "STOREDENCHANTMENTS":
                                    {
                                        var enchantments = (NbtList)tag;

                                        //Globals.PacketLogger.LogDebug($"List Type: {enchantments.ListType}");

                                        foreach (var enchantment in enchantments)
                                        {
                                            if (enchantment is NbtCompound compound)
                                            {

                                                var id = compound.Get<NbtString>("id").Value;

                                                itemMetaBuilder.AddStoredEnchantment(id.ToEnchantType(), compound.Get<NbtShort>("lvl").Value);
                                            }
                                        }
                                        break;
                                    }

                                case "SLOT":
                                    {
                                        itemMetaBuilder.WithSlot(tag.ByteValue);
                                        //Console.WriteLine($"Setting slot: {itemMetaBuilder.Slot}");
                                        break;
                                    }

                                case "DAMAGE":
                                    {
                                        itemMetaBuilder.WithDurability(tag.IntValue);
                                        //Globals.PacketLogger.LogDebug($"Setting damage: {tag.IntValue}");
                                        break;
                                    }

                                case "DISPLAY":
                                    {
                                        var display = (NbtCompound)tag;

                                        foreach (var displayTag in display)
                                        {
                                            if (displayTag.Name.EqualsIgnoreCase("name"))
                                            {
                                                itemMetaBuilder.WithName(displayTag.StringValue);
                                            }
                                            else if (displayTag.Name.EqualsIgnoreCase("lore"))
                                            {
                                                var loreTag = (NbtList)displayTag;

                                                foreach (var lore in loreTag)
                                                    itemMetaBuilder.AddLore(JsonConvert.DeserializeObject<ChatMessage>(lore.StringValue));
                                            }
                                        }
                                        break;
                                    }
                            }
                        }
                    }

                    slot.ItemMeta = itemMetaBuilder.Build();
                }

                return slot;
            }

            return null;
        }
        #endregion

        private sealed class ReadStream : Stream
        {
            private NetReadStream stream;

            public ReadStream(NetReadStream stream)
            {
                this.stream = stream;
            }
            
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override bool CanTimeout => false;

            public override long Length => stream._buffer.Length;

            public override long Position { get => stream.dataLength; set => throw new NotSupportedException(); }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return stream.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                return stream.Read(buffer);
            }

            public override int ReadByte()
            {
                return stream.ReadUnsignedByte();
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
                throw new NotSupportedException();
            }
        }
    }

    internal static class Extensions
    {
        public static byte[] Subarray(this byte[] array, int index, int length)
        {
            var subarray = new byte[length];
            Array.Copy(array, index, subarray, 0, length);
            return subarray;
        }
    }
}
