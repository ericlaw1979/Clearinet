using System;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Provides fixed-length, in-memory access to an existing byte array without copying it.
    /// </summary>
    /// <remarks>
    /// The caller retains ownership of the array. External modifications are visible but do not
    /// raise events or update change tracking. Clone the array first if isolation is required.
    /// Instances are not thread-safe.
    /// </remarks>
    public sealed class StaticByteProvider : IByteProvider
    {
        readonly byte[] _data;
        readonly bool _readOnly;
        bool _hasChanges;

        /// <summary>
        /// Wraps an array without allocating another data buffer.
        /// </summary>
        /// <param name="data">The backing array, which must not be null.</param>
        /// <param name="readOnly">Whether writes through this provider are prohibited.</param>
        public StaticByteProvider(byte[] data, bool readOnly = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _data = data;
            _readOnly = readOnly;
        }

        /// <summary>Gets whether writes through this provider are prohibited.</summary>
        public bool ReadOnly { get { return _readOnly; } }

        /// <inheritdoc />
        public long Length { get { return _data.Length; } }

        /// <inheritdoc />
        public event EventHandler Changed;

        /// <summary>Never raised because the length is fixed.</summary>
#pragma warning disable CS0067
        public event EventHandler LengthChanged;
#pragma warning restore CS0067

        /// <inheritdoc />
        public byte ReadByte(long index)
        {
            return _data[index];
        }

        /// <inheritdoc />
        public void WriteByte(long index, byte value)
        {
            if (_readOnly)
                throw new NotSupportedException("The provider is read-only.");

            _data[index] = value;
            _hasChanges = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public bool HasChanges() { return _hasChanges; }

        /// <summary>
        /// Clears change tracking. Writes already modify the caller's array; no data is persisted.
        /// </summary>
        public void ApplyChanges() { _hasChanges = false; }

        /// <inheritdoc />
        public bool SupportsWriteByte() { return !_readOnly; }

        /// <inheritdoc />
        public bool SupportsInsertBytes() { return false; }

        /// <inheritdoc />
        public bool SupportsDeleteBytes() { return false; }

        /// <summary>Not supported because the length is fixed.</summary>
        public void InsertBytes(long index, byte[] bs)
        {
            throw new NotSupportedException("The provider has a fixed length.");
        }

        /// <summary>Not supported because the length is fixed.</summary>
        public void DeleteBytes(long index, long length)
        {
            throw new NotSupportedException("The provider has a fixed length.");
        }
    }
}
