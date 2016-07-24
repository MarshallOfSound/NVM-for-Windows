using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Security;
using Microsoft.Win32.SafeHandles;


namespace SevenZipLib
{
    public enum ArchiveFormat : byte
    {
        Unkown = 0x00,

        Zip = 0x01,
        BZip2 = 0x02,
        Rar = 0x03,
        Arj = 0x04,
        Z = 0x05,
        Lzh = 0x06,
        _7z = 0x07,
        Cab = 0x08,
        Nsis = 0x09,
        lzma = 0x0A,
        lzma86 = 0x0B,
        xz = 0x0C,
        ppmd = 0xD,

        APM = 0xD4,
        Mslz = 0xD5,
        Flv = 0xD6,
        Swf = 0xD7,
        Swfc = 0xD8,
        Ntfs = 0xD9,
        Fat = 0xDA,
        Mbr = 0xDB,
        Vhd = 0xDC,
        Pe = 0xDD,
        Elf = 0xDE,
        Mach_O = 0xDF,
        Udf = 0xE0,
        Xar = 0xE1,
        Mub = 0xE2,
        Hfs = 0xE3,
        Dmg = 0xE4,
        Compound = 0xE5,
        Wim = 0xE6,
        Iso = 0xE7,
        Bkf = 0xE8,
        Chm = 0xE9,
        Split = 0xEA,
        Rpm = 0xEB,
        Deb = 0xEC,
        Cpio = 0xED,
        Tar = 0xEE,
        GZip = 0xEF,
    }

    public struct ArchiveProperty : IEquatable<ArchiveProperty>, IEqualityComparer<ArchiveProperty>
    {
        public string PropertyName { get; internal set; }
        public ItemPropId Property { get; internal set; }
        public object Value { get; internal set; }

        private static readonly Dictionary<ItemPropId, string> _propertyNames =
            new Dictionary<ItemPropId, string>() {
                { ItemPropId.Path, "Path" },
                { ItemPropId.Name, "Name" },
                { ItemPropId.IsDirectory, "Folder" },
                { ItemPropId.Size, "Size" },
                { ItemPropId.PackedSize, "Packed Size" },
                { ItemPropId.Attributes, "Attributes" },
                { ItemPropId.CreationTime, "Created" },
                { ItemPropId.LastAccessTime, "Accessed" },
                { ItemPropId.LastWriteTime, "Modified" },
                { ItemPropId.Solid, "Solid" },
                { ItemPropId.Commented, "Commented" },
                { ItemPropId.Encrypted, "Encrypted" },
                { ItemPropId.SplitBefore, "Split Before" },
                { ItemPropId.SplitAfter, "Split After" },
                { ItemPropId.DictionarySize, "Dictionary Size" },
                { ItemPropId.CRC, "CRC" },
                { ItemPropId.Type, "Type" },
                { ItemPropId.IsAnti, "Anti" },
                { ItemPropId.Method, "Method" },
                { ItemPropId.HostOS, "Host OS" },
                { ItemPropId.FileSystem, "File System" },
                { ItemPropId.User, "User" },
                { ItemPropId.Group, "Group" },
                { ItemPropId.Block, "Block" },
                { ItemPropId.Comment, "Comment" },
                { ItemPropId.Position, "Position" },
                { ItemPropId.Prefix, "Prefix" },
                { ItemPropId.NumSubDirs, "Folders" },
                { ItemPropId.NumSubFiles, "Files" },
                { ItemPropId.UnpackVersion, "Version" },
                { ItemPropId.Volume, "Volume" },
                { ItemPropId.IsVolume, "Multivolume" },
                { ItemPropId.Offset, "Offset" },
                { ItemPropId.Links, "Links" },
                { ItemPropId.NumBlocks, "Blocks" },
                { ItemPropId.NumVolumes, "Volumes" },
                { ItemPropId.Bit64, "64-bit" },
                { ItemPropId.BigEndian, "Big-endian" },
                { ItemPropId.Cpu, "CPU" },
                { ItemPropId.PhysicalSize, "Physical Size" },
                { ItemPropId.HeadersSize, "Headers Size" },
                { ItemPropId.Checksum, "Checksum" },
                { ItemPropId.Characts, "Characteristics" },
                { ItemPropId.Va, "Virtual Address" },
                { ItemPropId.Id, "ID" },
                { ItemPropId.ShortName, "Short Name" },
                { ItemPropId.CreatorApp, "Creator Application"},
                { ItemPropId.SectorSize, "Sector Size" },
                { ItemPropId.PosixAttrib, "Mode" },
                { ItemPropId.Link, "Link" },
                { ItemPropId.TotalSize, "Total Size" },
                { ItemPropId.FreeSpace, "Free Space" },
                { ItemPropId.ClusterSize, "Cluster Size" },
                { ItemPropId.VolumeName, "Label" }
            };

