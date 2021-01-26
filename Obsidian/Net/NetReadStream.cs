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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian.Net
{
    /// <summary>
    /// Read-only buffered stream.
    /// </summary>
    [DebuggerDisplay("{ToString(),nq}")]
    public struct NetReadStream : IDisposable
    {
        /// <summary>
        /// How many bytes of data are buffered by this stream.
        /// </summary>
        public int Length => _buffer.Length;

        /// <summary>
        /// Position in the data buffer from which data will be read.
        /// </summary>
        public int Position => dataLength;

        /// <summary>
        /// How many bytes of data were not read yet.
        /// </summary>
        public int DataLeft => _buffer.Length - dataLength;
        
        /// <summary>
        /// How many bytes of data were read from the <see cref="_buffer"/>.
        /// </summary>
        private int dataLength;

        /// <summary>
        /// This stream's underlying buffer.
        /// </summary>
        private byte[] _buffer;

        /// <summary>
        /// Creates a new instance of <see cref="NetReadStream"/> around a buffer.
        /// </summary>
        /// <param name="buffer">Data to be read from this stream.</param>
        /// <returns></returns>
        public static NetReadStream Fill(byte[] buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            return new NetReadStream
            {
                _buffer = buffer
            };
        }

        /// <summary>
        /// Asynchronously fills stream's buffer with packet data from socket.
        /// </summary>
        /// <param name="socket">Socket to be read from.</param>
        /// <returns>Stream with buffered packet data.</returns>
        public static async ValueTask<NetReadStream> FillAsync(Socket socket)
        {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));
            
            var stream = new NetReadStream();

            int length = ReadLength(socket);
            stream._buffer = ArrayPool<byte>.Shared.Rent(length);
            await socket.ReceiveAsync(stream._buffer, SocketFlags.None);

            return stream;
        }

        /// <summary>
        /// Asynchronously fills stream's buffer with packet data from socket.
        /// </summary>
        /// <param name="socket">Socket to be read from.</param>
        /// <returns>Stream with buffered packet data.</returns>
        public static async ValueTask<NetReadStream> FillAsync(Socket socket, CancellationToken cancellationToken)
        {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));

            var stream = new NetReadStream();

            int length = ReadLength(socket);
            stream._buffer = ArrayPool<byte>.Shared.Rent(length);
            await socket.ReceiveAsync(stream._buffer, SocketFlags.None, cancellationToken);

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

        /// <summary>
        /// Creates a new read-only <see cref="Stream"/> around this <see cref="NetReadStream"/>.
        /// </summary>
        /// <returns>Read-only <see cref="Stream"/> that wraps around <see cref="NetReadStream"/>.</returns>
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
        /// <summary>
        /// Reads bytes from this <see cref="NetReadStream"/> to specified location in <see cref="byte"/> buffer.
        /// </summary>
        /// <param name="buffer">Destination for bytes.</param>
        /// <param name="offset">Index of where first byte will be written to.</param>
        /// <param name="length">How many bytes should be read.</param>
        /// <returns>How many bytes were read into the <paramref name="buffer"/>.</returns>
        public int Read(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(_buffer, dataLength, buffer, offset, length);
            dataLength += length;
            return length;
        }

        /// <summary>
        /// Reads bytes into a <see cref="Span{T}"/> of <see cref="byte"/>s.
        /// </summary>
        /// <param name="span">Destination for bytes.</param>
        /// <returns>How many bytes were read into the <paramref name="span"/>.</returns>
        public int Read(Span<byte> span)
        {
            _buffer.AsSpan(dataLength, span.Length).CopyTo(span);
            dataLength += span.Length;
            return span.Length;
        }

        /// <summary>
        /// Reads <see cref="sbyte"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="sbyte"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadByte()
        {
            return (sbyte)_buffer[dataLength++];
        }

        /// <summary>
        /// Reads <see cref="byte"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="byte"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadUnsignedByte()
        {
            return _buffer[dataLength++];
        }

        /// <summary>
        /// Reads <see cref="bool"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="bool"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            return _buffer[dataLength++] == 0x01;
        }

        /// <summary>
        /// Reads <see cref="ushort"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="ushort"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUnsignedShort()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<ushort>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(ushort));
            var value = Unsafe.ReadUnaligned<ushort>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(ushort);
            return value;
        }

        /// <summary>
        /// Reads <see cref="short"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="short"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<short>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(short));
            var value = Unsafe.ReadUnaligned<short>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(short);
            return value;
        }

        /// <summary>
        /// Reads <see cref="int"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="int"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<int>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(int));
            var value = Unsafe.ReadUnaligned<int>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(int);
            return value;
        }

        /// <summary>
        /// Reads <see cref="long"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="long"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<long>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(long));
            var value = Unsafe.ReadUnaligned<long>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(long);
            return value;
        }

        /// <summary>
        /// Reads <see cref="ulong"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="ulong"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUnsignedLong()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<ulong>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(ulong));
            var value = Unsafe.ReadUnaligned<ulong>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(ulong);
            return value;
        }

        /// <summary>
        /// Reads <see cref="float"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="float"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<float>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(float));
            var value = Unsafe.ReadUnaligned<float>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(float);
            return value;
        }

        /// <summary>
        /// Reads <see cref="double"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="double"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
#if BIGENDIAN
            var value = Unsafe.ReadUnaligned<double>(ref _buffer[dataLength]);
#else
            _buffer.Reverse(dataLength, sizeof(double));
            var value = Unsafe.ReadUnaligned<double>(ref _buffer[dataLength]);
#endif
            dataLength += sizeof(double);
            return value;
        }

        /// <summary>
        /// Reads <see cref="string"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="string"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            var length = ReadVarInt();
            var value = Encoding.UTF8.GetString(_buffer, dataLength, length);
            dataLength += length;
            return value;
        }

        /// <summary>
        /// Reads <see cref="int"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="int"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="byte"/>[] from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="byte"/>[].</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadUnsignedByteArray()
        {
            int length = ReadVarInt();

            return length != 0 ? _buffer.Subarray(dataLength, length) : Array.Empty<byte>();
        }

        /// <summary>
        /// Reads <see cref="long"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="long"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="API.Position"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="API.Position"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="API.Position"/> in absolute format from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="API.Position"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="PositionF"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="PositionF"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="PositionF"/> in absolute format from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="PositionF"/>.</returns>
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

        /// <summary>
        /// Reads <see cref="SoundPosition"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="SoundPosition"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SoundPosition ReadSoundPosition()
        {
            return new SoundPosition(ReadInt(), ReadInt(), ReadInt());
        }

        /// <summary>
        /// Reads <see cref="Angle"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="Angle"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Angle ReadAngle()
        {
            return new Angle(ReadUnsignedByte());
        }

        /// <summary>
        /// Reads <see cref="Velocity"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="Velocity"/>.</returns>
        [ReadMethod]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Velocity ReadVelocity()
        {
            return new Velocity(ReadShort(), ReadShort(), ReadShort());
        }

        /// <summary>
        /// Reads <see cref="Guid"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="Guid"/>.</returns>
        [ReadMethod]
        public Guid ReadGuid()
        {
            return Guid.Parse(ReadString());
        }

        /// <summary>
        /// Reads <see cref="ItemStack"/> from this stream.
        /// </summary>
        /// <returns>Read value as <see cref="ItemStack"/>.</returns>
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

    internal static partial class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Subarray(this byte[] array, int index, int length)
        {
            var subarray = new byte[length];
            Array.Copy(array, index, subarray, 0, length);
            return subarray;
        }
    }
}
