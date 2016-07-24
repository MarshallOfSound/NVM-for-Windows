using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security;
using System.Reflection;
using System.ComponentModel;


namespace SevenZipLib
{
    [Flags]
    public enum ExtractOptions
    {
        None = 0,
        NoAbortOnFailure = 1,
        OverwriteExistingFiles = 2,
        SkipExistingFiles = 4,
        RenameInvalidEntries = 8
    }

    public class SevenZipArchive : ICollection<ArchiveEntry>, IEnumerable<ArchiveEntry>, IEnumerable, IDisposable
    {
        #region Field Members

        /******************* NOTE *********************/
        /* Update Swap method when adding new members */
        /**********************************************/

        private bool _disposed;
        private string _password;
        private string _fileName;
        private string _defaultName;
        private long _packedSize;
        private long? _unPackedSize;
        private bool _isEncrypted;
        private InStreamWrapper _stream;
        private ArchiveFormat _format;
        private int _formatIndex;
        private SafeLibraryHandle _library;
        internal IInArchive _archive;
        private ReadOnlyCollection<string> _volumes;
        private ReadOnlyCollection<ArchiveProperty> _properties;
        private SortedDictionary<string, ArchiveEntry> _items;
        private ArchiveEntry[] _orderedItems;
        private ArchiveOpenCallback _openCallback;
        private SevenZipArchive _parent;
        private bool _hasSubArchive;

        private static ReadOnlyCollection<ArchiveHandler> _handlers;
        private static Dictionary<byte, int> _handlersDictionary;
        public static readonly char InvalidCharReplacement = '_';

        #endregion

        #region Events

        public event EventHandler<FileExtractedEventArgs> FileExtracted;
        public event EventHandler<FileExtractionFailedEventArgs> FileExtractionFailed;
        public event EventHandler<ExistingFileSkippedEventArgs> ExistingFileSkipped;
        public event EventHandler<FileCheckedEventArgs> FileChecked;
        public event EventHandler<FileCheckFailedEventArgs> FileCheckFailed;
        public event EventHandler<FileExistsEventArgs> FileExists;
        public event EventHandler<PasswordRequestedEventArgs> PasswordRequested;

        #endregion

        #region Constructors

        public SevenZipArchive(string fileName)
            : this(fileName, ArchiveFormat.Unkown, null, null)
        {
        }

        public SevenZipArchive(string fileName, ArchiveFormat format)
            : this(fileName, format, null, null)
        {
        }

        public SevenZipArchive(string fileName, ArchiveFormat format, string password)
            : this(fileName, format, password, null)
        {
        }