        internal ArchiveProperty(ItemPropId property, string name, object value)
            : this()
        {
            this.Property = property;
            this.Value = value;
            if (name != null)
            {
                this.PropertyName = name;
            }
            else if (_propertyNames.ContainsKey(property))
            {
                this.PropertyName = _propertyNames[property];
            }
        }

        public override string ToString()
        {
            return string.Format("{0} = {1}", PropertyName ?? Property.ToString(), Value);
        }

        #region IEquatable<ArchiveProperty> Members

        public bool Equals(ArchiveProperty other)
        {
            return this.Property == other.Property &&
                this.Value == other.Value;
        }

        #endregion

        #region IEqualityComparer<ArchiveProperty> Members

        public bool Equals(ArchiveProperty x, ArchiveProperty y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(ArchiveProperty obj)
        {
            return Property.GetHashCode() ^ Value.GetHashCode();
        }

        #endregion
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000000050000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IProgress
    {
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600100000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveOpenCallback
    {
        // ref ulong replaced with IntPtr because handlers ofter pass null value
        // read actual value with Marshal.ReadInt64
        void SetTotal(
          IntPtr files,  // [In] ref ulong files, can use 'ulong* files' but it is unsafe
          IntPtr bytes); // [In] ref ulong bytes
        void SetCompleted(
          IntPtr files,  // [In] ref ulong files
          IntPtr bytes); // [In] ref ulong bytes
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000500100000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICryptoGetTextPassword
    {
        [PreserveSig]
        int CryptoGetTextPassword(
          [MarshalAs(UnmanagedType.BStr)] out string password);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000500110000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICryptoGetTextPassword2
    {
        void CryptoGetTextPassword2(
          [MarshalAs(UnmanagedType.Bool)] out bool passwordIsDefined,
          [MarshalAs(UnmanagedType.BStr)] out string password);
    }

    internal enum AskMode : int
    {
        Extract = 0,
        Test,
        Skip
    }

    internal enum OperationResult : int
    {
        OK = 0,
        UnSupportedMethod,
        DataError,
        CRCError
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600200000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveExtractCallback //: IProgress
    {
        // IProgress
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);

        // IArchiveExtractCallback
        [PreserveSig]
        int GetStream(
          uint index,
          [MarshalAs(UnmanagedType.Interface)] out ISequentialOutStream outStream,
          AskMode askExtractMode);
        // GetStream OUT: S_OK - OK, S_FALSE - skip this file

        void PrepareOperation(AskMode askExtractMode);

        [PreserveSig]
        int SetOperationResult(OperationResult operationResult);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600300000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveOpenVolumeCallback
    {
        [PreserveSig]
        int GetProperty(
          ItemPropId propID, // PROPID
          ref PropVariant value); // PROPVARIANT

        [PreserveSig]
        int GetStream(
          [MarshalAs(UnmanagedType.LPWStr)] string name,
          [MarshalAs(UnmanagedType.Interface)] out IInStream inStream);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600400000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInArchiveGetStream
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetStream(uint index);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600500000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveOpenSetSubArchiveName
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        void SetSubArchiveName(
            [MarshalAs(UnmanagedType.LPWStr)] string name);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300010000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IX
    {
        [PreserveSig]
        int Read(
          [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size,
          IntPtr processedSize); // ref uint processedSize}
    }

    [ComImport]
    //     23170F69-40C1-278A-0000-00yy00xx0000
    [Guid("23170F69-40C1-278A-0000-000300010000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISequentialInStream
    {
        //[PreserveSig]
        //int Read(
        //  [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        //  uint size,
        //  IntPtr processedSize); // ref uint processedSize

        uint Read(
          [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size);

        /*
        Out: if size != 0, return_value = S_OK and (*processedSize == 0),
          then there are no more bytes in stream.
        if (size > 0) && there are bytes in stream, 
        this function must read at least 1 byte.
        This function is allowed to read less than number of remaining bytes in stream.
        You must call Read function in loop, if you need exact amount of data
        */
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300020000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISequentialOutStream
    {
        [PreserveSig]
        int Write(
          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size,
          IntPtr processedSize); // ref uint processedSize
        /*
        if (size > 0) this function must write at least 1 byte.
        This function is allowed to write less than "size".
        You must call Write function in loop, if you need to write exact amount of data
        */
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300030000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInStream //: ISequentialInStream
    {
        //[PreserveSig]
        //int Read(
        //  [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
        //  uint size,
        //  IntPtr processedSize); // ref uint processedSize

        // ISequentialInStream
        uint Read(
          [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size);

        // IInStream
        //[PreserveSig]
        void Seek(
          long offset,
          uint seekOrigin,
          IntPtr newPosition); // ref long newPosition
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300040000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOutStream //: ISequentialOutStream
    {
        // ISequentialOutStream
        [PreserveSig]
        int Write(
          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size,
          IntPtr processedSize); // ref uint processedSize

        // IOutStream
        //[PreserveSig]
        void Seek(
          long offset,
          uint seekOrigin,
          IntPtr newPosition); // ref long newPosition

        [PreserveSig]
        int SetSize(long newSize);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300060000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IStreamGetSize
    {
        ulong GetSize();
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300070000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOutStreamFlush
    {
        void Flush();
    }

    public enum ItemPropId : uint
    {
        NoProperty = 0,
        MainSubfile = 1,
        HandlerItemIndex = 2,
        Path,
        Name,
        Extension,
        IsDirectory,
        Size,
        PackedSize,
        Attributes,
        CreationTime,
        LastAccessTime,
        LastWriteTime,
        Solid,
        Commented,
        Encrypted,
        SplitBefore,
        SplitAfter,
        DictionarySize,
        CRC,
        Type,
        IsAnti,
        Method,
        HostOS,
        FileSystem,
        User,
        Group,
        Block,
        Comment,
        Position,
        Prefix,
        NumSubDirs,
        NumSubFiles,
        UnpackVersion,
        Volume,
        IsVolume,
        Offset,
        Links,
        NumBlocks,
        NumVolumes,
        TimeType,
        Bit64,
        BigEndian,
        Cpu,
        PhysicalSize,
        HeadersSize,
        Checksum,
        Characts,
        Va,
        Id,
        ShortName,
        CreatorApp,
        SectorSize,
        PosixAttrib,
        Link,

        TotalSize = 0x1100,
        FreeSpace,
        ClusterSize,
        VolumeName,

        LocalName = 0x1200,
        Provider,

        UserDefined = 0x10000
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600600000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    //[AutomationProxy(true)]
    internal interface IInArchive
    {
        [PreserveSig]
        int Open(
          IInStream stream,
            /*[MarshalAs(UnmanagedType.U8)]*/ [In] ref ulong maxCheckStartPosition,
            [MarshalAs(UnmanagedType.Interface)] IArchiveOpenCallback openArchiveCallback);
        [PreserveSig]
        int Close();
        //void GetNumberOfItems([In] ref uint numItem);
        uint GetNumberOfItems();

        void GetProperty(
          uint index,
          ItemPropId propID, // PROPID
          ref PropVariant value); // PROPVARIANT

        [PreserveSig]
        int Extract(
          [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[] indices, //[In] ref uint indices,
          uint numItems,
          int testMode,
          [MarshalAs(UnmanagedType.Interface)] IArchiveExtractCallback extractCallback);
        // indices must be sorted 
        // numItems = 0xFFFFFFFF means all files
        // testMode != 0 means "test files operation"

        void GetArchiveProperty(
          ItemPropId propID, // PROPID
          ref PropVariant value); // PROPVARIANT

        //void GetNumberOfProperties([In] ref uint numProperties);
        uint GetNumberOfProperties();
        void GetPropertyInfo(
          uint index,
          [MarshalAs(UnmanagedType.BStr)] out string name,
          out ItemPropId propID, // PROPID
          out ushort varType); //VARTYPE

        //void GetNumberOfArchiveProperties([In] ref uint numProperties);
        uint GetNumberOfArchiveProperties();
        void GetArchivePropertyInfo(
          uint index,
          [MarshalAs(UnmanagedType.BStr)] out string name,
          out ItemPropId propID, // PROPID
          out ushort varType); //VARTYPE
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600800000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveUpdateCallback // : IProgress
    {
        // IProgress
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);

        // IArchiveUpdateCallback
        void GetUpdateItemInfo(int index,
          out int newData, // 1 - new data, 0 - old data
          out int newProperties, // 1 - new properties, 0 - old properties
          out uint indexInArchive); // -1 if there is no in archive, or if doesn't matter

        void GetProperty(
          int index,
          ItemPropId propID, // PROPID
          IntPtr value); // PROPVARIANT

        void GetStream(int index, out ISequentialInStream inStream);

        void SetOperationResult(int operationResult);
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600820000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IArchiveUpdateCallback2 // : IArchiveUpdateCallback
    {
        // IProgress
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);

        // IArchiveUpdateCallback
        void GetUpdateItemInfo(int index,
          out int newData, // 1 - new data, 0 - old data
          out int newProperties, // 1 - new properties, 0 - old properties
          out uint indexInArchive); // -1 if there is no in archive, or if doesn't matter

        void GetProperty(
          int index,
          ItemPropId propID, // PROPID
          IntPtr value); // PROPVARIANT

        void GetStream(int index, out ISequentialInStream inStream);

        void SetOperationResult(int operationResult);

        // IArchiveUpdateCallback2
        void GetVolumeSize(int index, out ulong size);
        void GetVolumeStream(int index, out ISequentialOutStream volumeStream);
    }

    internal enum FileTimeType : int
    {
        Windows,
        UNIX,
        DOS
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600A00000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOutArchive
    {
        void UpdateItems(
          ISequentialOutStream outStream,
          int numItems,
          IArchiveUpdateCallback updateCallback);

        FileTimeType GetFileTimeType();
    }

    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600030000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISetProperties
    {
        void SetProperties(
          [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 2)] string[] names,
          IntPtr values,
          int numProperties);
    }

    internal enum ArchivePropId : uint
    {
        Name = 0,
        ClassID,
        Extension,
        AddExtension,
        Update,
        KeepName,
        StartSignature,
        FinishSignature,
        Associate
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateObjectDelegate(
      [In] ref Guid classID,
      [In] ref Guid interfaceID,
        //out IntPtr outObject);
      [MarshalAs(UnmanagedType.Interface)] out object outObject);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetHandlerPropertyDelegate(
      ArchivePropId propID,
      ref PropVariant value); // PROPVARIANT

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetNumberOfFormatsDelegate(out uint numFormats);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetHandlerProperty2Delegate(
      uint formatIndex,
      ArchivePropId propID,
      ref PropVariant value); // PROPVARIANT

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetNumberOfMethodsDelegate(out uint numCodecs);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetMethodPropertyDelegate(
      uint codecIndex,
      ArchivePropId propID,
      ref PropVariant value); // PROPVARIANT

    internal static class HRESULT
    {
        public const int S_OK = unchecked((int)0x00000000);
        public const int S_FALSE = unchecked((int)0x00000001);
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int STG_E_INVALIDFUNCTION = unchecked((int)0x80030001);
        public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        public const int E_INVALIDARG = unchecked((int)0x80070057);
    }

    internal sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeLibraryHandle() : base(true) { }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>Release library handle</summary>
        /// <returns>true if the handle was released</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            return FreeLibrary(handle);
        }
    }

    internal class StreamWrapper : IDisposable
    {
        protected Stream _baseStream;
        protected bool _disposeBaseStream;
        protected bool _disposed;

        protected StreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
            _disposeBaseStream = true;
        }

        protected StreamWrapper(Stream baseStream, bool disposeBaseStream)
        {
            _baseStream = baseStream;
            _disposeBaseStream = disposeBaseStream;
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }

        public virtual void Seek(long offset, uint seekOrigin, IntPtr newPosition)
        {
            EnsureNotDisposed();

            long position = (uint)_baseStream.Seek(offset, (SeekOrigin)seekOrigin);
            if (newPosition != IntPtr.Zero)
            {
                Marshal.WriteInt64(newPosition, position);
            }
        }

        public Stream BaseStream
        {
            get { return _baseStream; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_baseStream != null)
                    {
                        if (_disposeBaseStream)
                        {
                            _baseStream.Dispose();
                        }
                        _baseStream = null;
                    }
                }

                _disposed = true;
            }
        }

        ~StreamWrapper()
        {
            Dispose(false);
        }
    }

    internal class InStreamWrapper : StreamWrapper, ISequentialInStream, IInStream// IStreamGetSize
    {
        public InStreamWrapper(Stream baseStream)
            : base(baseStream)
        {
        }

        public uint Read(byte[] data, uint size)
        {
            EnsureNotDisposed();

            return (uint)_baseStream.Read(data, 0, (int)size);
        }

        public ulong GetSize()
        {
            EnsureNotDisposed();

            return (ulong)_baseStream.Length;
        }
    }

    internal class OutStreamWrapper : StreamWrapper, ISequentialOutStream, IOutStream
    {
        public OutStreamWrapper(Stream baseStream)
            : base(baseStream)
        {
        }

        public OutStreamWrapper(Stream baseStream, bool disposeBaseStream)
            : base(baseStream, disposeBaseStream)
        {
        }

        public int SetSize(long newSize)
        {
            EnsureNotDisposed();

            _baseStream.SetLength(newSize);
            return HRESULT.S_OK;
        }

        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            EnsureNotDisposed();

            _baseStream.Write(data, 0, (int)size);
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int)size);
            }
            return HRESULT.S_OK;
        }
    }

    internal class InSubStream : Stream
    {
        private IInStream _stream;

        public InSubStream(IInStream stream)
        {
            _stream = stream;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get
            {
                long currentPosition = Position;
                long length = Seek(0, SeekOrigin.End);
                Position = currentPosition;
                return length;
            }
        }

        public override long Position
        {
            get
            {
                return Seek(0, SeekOrigin.Current);
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset == 0)
            {
                return (int)_stream.Read(buffer, (uint)count);
            }
            else
            {
                byte[] data = new byte[count];
                int length = (int)_stream.Read(data, (uint)count);
                Array.Copy(data, 0, buffer, offset, length);
                return length;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            IntPtr newPosition = IntPtr.Zero;
            try
            {
                newPosition = Marshal.AllocCoTaskMem(sizeof(Int64));
                _stream.Seek(offset, (uint)origin, newPosition);
                return Marshal.ReadInt64(newPosition);
            }
            finally
            {
                Marshal.FreeCoTaskMem(newPosition);
            }
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("The stream is only readable.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("The stream is only readable.");
        }
    }

    internal class DummyOutStream : Stream
    {
        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
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
        }
    }

    public class ArchiveHandler
    {
        public ArchiveFormat Format { get; internal set; }
        public int FormatIndex { get; internal set; }
        public string Name { get; internal set; }
        public Guid ClassID { get; internal set; }
        public string[] Extensions { get; internal set; }
        public byte[] StartSignature { get; internal set; }

        internal string[] AddExtensions { get; set; }
        internal bool Update { get; set; }
        internal bool KeepName { get; set; }

        internal ArchiveHandler()
        {
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(Name);
            if (Extensions != null && Extensions.Length > 0)
            {
                sb.Append(" - ");
                sb.Append(string.Join(" ", Extensions));
            }
            if (AddExtensions != null && AddExtensions.Length > 0)
            {
                sb.Append(" (");
                sb.Append(string.Join(" ", AddExtensions));
                sb.Append(")");
            }

            return sb.ToString();
        }
    }
}
