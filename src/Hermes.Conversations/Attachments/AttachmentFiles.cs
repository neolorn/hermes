using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Conversations.Attachments;

/// <summary>
/// The rules of an attachment's file and the form of its stored object (<c>09</c>
/// MSG-CONV-007, MSG-CONV-009; M-037): a name of 1 to 255 characters without path separators
/// or control characters; a declared type the configuration allows, whose first bytes match it
/// for the types the library knows; and one stored object of 64 KiB chunks, each protected
/// alone and bound to <c>attachments:&lt;id&gt;:chunk:&lt;n&gt;</c>, framed by a four-byte
/// big-endian length and streamed, so no whole file is ever held.
/// </summary>
internal static class AttachmentFiles
{
    /// <summary>The bytes of a chunk, the last one fewer.</summary>
    internal const int ChunkBytes = 64 * 1024;

    /// <summary>The most characters of a file name.</summary>
    internal const int MostNameCharacters = 255;

    /// <summary>The longest frame read back: a chunk with room for any protector's envelope.</summary>
    private const int MostFrame = ChunkBytes + 4096;

    private const int FrameHeader = 4;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The context a chunk is protected under.</summary>
    internal static string ChunkContext(Guid attachment, int chunk)
    {
        return ContentContext.Of(ConversationTables.Attachments, attachment, "chunk:" + chunk.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The context a file name is protected under.</summary>
    internal static string NameContext(Guid attachment)
    {
        return ContentContext.Of(ConversationTables.Attachments, attachment, ConversationTables.Name);
    }

    /// <summary>Whether <paramref name="name"/> is a file name: 1 to 255 characters, no path separator, no control character.</summary>
    internal static bool IsName(string? name)
    {
        return name is { Length: >= 1 and <= MostNameCharacters } && !name.Any(each => each is '/' or '\\' || char.IsControl(each));
    }

    /// <summary>A declared media type without its parameters, lower-case; none where nothing is declared.</summary>
    internal static string? TypeOf(string? declared)
    {
        if (declared is null)
        {
            return null;
        }

        int parameters = declared.IndexOf(';', StringComparison.Ordinal);
        string type = (parameters < 0 ? declared : declared[..parameters]).Trim();
        return type.Length == 0 ? null : Small(type);
    }

    /// <summary>Whether <paramref name="first"/>, a file's first bytes, match <paramref name="type"/>, for the types the library knows; any bytes for another.</summary>
    internal static bool Matches(string type, ReadOnlySpan<byte> first)
    {
        return type switch
        {
            "image/jpeg" => first.StartsWith(Jpeg),
            "image/png" => first.StartsWith(Png),
            "image/gif" => first.StartsWith("GIF87a"u8) || first.StartsWith("GIF89a"u8),
            "image/webp" => first.Length >= 12 && first.StartsWith("RIFF"u8) && first[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" => first.StartsWith("%PDF-"u8),
            _ => true,
        };
    }

    /// <summary>Draws a store key: 128 random bits in base64url, naming nothing.</summary>
    internal static string NewKey()
    {
        Span<byte> bits = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bits);
        return Convert.ToBase64String(bits).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Reads into <paramref name="buffer"/> until it is full or the stream ends, answering the bytes read.</summary>
    internal static async Task<int> FillAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await source.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }

    /// <summary>
    /// Streams a file to the store under <paramref name="key"/>: <paramref name="first"/>, its
    /// first chunk already read, then the rest of <paramref name="rest"/>, each chunk protected
    /// by <paramref name="protect"/> and framed, through a pipe the store reads as it writes.
    /// It stops where the file passes <paramref name="most"/> bytes, or where
    /// <paramref name="protect"/> answers none, failing the store's write; a failure of the
    /// store or of <paramref name="rest"/> is thrown. Answers what it wrote.
    /// </summary>
    internal static async Task<Streamed> WriteAsync(
        IAttachmentStore store,
        string key,
        ReadOnlyMemory<byte> first,
        Stream rest,
        long most,
        Func<int, ReadOnlyMemory<byte>, CancellationToken, Task<byte[]?>> protect,
        CancellationToken cancellationToken)
    {
        Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 4 * MostFrame, resumeWriterThreshold: 2 * MostFrame, useSynchronizationContext: false));
        Task put = PutAsync(store, key, pipe.Reader, cancellationToken);
        long size = 0;
        int chunks = 0;
        Streamed.Outcome outcome = Streamed.Outcome.Written;
        bool ended = false;
        byte[] buffer = new byte[ChunkBytes];
        try
        {
            ReadOnlyMemory<byte> current = first;
            while (current.Length > 0)
            {
                size += current.Length;
                if (size > most)
                {
                    outcome = Streamed.Outcome.TooLarge;
                    break;
                }

                if (await protect(chunks, current, cancellationToken).ConfigureAwait(false) is not { } stored)
                {
                    outcome = Streamed.Outcome.Stopped;
                    break;
                }

                Memory<byte> frame = pipe.Writer.GetMemory(FrameHeader + stored.Length);
                BinaryPrimitives.WriteInt32BigEndian(frame.Span, stored.Length);
                stored.CopyTo(frame[FrameHeader..]);
                pipe.Writer.Advance(FrameHeader + stored.Length);
                FlushResult flushed = await pipe.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                chunks++;
                if (flushed.IsCompleted)
                {
                    // The store stopped reading before the file's end: its own failure, where it failed, says why.
                    await put.ConfigureAwait(false);
                    throw new IOException("The attachment store stopped reading before the attachment's end.");
                }

                if (current.Length < ChunkBytes)
                {
                    break;
                }

                current = buffer.AsMemory(0, await FillAsync(rest, buffer, cancellationToken).ConfigureAwait(false));
            }

            ended = true;
        }
        finally
        {
            if (ended && outcome == Streamed.Outcome.Written)
            {
                await pipe.Writer.CompleteAsync().ConfigureAwait(false);
            }
            else
            {
                // The store's write fails as it is made to, and ends before its bytes are deleted.
                await pipe.Writer.CompleteAsync(new IOException("The attachment was refused before its end.")).ConfigureAwait(false);
                await Task.WhenAny(put).ConfigureAwait(false);
                _ = put.Exception;
            }
        }

        if (outcome == Streamed.Outcome.Written)
        {
            await put.ConfigureAwait(false);
        }

        return new Streamed(outcome, size, chunks);
    }