        public SevenZipArchive(string fileName, ArchiveFormat format, string password, IComparer<string> comparer)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("fileName");
            }
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("The specified file does not exist.", fileName);
            }

            Init(false, fileName, null, password, format, comparer, new List<SevenZipArchive>() { this });
        }

        public SevenZipArchive(Stream stream)
            : this(stream, ArchiveFormat.Unkown, null, null)
        {
        }
        
        public SevenZipArchive(Stream stream, ArchiveFormat format)
            : this(stream, format, null, null)
        {
        }

        public SevenZipArchive(Stream stream, ArchiveFormat format, string password)
            : this(stream, format, password, null)
        {
        }

        public SevenZipArchive(Stream stream, ArchiveFormat format, string password, IComparer<string> comparer)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new ArgumentException("The specified stream should be seekable and readable.", "stream");
            }

            Init(true, null, stream, password, format, comparer, new List<SevenZipArchive>() { this });
        }

        private SevenZipArchive(bool streamMode, string fileName, Stream stream, ArchiveFormat format, string password, IComparer<string> comparer, List<SevenZipArchive> archives)
        {
            Init(streamMode, fileName, stream, password, format, comparer, archives);
        }

        #endregion

        #region Public Properties

        public string FileName
        {
            get { return _fileName; }
        }

        public ArchiveFormat Format
        {
            get { return _format; }
        }

        public ReadOnlyCollection<string> Volumes
        {
            get { return _volumes; }
        }

        public ReadOnlyCollection<ArchiveProperty> Properties
        {
            get { return _properties; }
        }

        public long PackedSize
        {
            get { return _packedSize; }
        }
        
        public long UnPackedSize
        {
            get
            {
                if (!_unPackedSize.HasValue)
                {
                    EnsureNotDisposed();

                    using (var extractCallback = new ArchiveExtractCallback(_password))
                    {
                        _archive.Extract(null, uint.MaxValue, 1, extractCallback);
                        _unPackedSize = extractCallback.UnPackedSize;
                    }
                }
                return _unPackedSize.Value;
            }
        }

        public SevenZipArchive Parent
        {
            get { return _parent; }
        }

        public string Password
        {
            get { return _password; }
        }

        public bool IsEncrypted
        {
            get { return _isEncrypted; }
        }

        public bool HasSubArchive
        {
            get { return _hasSubArchive; }
        }

        public bool IsClosed
        {
            get { return _disposed; }
        }
        
        #endregion

        #region Public Methods

        public static ReadOnlyCollection<ArchiveHandler> GetArchiveHandlers()
        {
            if (_handlers != null)
            {
                return _handlers;
            }
            else
            {
                using (SafeLibraryHandle library = Load7ZipLibrary())
                {
                    return GetHandlers(library);
                }
            }
        }

        public static string CleanFileName(string entry)
        {
            if (entry == null)
            {
                return null;
            }
            return entry.Replace('/', '\\').Trim('\\');
        }

        public bool Contains(string entry)
        {
            EnsureNotDisposed();
            return _items.ContainsKey(entry);
        }

        public void CopyTo(string[] array, int startIndex)
        {
            EnsureNotDisposed();
            _items.Keys.CopyTo(array, startIndex);
        }

        public void ExtractFile(string entry, Stream stream)
        {
            ExtractFile(entry, stream, ExtractOptions.None, null);
        }

        public void ExtractFile(string entry, Stream stream, ExtractOptions options)
        {
            ExtractFile(entry, stream, options, null);
        }

        public void ExtractFile(string entry, Stream stream, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            ValidateFileNameForExtraction(entry, false, true);
            ValidateOutStream(stream);
            ValidateExtractOptions(options);

            uint[] indices = new uint[] { _items[entry].Index };
            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            entriesDictionary[_items[entry].Index] = _items[entry];
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password, stream, options))
            {
                int result = _archive.Extract(indices, (uint)indices.Length, 0, extractCallback);
                VerifyExtractCallbackResult(extractCallback, result);
            }
        }

        public void ExtractFile(string entry, string directory)
        {
            ExtractFile(entry, directory, ExtractOptions.None, null);
        }

        public void ExtractFile(string entry, string directory, ExtractOptions options)
        {
            ExtractFile(entry, directory, options, null);
        }

        public void ExtractFile(string entry, string directory, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            ValidateFileNameForExtraction(entry);
            ValidateTargetDirectory(directory);
            ValidateExtractOptions(options);

            uint[] indices = new uint[] { _items[entry].Index };
            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            entriesDictionary[_items[entry].Index] = _items[entry];

            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password, directory, options))
            {
                int result = _archive.Extract(indices, (uint)indices.Length, 0, extractCallback);
                VerifyExtractCallbackResult(extractCallback, result);
            }
        }

        public void ExtractFiles(IEnumerable<string> entries, string directory)
        {
            ExtractFiles(entries, directory, ExtractOptions.None, null);
        }

        public void ExtractFiles(IEnumerable<string> entries, string directory, ExtractOptions options)
        {
            ExtractFiles(entries, directory, options, null);
        }

        public void ExtractFiles(IEnumerable<string> entries, string directory, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            ValidateFileNamesForExtraction(entries);
            ValidateTargetDirectory(directory);
            ValidateExtractOptions(options);

            uint[] indices = entries.Select(x => _items[x].Index).Distinct().OrderBy(x => x).ToArray();
            if (indices.Length == 0)
            {
                return;
            }
            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            foreach (string fileName in entries)
            {
                entriesDictionary[_items[fileName].Index] = _items[fileName];
            }
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password, directory, options))
            {
                int result = _archive.Extract(indices, (uint)indices.Length, 0, extractCallback);
                VerifyExtractCallbackResult(extractCallback, result);
            }
        }
        
        public void ExtractAll(string directory)
        {
            ExtractAll(directory, ExtractOptions.None, null);
        }

        public void ExtractAll(string directory, ExtractOptions options)
        {
            ExtractAll(directory, options, null);
        }

        public void ExtractAll(string directory, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            ValidateTargetDirectory(directory);
            ValidateExtractOptions(options);

            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            foreach (var entry in _items)
            {
                entriesDictionary[entry.Value.Index] = entry.Value;
            }
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password, directory, options))
            {
                int result = _archive.Extract(null, uint.MaxValue, 0, extractCallback);
                if (_unPackedSize == null)
                {
                    _unPackedSize = extractCallback.UnPackedSize;
                }
                else
                {
                    Debug.Assert(_unPackedSize == extractCallback.UnPackedSize, "Inconsistency found with _unPackedSize.");
                }
                VerifyExtractCallbackResult(extractCallback, result);
            }
        }

        public bool CheckFile(string entry)
        {
            return CheckFile(entry, null);
        }

        public bool CheckFile(string entry, string password)
        {
            EnsureNotDisposed();
            ValidateFileNameForExtraction(entry, false);

            uint[] indices = new uint[] { _items[entry].Index };
            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            entriesDictionary[_items[entry].Index] = _items[entry];

            bool checksOK = true;
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password))
            {
                EventHandler<FileCheckFailedEventArgs> handler = (s, e) => { checksOK = false; };
                try
                {
                    FileCheckFailed += handler;
                    int result = _archive.Extract(indices, (uint)indices.Length, 1, extractCallback);
                    VerifyExtractCallbackResult(extractCallback, result, false);
                }
                finally
                {
                    FileCheckFailed -= handler;
                }
            }
            return checksOK;
        }

        public bool CheckFiles(IEnumerable<string> entries)
        {
            return CheckFiles(entries, null);
        }

        public bool CheckFiles(IEnumerable<string> entries, string password)
        {
            EnsureNotDisposed();
            ValidateFileNamesForExtraction(entries, false);

            uint[] indices = entries.Select(x => _items[x].Index).Distinct().OrderBy(x => x).ToArray();
            if (indices.Length == 0)
            {
                return true;
            }
            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            foreach (string fileName in entries)
            {
                entriesDictionary[_items[fileName].Index] = _items[fileName];
            }

            bool checksOK = true;
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password))
            {
                EventHandler<FileCheckFailedEventArgs> handler = (s, e) => { checksOK = false; };
                try
                {
                    FileCheckFailed += handler;
                    int result = _archive.Extract(indices, (uint)indices.Length, 1, extractCallback);
                    VerifyExtractCallbackResult(extractCallback, result, false);
                }
                finally
                {
                    FileCheckFailed -= handler;
                }
            }
            return checksOK;
        }

        public bool CheckAll()
        {
            return CheckAll(null);
        }

        public bool CheckAll(string password)
        {
            EnsureNotDisposed();

            Dictionary<uint, ArchiveEntry> entriesDictionary = new Dictionary<uint, ArchiveEntry>();
            foreach (var entry in _items)
            {
                entriesDictionary[entry.Value.Index] = entry.Value;
            }

            bool checksOK = true;
            using (var extractCallback = new ArchiveExtractCallback(this, entriesDictionary, password))
            {
                EventHandler<FileCheckFailedEventArgs> handler = (s, e) => { checksOK = false; };
                try
                {
                    FileCheckFailed += handler;
                    int result = _archive.Extract(null, uint.MaxValue, 1, extractCallback);
                    if (_unPackedSize == null)
                    {
                        _unPackedSize = extractCallback.UnPackedSize;
                    }
                    else
                    {
                        Debug.Assert(_unPackedSize == extractCallback.UnPackedSize, "Inconsistency found with _unPackedSize.");
                    }
                    VerifyExtractCallbackResult(extractCallback, result, false);
                }
                finally
                {
                    FileCheckFailed -= handler;
                }
            }
            return checksOK;
        }

        #endregion

        #region Protected Methods

        protected internal virtual void OnFileExtracted(FileExtractedEventArgs e)
        {
            if (FileExtracted != null)
            {
                FileExtracted(this, e);
            }
        }

        protected internal virtual void OnExistingFileSkipped(ExistingFileSkippedEventArgs e)
        {
            if (ExistingFileSkipped != null)
            {
                ExistingFileSkipped(this, e);
            }
        }

        protected internal virtual void OnFileExtractionFailed(FileExtractionFailedEventArgs e)
        {
            if (FileExtractionFailed != null)
            {
                FileExtractionFailed(this, e);
            }
        }

        protected internal virtual void OnFileChecked(FileCheckedEventArgs e)
        {
            if (FileChecked != null)
            {
                FileChecked(this, e);
            }
        }

        protected internal virtual void OnFileCheckFailed(FileCheckFailedEventArgs e)
        {
            if (FileCheckFailed != null)
            {
                FileCheckFailed(this, e);
            }
        }

        protected internal virtual void OnFileExists(FileExistsEventArgs e)
        {
            if (FileExists != null)
            {
                FileExists(this, e);
            }
        }

        protected internal virtual void OnPasswordRequested(PasswordRequestedEventArgs e)
        {
            if (PasswordRequested != null)
            {
                PasswordRequested(this, e);
            }
        }

        #endregion

        #region ICollection and IEnumerable Members

        #region ICollection<ArchiveEntry> Members

        void ICollection<ArchiveEntry>.Add(ArchiveEntry item)
        {
            throw new NotSupportedException();
        }

        void ICollection<ArchiveEntry>.Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(ArchiveEntry entry)
        {
            EnsureNotDisposed();
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }
            return _items.ContainsKey(entry.FileName);
        }

        public void CopyTo(ArchiveEntry[] array, int startIndex)
        {
            EnsureNotDisposed();
            _items.Values.CopyTo(array, startIndex);
        }

        public int Count
        {
            get
            {
                EnsureNotDisposed();
                return _items.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        bool ICollection<ArchiveEntry>.Remove(ArchiveEntry item)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region IEnumerable<ArchiveEntry> Members

        public IEnumerator<ArchiveEntry> GetEnumerator()
        {
            EnsureNotDisposed();
            return _items.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            EnsureNotDisposed();
            return _items.Values.GetEnumerator();
        }

        #endregion

        #region Indexers

        public ArchiveEntry this[string entry]
        {
            get
            {
                EnsureNotDisposed();
                return _items[entry];
            }
        }

        public ArchiveEntry this[int index]
        {
            get
            {
                EnsureNotDisposed();
                return _orderedItems[index];
            }
        }
        
        #endregion

        #endregion

        #region Private Methods

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeLibraryHandle LoadLibrary(
          [MarshalAs(UnmanagedType.LPTStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(
          SafeLibraryHandle hModule,
          [MarshalAs(UnmanagedType.LPStr)] string procName);

        private static string SevenZipLibraryPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    IntPtr.Size == 8 ? "7z64.dll" : "7z86.dll");
            }
        }

        private static SafeLibraryHandle Load7ZipLibrary()
        {
            SafeLibraryHandle handle = LoadLibrary(SevenZipLibraryPath);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to load 7-Zip library.");
            }

            return handle;
        }

        private static T CreateInterface<T>(SafeLibraryHandle library, Guid classId) where T : class
        {
            CreateObjectDelegate createObject =
              (CreateObjectDelegate)Marshal.GetDelegateForFunctionPointer(
              GetProcAddress(library, "CreateObject"), typeof(CreateObjectDelegate));

            if (createObject == null)
            {
                throw new SevenZipException("Unable to get the CreateObject delegate.");
            }

            object result = null;
            Guid interfaceId = typeof(T).GUID;

            if (createObject(ref classId, ref interfaceId, out result) != 0 || result as T == null)
            {
                throw new SevenZipException("Unable to create the interface: " + typeof(T).ToString());
            }

            return result as T;
        }

        private static IInArchive CreateInArchive(SafeLibraryHandle library, int formatIndex)
        {
            if (formatIndex < 0)
            {
                throw new ArgumentException("Invalid handler format index", "formatIndex");
            }
            var handlers = GetHandlers(library);
            return CreateInterface<IInArchive>(library, handlers[formatIndex].ClassID);
        }

        private static void ReleaseInArchive(IInArchive archive)
        {
            Marshal.ReleaseComObject(archive);
        }

        private static ReadOnlyCollection<ArchiveProperty> GetArchiveProperties(IInArchive archive, ArchiveFormat format)
        {
            uint numberOfProperties = archive.GetNumberOfArchiveProperties();
            List<ArchiveProperty> properties = new List<ArchiveProperty>((int)numberOfProperties);

            for (uint i = 0; i < numberOfProperties; i++)
            {
                try
                {
                    string name; ItemPropId propId; ushort varType;
                    archive.GetArchivePropertyInfo(i, out name, out propId, out varType);

                    PropVariant data = new PropVariant();
                    try
                    {
                        archive.GetArchiveProperty(propId, ref data);
                        properties.Add(new ArchiveProperty(propId, name, data.Value));
                    }
                    finally
                    {
                        data.Clear();
                    }
                }
                catch (Exception e)
                {
                    Debug.Fail(e.ToString());
                    continue;
                }
            }

            return properties.AsReadOnly();
        }

        private static T SafeCast<T>(PropVariant prop, T defaultValue)
        {
            object result;
            try
            {
                result = prop.Value;
            }
            catch
            {
                return defaultValue;
            }
            finally
            {
                prop.Clear();
            }
            if (result != null && result is T)
            {
                return (T)result;
            }
            return defaultValue;
        }

        private static void AddEntries(SevenZipArchive archive, string defaultName, IDictionary<string, ArchiveEntry> items)
        {
            uint numberOfEntries = archive._archive.GetNumberOfItems();

            for (uint i = 0, untitledCounter = 1; i < numberOfEntries; i++)
            {
                PropVariant data = new PropVariant();
                try
                {
                    bool isPathSet = false;
                    bool isUntitled = false;
                    string fileName = GetItemPath(archive._archive, defaultName, i, ref isPathSet);
                    if (fileName == null)
                    {
                        fileName = string.Format("[Untitled Entry{0}]", numberOfEntries > 1 ? (" " + untitledCounter++) : string.Empty);
                        isUntitled = true;
                    }

                    ArchiveEntry entry = new ArchiveEntry(archive, fileName, i);
                    DateTime invalidDate = DateTime.FromFileTime(0);

                    entry.IsUntitled = isUntitled;

                    archive._archive.GetProperty(i, ItemPropId.LastWriteTime, ref data);
                    entry.LastWriteTime = SafeCast<DateTime?>(data, null);
                    data.Clear();
                    if (entry.LastWriteTime == invalidDate)
                    {
                        entry.LastWriteTime = null;
                    }

                    archive._archive.GetProperty(i, ItemPropId.CreationTime, ref data);
                    entry.CreationTime = SafeCast<DateTime?>(data, null);
                    data.Clear();
                    if (entry.CreationTime == invalidDate)
                    {
                        entry.CreationTime = null;
                    }

                    archive._archive.GetProperty(i, ItemPropId.LastAccessTime, ref data);
                    entry.LastAccessTime = SafeCast<DateTime?>(data, null);
                    data.Clear();
                    if (entry.LastAccessTime == invalidDate)
                    {
                        entry.LastAccessTime = null;
                    }

                    archive._archive.GetProperty(i, ItemPropId.Size, ref data);
                    entry.Size = SafeCast<ulong>(data, 0);
                    data.Clear();

                    archive._archive.GetProperty(i, ItemPropId.Attributes, ref data);
                    entry.Attributes = SafeCast<FileAttributes?>(data, null);
                    data.Clear();

                    archive._archive.GetProperty(i, ItemPropId.IsDirectory, ref data);
                    entry.IsDirectory = SafeCast(data, false);
                    data.Clear();

                    archive._archive.GetProperty(i, ItemPropId.Encrypted, ref data);
                    entry.IsEncrypted = SafeCast(data, false);
                    data.Clear();

                    archive._archive.GetProperty(i, ItemPropId.CRC, ref data);
                    entry.Crc = SafeCast<uint>(data, 0);
                    data.Clear();

                    archive._archive.GetProperty(i, ItemPropId.Comment, ref data);
                    entry.Comment = SafeCast<string>(data, null);
                    data.Clear();

                    items.Add(entry.FileName, entry);
                }
                catch (Exception e)
                {
                    throw new SevenZipException("The archive may be corrupted.", e);
                }
                finally
                {
                    data.Clear();
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("SevenZipArchive");
            }
        }

        private void ValidateTargetDirectory(string directory)
        {
            if (directory == null)
            {
                throw new ArgumentNullException("directory");
            }
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(string.Format("The directory '{0}' does not exist.", directory));
            }
        }

        private static void ValidateExtractOptions(ExtractOptions options)
        {
            if (options.HasFlag(ExtractOptions.OverwriteExistingFiles) && options.HasFlag(ExtractOptions.SkipExistingFiles))
            {
                throw new ArgumentException(string.Format("The flags {0} and {1} cannot both be set.",
                    ExtractOptions.OverwriteExistingFiles, ExtractOptions.SkipExistingFiles),  "options");
            }
        }

        private void ValidateOutStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (!stream.CanWrite)
            {
                throw new ArgumentException("The specified stream should be writable.", "stream");
            }
        }

        private void ValidateFileNamesForExtraction(IEnumerable<string> entries)
        {
            ValidateFileNamesForExtraction(entries, true);
        }

        private void ValidateFileNamesForExtraction(IEnumerable<string> entries, bool checkInvalidChars)
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }

            int count = 0;
            foreach (string entry in entries)
            {
                try
                {
                    ValidateFileNameForExtraction(entry, checkInvalidChars);
                }
                catch(Exception e)
                {
                    throw new ArgumentException(string.Format("The entry at index {0} is invalid. See inner exception for details.", count), "entries", e);
                }
                count++;
            }
        }

        private void ValidateFileNameForExtraction(string entry)
        {
            ValidateFileNameForExtraction(entry, true, false);
        }

        private void ValidateFileNameForExtraction(string entry, bool checkIfUntitled)
        {
            ValidateFileNameForExtraction(entry, checkIfUntitled, false);
        }

        private void ValidateFileNameForExtraction(string entry, bool checkIfUntitled, bool extractToStream)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }
            if (entry == string.Empty)
            {
                throw new ArgumentException("The entry's file name cannot be empty.", "entry");
            }
            if (!_items.ContainsKey(entry))
            {
                throw new ArgumentException(string.Format("The entry '{0}' was not found in the archive.",
                    entry), "entry");
            }
            if (extractToStream && _items[entry].IsDirectory)
            {
                throw new ArgumentException(string.Format("The entry '{0}' is a directory. Only file entries can be extracted to streams.",
                    entry), "entry");
            }
            if (checkIfUntitled && _items[entry].IsUntitled)
            {
                throw new ArgumentException(string.Format("The entry '{0}' cannot be extracted to the file system because it is untitled. " +
                    "You can try to extract it manually to a file stream.", _items[entry].FileName));
            }
        }

        private static void VerifyExtractCallbackResult(ArchiveExtractCallback extractCallback, int result)
        {
            VerifyExtractCallbackResult(extractCallback, result, true);
        }

        private static void VerifyExtractCallbackResult(ArchiveExtractCallback extractCallback, int result, bool extractMode)
        {
            if (extractCallback.HasException)
            {
                if (extractCallback.Exception is CancelOperationException)
                {
                    return;
                }
                throw extractCallback.Exception;
            }
            else if (result != HRESULT.S_OK)
            {
                if (extractCallback.IsEncrypted && extractCallback.LastEntryFileName != null)
                {
                    throw new BadPasswordException(string.Format(
                        "Incorrect password specified to decrypt the file '{0}'.", extractCallback.LastEntryFileName),
                        extractCallback.LastEntryFileName, Marshal.GetExceptionForHR(result));
                }
                else
                {
                    throw new SevenZipException(string.Format("Unable to {0} '{1}' in the archive.", extractMode ? "extract" : "check",
                        extractCallback.LastEntryFileName ?? "files"), Marshal.GetExceptionForHR(result));
                }
            }
        }

        private static Dictionary<byte, int> GetHandlersDictionary(SafeLibraryHandle library)
        {
            GetHandlers(library);
            return _handlersDictionary;
        }

        private static ReadOnlyCollection<ArchiveHandler> GetHandlers(SafeLibraryHandle library)
        {
            if (_handlers == null)
            {
                List<ArchiveHandler> handlers = new List<ArchiveHandler>();
                _handlersDictionary = new Dictionary<byte, int>();

                GetNumberOfFormatsDelegate getNumberOfFormats =
                  (GetNumberOfFormatsDelegate)Marshal.GetDelegateForFunctionPointer(
                  GetProcAddress(library, "GetNumberOfFormats"), typeof(GetNumberOfFormatsDelegate));

                if (getNumberOfFormats == null)
                {
                    throw new SevenZipException("Unable to get the GetNumberOfFormats delegate.");
                }

                GetHandlerProperty2Delegate getHandlerProperty2 =
                  (GetHandlerProperty2Delegate)Marshal.GetDelegateForFunctionPointer(
                  GetProcAddress(library, "GetHandlerProperty2"), typeof(GetHandlerProperty2Delegate));

                if (getHandlerProperty2 == null)
                {
                    throw new SevenZipException("Unable to get the GetHandlerProperty2 delegate.");
                }

                uint numberOfFormats = 0;
                getNumberOfFormats(out numberOfFormats);

                for (uint formatIndex = 0; formatIndex < numberOfFormats; formatIndex++)
                {
                    ArchiveHandler handler = new ArchiveHandler();
                    handler.FormatIndex = (int)formatIndex;

                    PropVariant prop = new PropVariant();
                    try
                    {
                        getHandlerProperty2(formatIndex, ArchivePropId.Name, ref prop);
                        handler.Name = SafeCast(prop, "Unknown");
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.ClassID, ref prop);
                        handler.ClassID = prop.ValueAsGuid;
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.Update, ref prop);
                        handler.Update = SafeCast(prop, false);
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.KeepName, ref prop);
                        handler.KeepName = SafeCast(prop, false);
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.Extension, ref prop);
                        handler.Extensions = SafeCast(prop, string.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.AddExtension, ref prop);
                        handler.AddExtensions = SafeCast(prop, string.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        prop.Clear();

                        getHandlerProperty2(formatIndex, ArchivePropId.StartSignature, ref prop);
                        handler.StartSignature = prop.ValueAsSignature;
                        prop.Clear();
                    }
                    finally
                    {
                        prop.Clear();
                    }

                    byte handlerID = handler.ClassID.ToByteArray()[13];
                    if (handlerID > 0)
                    {
                        handlers.Add(handler);
                        _handlersDictionary.Add(handlerID, handlers.Count - 1);

                        try
                        {
                            handler.Format = (ArchiveFormat)handlerID;
                        }
                        catch (InvalidCastException)
                        {
                        }
                    }
                }
                
                _handlers = handlers.AsReadOnly();
            }

            return _handlers;
        }

        private static int GetFormatIndex(SafeLibraryHandle library, ArchiveFormat format)
        {
            var handlers = GetHandlers(library);
            var handlersDictionary = GetHandlersDictionary(library);

            int formatIndex = -1;
            if (handlersDictionary.ContainsKey((byte)format))
            {
                formatIndex = handlers[handlersDictionary[(byte)format]].FormatIndex;
            }

            return formatIndex;
        }

        private static ArchiveFormat GetArchiveFormat(SafeLibraryHandle library, int formatIndex)
        {
            ArchiveFormat format = ArchiveFormat.Unkown;

            if (formatIndex >= 0)
            {
                var handlers = GetHandlers(library);
                try
                {
                    format = (ArchiveFormat)handlers[formatIndex].ClassID.ToByteArray()[13];
                }
                catch(InvalidCastException)
                {
                }
            }

            return format;
        }

        private static bool TestSignature(byte[] buffer, uint startIndex, byte[] signature)
        {
            for (int i = 0; i < signature.Length; i++)
            {
                if (buffer[startIndex + i] != signature[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void Init(bool streamMode, string fileName, Stream stream, string password, ArchiveFormat format, IComparer<string> comparer, List<SevenZipArchive> archives)
        {
            try
            {
                _format = (streamMode && format == ArchiveFormat.Split) ? ArchiveFormat.Unkown : format;
                _fileName = fileName;
                _password = password;
                _items = new SortedDictionary<string, ArchiveEntry>(comparer);
                _library = Load7ZipLibrary();
                _stream = new InStreamWrapper(stream ?? File.OpenRead(fileName));
                if (stream is InSubStream && fileName == null)
                {
                    _fileName = "[Untitled SubArchive]";
                }
                _openCallback = new ArchiveOpenCallback(streamMode, _fileName, _stream.BaseStream, _password);
                if (stream is InSubStream)
                {
                    _openCallback.SetSubArchiveName(_fileName);
                }

                _formatIndex = GetFormatIndex(_library, _format);
                _archive = OpenStream(_library, streamMode, _fileName, _stream, _openCallback, ref _formatIndex, ref _defaultName);
                _format = GetArchiveFormat(_library, _formatIndex);

                _volumes = _openCallback.Volumes;
                _isEncrypted = _openCallback.IsEncrypted;
                _packedSize = _openCallback.PackedSize;
                _properties = GetArchiveProperties(_archive, _format);
                AddEntries(this, _defaultName, _items);
                _orderedItems = _items.Values.ToArray();

                OpenSubArchive(this, streamMode, archives);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static string GetItemPath(IInArchive archive, string defaultName, uint index)
        {
            bool isPathSet = false;
            return GetItemPath(archive, defaultName, index, ref isPathSet);
        }

        private static string GetItemPath(IInArchive archive, string defaultName, uint index, ref bool isPathSet)
        {
            PropVariant prop = new PropVariant();
            try
            {
                archive.GetProperty(index, ItemPropId.Path, ref prop);
                string path = SafeCast<string>(prop, null);
                prop.Clear();

                isPathSet = (path != null);
                if (path == null && defaultName != null)
                {
                    path = defaultName;

                    archive.GetProperty(index, ItemPropId.Extension, ref prop);
                    string ext = SafeCast<string>(prop, null);
                    if (ext != null)
                    {
                        path += "." + ext;
                    }
                }

                return path;
            }
            finally
            {
                prop.Clear();
            }
        }

        private static string GetDefaultName(string fileName, string ext, string addExt)
        {
            string defaultName = null;

            if (fileName != null)
            {
                string fileExt = Path.GetExtension(fileName).TrimStart('.');

                if (addExt == "*")
                {
                    addExt = string.Empty;
                }

                if (Path.HasExtension(fileName))
                {
                    defaultName = Path.GetFileNameWithoutExtension(fileName) + addExt;
                }
                else if (addExt == string.Empty)
                {
                    defaultName = fileName + "~";
                }
                else
                {
                    defaultName = fileName + addExt;
                }

                defaultName = defaultName.TrimEnd();
            }
            return defaultName;
        }

        private static void OpenSubArchive(SevenZipArchive archive, bool streamMode, List<SevenZipArchive> archives)
        {
            uint mainSubfile = uint.MaxValue;
            PropVariant prop = new PropVariant();
            try
            {
                archive._archive.GetArchiveProperty(ItemPropId.MainSubfile, ref prop);
                mainSubfile = SafeCast(prop, uint.MaxValue);
            }
            finally
            {
                prop.Clear();
            }

            Stream subArchiveStream = null;
            int numberOfItems = archive._items.Count;
            if (mainSubfile < numberOfItems && archive._archive is IInArchiveGetStream)
            {
                
                object seqStream = (archive._archive as IInArchiveGetStream).GetStream(mainSubfile);
                if (seqStream is IInStream)
                {
                    subArchiveStream = new InSubStream(seqStream as IInStream);
                }
            }

            if (subArchiveStream == null && numberOfItems == 1)
            {
                mainSubfile = archive._orderedItems[0].Index;
                bool testSubarchive =
                    archive._format == ArchiveFormat.BZip2 ||
                    archive._format == ArchiveFormat.GZip ||
                    archive._format == ArchiveFormat.xz ||
                    archive._format == ArchiveFormat.Z ||
                    archive._format == ArchiveFormat.lzma ||
                    archive._format == ArchiveFormat.lzma86;

                if (testSubarchive)
                {
                    string tempFilePath = null;
                    try
                    {
                        tempFilePath = Path.GetTempFileName();
                        subArchiveStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite,
                            FileShare.None, 0x1000, FileOptions.DeleteOnClose);
                        File.SetAttributes(tempFilePath, File.GetAttributes(tempFilePath) | FileAttributes.Temporary);
                    }
                    catch
                    {
                        if (subArchiveStream != null)
                        {
                            subArchiveStream.Close();
                        }
                        if (File.Exists(tempFilePath))
                        {
                            try { File.Delete(tempFilePath); }
                            catch { }
                        }
                        throw;
                    }

                    archive.ExtractFile(archive._orderedItems[0].FileName, subArchiveStream);
                }
            }

            if (subArchiveStream != null)
            {
                string mainSubfilePath = GetItemPath(archive._archive, archive._defaultName, mainSubfile);

                SevenZipArchive subArchive = null;
                try
                {
                    subArchive = new SevenZipArchive(true, mainSubfilePath, subArchiveStream, ArchiveFormat.Unkown, archive._password, archive._items.Comparer, archives);

                    if (subArchiveStream is InSubStream || subArchive._format == ArchiveFormat.Tar || subArchive._format == ArchiveFormat.Cpio)
                    {
                        archives.Insert(1, subArchive);
                    }
                    else
                    {
                        subArchive.Dispose();
                        subArchive = null;
                    }
                }
                catch
                {
                    if (subArchive != null)
                    {
                        subArchive.Dispose();
                        throw;
                    }
                }
            }

            if (archives.Count > 1 && archive == archives[0])
            {
                int last = archives.Count - 1;

                foreach (var entry in archives[last])
                {
                    entry._archiveReference.Target = archive;
                }
                foreach (var entry in archive)
                {
                    entry._archiveReference.Target = archives[last];
                }
                archive.Swap(archives[last]);
                archives[0] = archives[last];
                archives[last] = archive;

                for (int i = last; i > 0; i--)
                {
                    archives[i]._parent = archives[i - 1];
                }
                for (int i = 0; i < last; i++)
                {
                    archives[i]._hasSubArchive = true;
                }

                archives.Clear();
            }

        }

        private static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        private void Swap(SevenZipArchive archive)
        {
            Swap(ref _disposed, ref archive._disposed);
            Swap(ref _password, ref archive._password);
            Swap(ref _fileName, ref archive._fileName);
            Swap(ref _defaultName, ref archive._defaultName);
            Swap(ref _packedSize, ref archive._packedSize);
            Swap(ref _unPackedSize, ref archive._unPackedSize);
            Swap(ref _isEncrypted, ref archive._isEncrypted);
            Swap(ref _stream, ref archive._stream);
            Swap(ref _format, ref archive._format);
            Swap(ref _formatIndex, ref archive._formatIndex);
            Swap(ref _library, ref archive._library);
            Swap(ref _archive, ref archive._archive);
            Swap(ref _volumes, ref archive._volumes);
            Swap(ref _properties, ref archive._properties);
            Swap(ref _items, ref archive._items);
            Swap(ref _orderedItems, ref archive._orderedItems);
            Swap(ref _openCallback, ref archive._openCallback);

            // Don't swap these on purpose
            //Swap(ref _parent, ref archive._parent);
            //Swap(ref _hasSubArchive, ref archive._hasSubArchive);
        }

        private static IInArchive OpenStream(SafeLibraryHandle library, bool streamMode, string fileName, InStreamWrapper stream, ArchiveOpenCallback openCallback, ref int formatIndex, ref string defaultName)
        {
            var handlers = GetHandlers(library);
            string extension = fileName != null ? Path.GetExtension(fileName).TrimStart('.') : string.Empty;
            string password = openCallback.Password;
            List<int> orderIndices = new List<int>();
            if (formatIndex >= 0)
            {
                orderIndices.Add(formatIndex);
            }
            else
            {
                int numberOfFormatsFound = 0;
                for (int i = 0; i < handlers.Count; i++)
                {
                    if (extension != string.Empty && handlers[i].Extensions.Any(x => extension.Equals(x, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        orderIndices.Insert(numberOfFormatsFound++, i);
                    }
                    else
                    {
                        orderIndices.Add(i);
                    }
                }

                if (orderIndices.Count >= 2 && (numberOfFormatsFound == 0 || extension.Equals("exe", StringComparison.InvariantCultureIgnoreCase)))
                {
                    List<int> orderIndices2 = new List<int>();
                    List<int> signatureLenghts = new List<int>();
                    const uint headerBufferSize = (1 << 21);
                    const uint footerBufferSize = (1 << 15);

                    byte[] buf = new byte[headerBufferSize];
                    stream.Seek(0, (uint)SeekOrigin.Begin, IntPtr.Zero);
                    uint processedSize = stream.Read(buf, headerBufferSize);
                    if (processedSize == 0)
                    {
                        throw new ArgumentException("The specified file or stream is empty.");
                    }
                    Dictionary<uint, List<int>> hash = new Dictionary<uint, List<int>>(orderIndices.Count);
                    for (int i = 0; i < orderIndices.Count; i++)
                    {
                        byte[] sig = handlers[orderIndices[i]].StartSignature;
                        if (sig.Length < 2)
                        {
                            continue;
                        }
                        uint v = sig[0] | (uint)sig[1] << 8;
                        if (!hash.ContainsKey(v))
                        {
                            hash[v] = new List<int>();
                        }
                        hash[v].Add(i);
                    }

                    AddPossibleHandlers(orderIndices, orderIndices2, signatureLenghts, handlers, hash, buf, processedSize);

                    if (processedSize == headerBufferSize)
                    {
                        long length = stream.BaseStream.Length;
                        buf = new byte[footerBufferSize];
                        long offset = Math.Max(headerBufferSize - 16, length - footerBufferSize);
                        stream.Seek(offset, (uint)SeekOrigin.Begin, IntPtr.Zero);
                        processedSize = stream.Read(buf, footerBufferSize);

                        AddPossibleHandlers(orderIndices, orderIndices2, signatureLenghts, handlers, hash, buf, processedSize);
                    }

                    for (int i = 0; i < orderIndices.Count; i++)
                    {
                        int val = orderIndices[i];
                        if (val != 0xFF)
                        {
                            orderIndices2.Add(val);
                        }
                    }
                    orderIndices = orderIndices2;
                }
                else if (extension == "000" || extension == "001")
                {
                    const int bufferSize = (1 << 10);
                    byte[] buffer = new byte[bufferSize];
                    stream.Seek(0, (uint)SeekOrigin.Begin, IntPtr.Zero);
                    uint processedSize = stream.Read(buffer, bufferSize);
                    if (processedSize >= 16)
                    {
                        byte[] rarHeader = { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x00 };
                        if (TestSignature(buffer, 0, rarHeader) && buffer[9] == 0x73 && (buffer[10] & 1) != 0)
                        {
                            for (int i = 0; i < orderIndices.Count; i++)
                            {
                                int index = orderIndices[i];
                                if (!"rar".Equals(handlers[index].Name, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }
                                orderIndices.RemoveAt(i--);
                                orderIndices.Insert(0, index);
                                break;
                            }
                        }
                    }
                }
            }
            if (orderIndices.Count >= 2)
            {
                int isoIndex = FindHandlerIndexByName(handlers, "iso");
                int udfIndex = FindHandlerIndexByName(handlers, "udf");
                int iIso = -1;
                int iUdf = -1;
                for (int i = 0; i < orderIndices.Count; i++)
                {
                    if (orderIndices[i] == isoIndex) iIso = i;
                    if (orderIndices[i] == udfIndex) iUdf = i;
                }
                if (iUdf > iIso && iIso >= 0)
                {
                    orderIndices[iUdf] = isoIndex;
                    orderIndices[iIso] = udfIndex;
                }
            }

            for (int i = 0; i < orderIndices.Count; i++)
            {
                IInArchive archive = null;
                try
                {
                    archive = CreateInArchive(library, orderIndices[i]);
                    stream.Seek(0, (uint)SeekOrigin.Begin, IntPtr.Zero);
                    ulong checkPos = 1 << 22;
                    int result = archive.Open(stream, ref checkPos, openCallback);
                    if (openCallback.HasException)
                    {
                        throw openCallback.Exception;
                    }
                    else if (result != HRESULT.S_OK)
                    {
                        Exception exception = new SevenZipException("Unable to open the archive.", Marshal.GetExceptionForHR(result));

                        if (openCallback.IsEncrypted)
                        {
                            exception = new BadPasswordException("Incorrect password specified to decrypt the archive.", fileName, exception);
                        }

                        if (result == HRESULT.S_FALSE && !openCallback.IsEncrypted)
                        {
                            ReleaseInArchive(archive);
                            openCallback.Reset(streamMode, fileName, stream.BaseStream, password);
                            continue;
                        }
                        else
                        {
                            throw exception;
                        }
                    }
                    else
                    {
                        formatIndex = orderIndices[i];

                        if (handlers[formatIndex].Extensions.Length == 0)
                        {
                            defaultName = GetDefaultName(Path.GetFileName(fileName), string.Empty, string.Empty);
                        }
                        else
                        {
                            int index = Array.FindIndex(handlers[formatIndex].Extensions,
                                x => extension.Equals(x, StringComparison.InvariantCultureIgnoreCase));
                            if (index < 0) index = 0;
                            string ext = handlers[formatIndex].Extensions[index];
                            string addExt = string.Empty;
                            if (index < handlers[formatIndex].AddExtensions.Length)
                            {
                                addExt = handlers[formatIndex].AddExtensions[index];
                            }
                            defaultName = GetDefaultName(Path.GetFileName(fileName), ext, addExt);
                        }

                        return archive;
                    }
                }
                catch
                {
                    ReleaseInArchive(archive);
                    throw;
                }
            }

            throw new SevenZipException("Unable to open the archive. It may be corrupted.");
        }

        private static int FindHandlerIndexByName(ReadOnlyCollection<ArchiveHandler> handlers, string name)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                if (name.Equals(handlers[i].Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void AddPossibleHandlers(List<int> orderIndices, List<int> orderIndices2, List<int> signatureLenghts, ReadOnlyCollection<ArchiveHandler> handlers, Dictionary<uint, List<int>> hash, byte[] buf, uint processedSize)
        {
            processedSize -= 1;
            for (uint pos = 0; pos < processedSize; pos++)
            {
                for (; pos < processedSize && !hash.ContainsKey(buf[pos] | (uint)buf[pos + 1] << 8); pos++) ;
                if (pos == processedSize)
                {
                    break;
                }
                uint v = buf[pos] | (uint)buf[pos + 1] << 8;
                for (int i = 0; i < hash[v].Count; i++)
                {
                    int index = orderIndices[hash[v][i]];
                    if (index < handlers.Count)
                    {
                        byte[] sig = handlers[index].StartSignature;
                        if (sig.Length != 0 && pos + sig.Length <= processedSize + 1 && TestSignature(buf, pos, sig))
                        {
                            int insertPos = -1;
                            while (++insertPos < orderIndices2.Count && signatureLenghts[insertPos] >= sig.Length) ;
                            orderIndices2.Insert(insertPos, index);
                            signatureLenghts.Insert(insertPos, sig.Length);
                            orderIndices[hash[v][i]] = 0xFF;
                        }
                    }
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_hasSubArchive)
            {
                throw new InvalidOperationException(
                    "This archive has a sub archive, and thus cannot be disposed directly. Dispose() should be called on the sub archive.");
            }

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_stream != null)
                    {
                        _stream.Dispose();
                    }
                    if (_openCallback != null)
                    {
                        _openCallback.Dispose();
                    }
                }

                _items = null;
                _orderedItems = null;
                _volumes = null;
                _properties = null;
                _stream = null;
                _openCallback = null;


                // Dispose unmanaged resources
                if (_archive != null)
                {
                    _archive.Close();
                    ReleaseInArchive(_archive);
                    _archive = null;
                }
                if (_parent != null)
                {
                    _parent._hasSubArchive = false;
                    _parent.Dispose();
                    _parent = null;
                }
                if (_library != null)
                {
                    if (!_library.IsInvalid)
                    {
                        _library.Close();
                    }
                    _library = null;
                }

                _disposed = true;
            }
        }

        ~SevenZipArchive()
        {
            Dispose(false);
        }

        #endregion
    }

    # region Extensions

    public static class SevenZipArchiveExtensions
    {
        public static bool CheckAll(this IEnumerable<ArchiveEntry> entries)
        {
            bool checksOK = true;
            foreach (var group in entries.GroupBy(x => x.Archive))
            {
                checksOK = checksOK && group.Key.CheckFiles(group.Select(x => x.FileName));
            }
            return checksOK;
        }

        public static bool CheckAll(this IEnumerable<ArchiveEntry> entries, string password)
        {
            bool checksOK = true;
            foreach (var group in entries.GroupBy(x => x.Archive))
            {
                checksOK = checksOK && group.Key.CheckFiles(group.Select(x => x.FileName), password);
            }
            return checksOK;
        }

        public static void ExtractAll(this IEnumerable<ArchiveEntry> entries, string directory)
        {
            foreach (var group in entries.GroupBy(x => x.Archive))
            {
                group.Key.ExtractFiles(group.Select(x => x.FileName), directory);
            }
        }

        public static void ExtractAll(this IEnumerable<ArchiveEntry> entries, string directory, ExtractOptions options)
        {
            foreach (var group in entries.GroupBy(x => x.Archive))
            {
                group.Key.ExtractFiles(group.Select(x => x.FileName), directory, options);
            }
        }

        public static void ExtractAll(this IEnumerable<ArchiveEntry> entries, string directory, ExtractOptions options, string password)
        {
            foreach (var group in entries.GroupBy(x => x.Archive))
            {
                group.Key.ExtractFiles(group.Select(x => x.FileName), directory, options, password);
            }
        }
    }

    #endregion
}