    /// <summary>
    /// <paramref name="text"/> with its ASCII capitals made small, as media types are compared;
    /// nothing else of it changes.
    /// </summary>
    internal static string Small(string text)
    {
        return string.Create(text.Length, text, (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char each = source[index];
                span[index] = each is >= 'A' and <= 'Z' ? (char)(each + ('a' - 'A')) : each;
            }
        });
    }

    private static async Task PutAsync(IAttachmentStore store, string key, PipeReader reader, CancellationToken cancellationToken)
    {
        // Yields first, so the writer starts while the store reads.
        await Task.Yield();
        Stream content = reader.AsStream(leaveOpen: true);
        try
        {
            await store.PutAsync(key, content, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>What <see cref="WriteAsync"/> wrote.</summary>
    /// <param name="Result">Whether the whole file was written, passed its bound, or was stopped by its protection.</param>
    /// <param name="Size">The bytes read.</param>
    /// <param name="Chunks">The chunks written.</param>
    internal sealed record Streamed(Streamed.Outcome Result, long Size, int Chunks)
    {
        internal enum Outcome
        {
            Written,
            TooLarge,
            Stopped,
        }
    }

    /// <summary>
    /// An attachment's content, read from its stored object chunk by chunk, each opened by
    /// <paramref name="open"/> as it is reached: a frame outside its bounds, a chunk that does
    /// not open, or a count other than <paramref name="chunks"/> fails the read.
    /// </summary>
    /// <param name="stored">The stored object, disposed with the content.</param>
    /// <param name="chunks">The chunks its row records.</param>
    /// <param name="open">Opens chunk n's stored bytes, or answers none where it does not open.</param>
    internal sealed class Content(Stream stored, int chunks, Func<int, byte[], CancellationToken, Task<byte[]?>> open) : Stream
    {
        private readonly byte[] header = new byte[FrameHeader];

        private byte[] current = [];

        private int offset;

        private int next;

        private bool ended;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>Whether a chunk did not open under its binding, or the object was not one this attachment's row describes.</summary>
        internal bool Refused { get; private set; }

        /// <summary>Opens the first chunk now, so a refusal is known before anything is answered; false where it did not open.</summary>
        internal async Task<bool> PrimeAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (chunks > 0 && offset == current.Length)
                {
                    await NextAsync(cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            while (offset == current.Length)
            {
                if (next == chunks)
                {
                    await EndAsync(cancellationToken).ConfigureAwait(false);
                    return 0;
                }

                await NextAsync(cancellationToken).ConfigureAwait(false);
            }

            int count = Math.Min(buffer.Length, current.Length - offset);
            current.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush()
        {
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

        public override async ValueTask DisposeAsync()
        {
            await stored.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stored.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task NextAsync(CancellationToken cancellationToken)
        {
            if (await FillAsync(stored, header, cancellationToken).ConfigureAwait(false) != FrameHeader)
            {
                throw Fail();
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length is <= 0 or > MostFrame)
            {
                throw Fail();
            }

            byte[] frame = new byte[length];
            if (await FillAsync(stored, frame, cancellationToken).ConfigureAwait(false) != length)
            {
                throw Fail();
            }

            current = await open(next, frame, cancellationToken).ConfigureAwait(false) ?? throw Fail();
            offset = 0;
            next++;
        }

        private async Task EndAsync(CancellationToken cancellationToken)
        {
            if (ended)
            {
                return;
            }

            ended = true;
            if (await FillAsync(stored, header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
            {
                throw Fail();
            }
        }

        private InvalidDataException Fail()
        {
            Refused = true;
            return new InvalidDataException("An attachment's stored chunks did not open as its row describes them.");
        }
    }
}