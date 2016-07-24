using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SevenZipLib
{
    internal class ArchiveExtractCallback : IArchiveExtractCallback, ICryptoGetTextPassword, IDisposable
    {
        private enum ExtractMode { ExtractToStream, ExtractToFile, IntegrityCheck, UnpackedSize }

        private SevenZipArchive _archive;
        private string _targetDirectory;
        private ExtractOptions _options;
        private bool _disposed;
        private string _password;
        private Exception _exception;
        private bool _isEncrypted;
        private long _unPackedSize;
        private uint _lastEntryIndex;
        private AskMode _lastAskMode;
        private bool _lastEntryIndexSet;
        private string _lastExtractedFileName;
        private bool _lastFileSkipped;
        private Stream _targetStream;
        private OutStreamWrapper _stream;
        private ExtractMode _extractMode;
        private Dictionary<uint, ArchiveEntry> _entries;
        private SortedDictionary<string, ArchiveEntry> _directoryEntries;
        private int _entriesProcessed;

        #region Constructors

        /// <summary>
        /// Constructor used for getting the unpacked size of an archive
        /// </summary>
        public ArchiveExtractCallback(string password)
        {
            _password = password;
            _extractMode = ExtractMode.UnpackedSize;
        }

        /// <summary>
        /// Constructor used for integrity checks (test mode)
        /// </summary>
        public ArchiveExtractCallback(SevenZipArchive archive, Dictionary<uint, ArchiveEntry> entries, string password)
        {
            _archive = archive;
            _entries = entries;
            _password = string.IsNullOrEmpty(password) ? archive.Password : password;
            _options = ExtractOptions.NoAbortOnFailure;
            _extractMode = ExtractMode.IntegrityCheck;
        }

        /// <summary>
        /// Constructor used to extract a file to a stream
        /// </summary>
        public ArchiveExtractCallback(SevenZipArchive archive, Dictionary<uint, ArchiveEntry> entries, string password, Stream targetStream, ExtractOptions options)
        {
            _archive = archive;
            _entries = entries;
            _password = string.IsNullOrEmpty(password) ? archive.Password : password;
            _targetStream = targetStream;
            _options = options;
            _extractMode = ExtractMode.ExtractToStream;
        }

        /// <summary>
        /// Constructor used to extract files to the file system
        /// </summary>
        public ArchiveExtractCallback(SevenZipArchive archive, Dictionary<uint, ArchiveEntry> entries, string password, string targetDirectory, ExtractOptions options)
        {
            _archive = archive;
            _entries = entries;
            _directoryEntries = new SortedDictionary<string, ArchiveEntry>(new DirectoryNameComparer());
            _password = string.IsNullOrEmpty(password) ? archive.Password : password;
            _targetDirectory = targetDirectory;
            _options = options;
            _extractMode = ExtractMode.ExtractToFile;
        }

        #endregion

        #region Properties

        public bool HasException
        {
            get { return _exception != null; }
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        public string Password
        {
            get { return _password; }
        }

        public bool IsEncrypted
        {
            get { return _isEncrypted; }
        }

        public long UnPackedSize
        {
            get { return _unPackedSize; }
        }

        public string LastExtractedFileName
        {
            get { return _lastExtractedFileName ?? LastEntryFileName; }
        }

        public string LastEntryFileName
        {
            get { return _entries != null ? _entries[_lastEntryIndex].FileName : null; }
        }

        public string LastExtractedFilePath
        {
            get { return _targetDirectory != null ? Path.Combine(_targetDirectory, LastExtractedFileName) : null; }
        }

        #endregion

        #region IArchiveExtractCallback Members

        public void SetTotal(ulong total)
        {
            _unPackedSize = (long)total;
        }

        public void SetCompleted(ref ulong completeValue)
        {
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;
            _lastEntryIndex = index;
            _lastEntryIndexSet = true;
            _lastAskMode = askExtractMode;

            Debug.Assert(_stream == null);
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_extractMode == ExtractMode.UnpackedSize)
            {
                return HRESULT.E_ABORT;
            }
            else if (askExtractMode != AskMode.Extract)
            {
                if (askExtractMode == AskMode.Skip)
                {
                    Debug.Assert(!_entries.ContainsKey(index));
                }
                return HRESULT.S_OK;
            }
            else if (!_entries.ContainsKey(index))
            {
                Debug.Fail("Unexpected case.");
                _stream = new OutStreamWrapper(new DummyOutStream());
                outStream = _stream;
                return HRESULT.S_OK;
            }

            Debug.Assert(_archive != null, "_archive was not set correctly. The wrong constructor may have been used.");

            if (_targetDirectory != null)
            {
                // Writing to file system

                Debug.Assert(_extractMode == ExtractMode.ExtractToFile, "Wrong constructor used.");
                Debug.Assert(_targetDirectory.IndexOfAny(Path.GetInvalidPathChars()) == -1);

                string filePath;
                if (AutoRenamePath(LastEntryFileName, out _lastExtractedFileName))
                {
                    filePath = LastExtractedFilePath;
                }
                else
                {
                    Debug.Assert(_exception == null || _exception is PasswordRequiredException);
                    if (!(_exception is PasswordRequiredException))
                    {
                        _exception = new SevenZipException(string.Format(
                            "The file '{0}' cannot be extracted because the path contains invalid characters or components.", LastEntryFileName));
                    }

                    _stream = new OutStreamWrapper(new DummyOutStream());
                    outStream = _stream;
                    return HRESULT.S_OK;
                }

                try
                {
                    string directory = _entries[index].IsDirectory ? filePath : Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (_entries[index].IsDirectory)
                    {
                        Debug.Assert(_directoryEntries != null);
                        _directoryEntries[directory] = _entries[index];
                        _stream = new OutStreamWrapper(new DummyOutStream());
                        outStream = _stream; 
                        return HRESULT.S_OK;
                    }
                    else
                    {
                        if (File.Exists(filePath))
                        {
                            FileExistsEventArgs fileExistsArgs = new FileExistsEventArgs()
                            {
                                Action = ExtractOptionsToFileExistAction(_options),
                                TargetDirectory = _targetDirectory,
                                Entry = _entries[_lastEntryIndex],
                            };

                            int result = InvokeEventHandler(fileExistsArgs, _archive.OnFileExists, "SevenZipArchive.FileExists");
                            if (result != HRESULT.S_OK)
                            {
                                return result;
                            }

                            if (fileExistsArgs.Action == FileExistsAction.Throw)
                            {
                                Debug.Assert(_exception == null || _exception is PasswordRequiredException);
                                if (!(_exception is PasswordRequiredException))
                                {
                                    _exception = new SevenZipException(string.Format(
                                        "The file '{0}' cannot be extracted to '{1}' because a file with the same name already exists.",
                                        Path.GetFileName(LastExtractedFileName), directory));
                                }

                                _stream = new OutStreamWrapper(new DummyOutStream());
                                outStream = _stream;
                                return HRESULT.S_OK;
                            }
                            else if (fileExistsArgs.Action == FileExistsAction.Skip)
                            {
                                _lastFileSkipped = true;
                                _stream = new OutStreamWrapper(new DummyOutStream());
                                outStream = _stream;
                                return HRESULT.S_OK;
                            }
                        }
                        _stream = new OutStreamWrapper(File.Create(filePath));
                        outStream = _stream;
                        return HRESULT.S_OK;
                    }
                }
                catch(Exception e)
                {
                    Debug.Assert(_exception == null || _exception is PasswordRequiredException);
                    if (!(_exception is PasswordRequiredException))
                    {
                        _exception = new SevenZipException(string.Format(
                            "An exception occured while extracting the file '{0}'.", LastEntryFileName), e);
                    }

                    _stream = new OutStreamWrapper(new DummyOutStream());
                    outStream = _stream;
                    return HRESULT.S_OK;
                }
            }
            else
            {
                // Writing to stream

                Debug.Assert(_extractMode == ExtractMode.ExtractToStream, "Wrong constructor used.");
                Debug.Assert(_targetStream != null);
                _stream = new OutStreamWrapper(_targetStream, false);
                outStream = _stream;
                return HRESULT.S_OK;
            }
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
        }

        public int SetOperationResult(OperationResult operationResult)
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_lastAskMode == AskMode.Skip)
            {
                Debug.Assert(!_entries.ContainsKey(_lastEntryIndex));
                return HRESULT.S_OK;
            }
            
            _entriesProcessed++;
            _lastEntryIndexSet = false;
            Debug.Assert(_entriesProcessed <= _entries.Count);

            if (_lastFileSkipped)
            {
                _lastFileSkipped = false;

                ExistingFileSkippedEventArgs args = new ExistingFileSkippedEventArgs()
                {
                    CurrentEntry = _entries[_lastEntryIndex],
                    ExistingFilePath = LastExtractedFilePath,
                    EntriesTotal = _entries.Count,
                    EntriesProcessed = _entriesProcessed,
                };
                return InvokeEventHandler(args, _archive.OnExistingFileSkipped, "SevenZipArchive.ExistingFileSkipped");
            }

            if (_exception != null)
            {
                if (_exception is PasswordRequiredException)
                {
                    _exception = new PasswordRequiredException(
                        string.Format("The file '{0}' is encrypted, and requires a password to be decrypted.", LastEntryFileName),
                        LastEntryFileName, _exception.InnerException);
                }
                return RaiseExtractionOrCheckFailedEvent(_exception);
            }

            int result = HRESULT.S_OK;
            switch (operationResult)
            {
                case OperationResult.OK:
                    if (_extractMode == ExtractMode.ExtractToFile || _extractMode == ExtractMode.ExtractToStream)
                    {
                        if (_extractMode == ExtractMode.ExtractToFile)
                        {
                            SetAttributes(LastExtractedFilePath, _entries[_lastEntryIndex]);
                        }

                        FileExtractedEventArgs args = new FileExtractedEventArgs()
                        {
                            CurrentEntry = _entries[_lastEntryIndex],
                            TargetFilePath = LastExtractedFilePath,
                            EntriesTotal = _entries.Count,
                            EntriesProcessed = _entriesProcessed,
                        };
                        result = InvokeEventHandler(args, _archive.OnFileExtracted, "SevenZipArchive.FileExtracted");
                    }
                    else if (_extractMode == ExtractMode.IntegrityCheck)
                    {
                        FileCheckedEventArgs args = new FileCheckedEventArgs()
                        {
                            CurrentEntry = _entries[_lastEntryIndex],
                            EntriesTotal = _entries.Count,
                            EntriesProcessed = _entriesProcessed,
                        };
                        result = InvokeEventHandler(args, _archive.OnFileChecked, "SevenZipArchive.FileChecked");
                    }
                    else
                    {
                        result = HRESULT.S_OK;
                    }
                    break;

                case OperationResult.CRCError:
                    _exception = new BadCrcException(string.Format(
                        "The {0} is corrupted. The CRC check has failed.",
                        (LastEntryFileName != null ? string.Format("file '{0}'", LastEntryFileName) : "archive")),
                        LastEntryFileName);
                    result = RaiseExtractionOrCheckFailedEvent(_exception);
                    break;

                case OperationResult.DataError:
                    if (LastEntryFileName != null)
                    {
                        _exception = new DataErrorException(string.Format(
                            "The file '{0}' is corrupted. A data error has occured.", LastEntryFileName), LastEntryFileName);
                        if (_isEncrypted)
                        {
                            _exception = new BadPasswordException(string.Format(
                                "Incorrect password specified to decrypt the file '{0}'.", LastEntryFileName), LastEntryFileName, _exception);
                        }
                    }
                    else
                    {
                        _exception = new DataErrorException("The archive is corrupted. A data error has occured.");
                    }
                    result = RaiseExtractionOrCheckFailedEvent(_exception);
                    break;

                case OperationResult.UnSupportedMethod:
                    _exception = new UnsupportedMethodException("An unsupported method error has occured.", LastEntryFileName);
                    result = RaiseExtractionOrCheckFailedEvent(_exception);
                    break;
            }

            return result;
        }

        #endregion

        #region ICryptoGetTextPassword Members

        public int CryptoGetTextPassword(out string password)
        {
            _isEncrypted = true;

            PasswordRequestedEventArgs args = new PasswordRequestedEventArgs() { Password = _password };
            if (_entries != null)
            {
                if (_lastEntryIndexSet)
                {
                    args.Entry = _entries[_lastEntryIndex];
                }
                else
                {
                    args.Entry = _entries.Values.OrderBy(x => x.Index).FirstOrDefault(x => x.IsEncrypted);
                    Debug.Assert(args.Entry != null);
                }
            };

            int result = HRESULT.S_OK;
            if (_extractMode != ExtractMode.UnpackedSize)
            {
                result = InvokeEventHandler(args, _archive.OnPasswordRequested, "SevenZipArchive.PasswordRequested");
            }

            if (result == HRESULT.S_OK)
            {
                if (!string.IsNullOrEmpty(args.Password))
                {
                    password = args.Password;
                }
                else
                {
                    password = string.Empty;
                    _exception = new PasswordRequiredException(null, null, Marshal.GetExceptionForHR(result));
                }
            }
            else
            {
                password = string.Empty;
            }

            return result;
        }

        #endregion

        #region Private Methods


        private int InvokeEventHandler<T>(T e, Action<T> handler, string eventName)
            where T : EventArgs
        {
            int result = HRESULT.S_OK;
            try
            {
                handler(e);
                if (e is ProgressEventArgs && (e as ProgressEventArgs).Cancel)
                {
                    _exception = new CancelOperationException();
                    result = HRESULT.E_ABORT;
                }
            }
            catch(Exception exception)
            {
                _exception = new SevenZipException("An error occured while involking the " + eventName + " handler.", exception);
                result = HRESULT.E_ABORT;
            }

            return result;
        }

        private void CleanLastFileExtracted()
        {
            if (_targetDirectory != null && !_entries[_lastEntryIndex].IsDirectory)
            {
                try
                {
                    string path = LastExtractedFilePath;
                    if (File.Exists(path) && new FileInfo(path).Length == 0)
                    {
                        File.Delete(path);
                    }
                }
                catch { }
            }
        }

        private int RaiseExtractionOrCheckFailedEvent(Exception exception)
        {

            int result = HRESULT.S_OK;
            bool abortAndThrow = !_options.HasFlag(ExtractOptions.NoAbortOnFailure);

            if (_extractMode == ExtractMode.ExtractToFile || _extractMode == ExtractMode.ExtractToStream)
            {
                CleanLastFileExtracted();
                FileExtractionFailedEventArgs args = new FileExtractionFailedEventArgs()
                {
                    CurrentEntry = _entries[_lastEntryIndex],
                    TargetFilePath = LastExtractedFilePath,
                    EntriesTotal = _entries.Count,
                    EntriesProcessed = _entriesProcessed,
                    AbortAndThrow = abortAndThrow,
                    Exception = exception
                };

                result = InvokeEventHandler(args, _archive.OnFileExtractionFailed, "SevenZipArchive.FileExtractionFailed");
                abortAndThrow = args.AbortAndThrow;
            }
            else if (_extractMode == ExtractMode.IntegrityCheck)
            {
                FileCheckFailedEventArgs args = new FileCheckFailedEventArgs()
                {
                    CurrentEntry = _entries[_lastEntryIndex],
                    EntriesTotal = _entries.Count,
                    EntriesProcessed = _entriesProcessed,
                    Exception = exception
                };

                result = InvokeEventHandler(args, _archive.OnFileCheckFailed, "SevenZipArchive.FileCheckFailed");
            }
            else
            {
                Debug.Fail("Unexpected exception.");
                return HRESULT.S_OK;
            }

            if (result != HRESULT.S_OK)
            {
                return result;
            }
            else
            {
                if (!abortAndThrow)
                {
                    _exception = null;
                    return HRESULT.S_OK;
                }
                else
                {
                    _exception = exception;
                    return HRESULT.E_ABORT;
                }
            }
        }

        private FileExistsAction ExtractOptionsToFileExistAction(ExtractOptions options)
        {
            if (options.HasFlag(ExtractOptions.OverwriteExistingFiles))
            {
                return FileExistsAction.Overwrite;
            }
            else if (options.HasFlag(ExtractOptions.SkipExistingFiles))
            {
                return FileExistsAction.Skip;
            }
            else
            {
                return FileExistsAction.Throw;
            }
        }

        private bool AutoRenamePath(string relativePath, out string renamedPath)
        {
            renamedPath = null;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string reservedNamesPattern = @"^(PRN|AUX|CLOCK\$|NUL|CON|COM\d|LPT\d|\.+)$";

            string[] parts = relativePath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            List<int> invalidPartIndices = new List<int>();
            StringBuilder sb = new StringBuilder();

            for(int j = 0; j < parts.Length; j++)
            {
                StringBuilder part = new StringBuilder(parts[j]);

                if (Regex.IsMatch(parts[j], reservedNamesPattern, RegexOptions.IgnoreCase))
                {
                    invalidPartIndices.Add(j);
                    if (_options.HasFlag(ExtractOptions.RenameInvalidEntries))
                    {
                        if (Regex.IsMatch(parts[j], @"^\.+$"))
                        {
                            part.Clear();
                            part.Append("[" + parts[j].Length + "]");
                        }
                        else
                        {
                            part.Insert(0, SevenZipArchive.InvalidCharReplacement);
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    for (int i = 0; i < part.Length; i++)
                    {
                        if (Array.IndexOf(invalidChars, part[i]) >= 0)
                        {
                            invalidPartIndices.Add(j);
                            if (_options.HasFlag(ExtractOptions.RenameInvalidEntries))
                            {
                                part[i] = SevenZipArchive.InvalidCharReplacement;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }

                sb.Append(part.ToString());
                if (j < parts.Length - 1)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }
            }

            renamedPath = sb.ToString();
            return true;
        }

        private static void SetAttributes(string path, ArchiveEntry entry)
        {
            if (entry.IsDirectory)
            {
                if (entry.CreationTime.HasValue)
                {
                    try { Directory.SetCreationTime(path, entry.CreationTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.LastAccessTime.HasValue)
                {
                    try { Directory.SetLastAccessTime(path, entry.LastAccessTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.LastWriteTime.HasValue)
                {
                    try { Directory.SetLastWriteTime(path, entry.LastWriteTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.Attributes.HasValue)
                {
                    try { new DirectoryInfo(path).Attributes = entry.Attributes.Value; }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
            }
            else
            {
                if (entry.CreationTime.HasValue)
                {
                    try { File.SetCreationTime(path, entry.CreationTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.LastAccessTime.HasValue)
                {
                    try { File.SetLastAccessTime(path, entry.LastAccessTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.LastWriteTime.HasValue)
                {
                    try { File.SetLastWriteTime(path, entry.LastWriteTime.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
                if (entry.Attributes.HasValue)
                {
                    try { File.SetAttributes(path, entry.Attributes.Value); }
                    catch (Exception e) { Debug.Fail(e.Message); }
                }
            }
        }

        private void SetDirectoryAttributes()
        {
            Debug.Assert(_directoryEntries != null);
            foreach (var kvp in _directoryEntries)
            {
                SetAttributes(kvp.Key, kvp.Value);
            }
            _directoryEntries.Clear();
        }

        private class DirectoryNameComparer : IComparer<string>
        {
            #region IComparer<string> Members

            public int Compare(string x, string y)
            {
                return -x.ToUpper().CompareTo(y.ToUpper());
            }

            #endregion
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Verify that _entriesProcessed matches _entries.Count
                // For Xar archives, when no file list is specified in the call IInArchive.Extract(null, uint.MaxValue, ...),
                // the "[TOC].xml" file is not extracted, so there is an exception for that.
                Debug.Assert(_entries == null || _extractMode != ExtractMode.ExtractToFile || _exception != null ||
                    (_entriesProcessed == _entries.Count - 1 && _archive.Format == ArchiveFormat.Xar) ||
                    _entriesProcessed == _entries.Count);

                if (_extractMode == ExtractMode.ExtractToFile)
                {
                    SetDirectoryAttributes();
                }

                if (disposing)
                {
                    // Dispose managed resources
                    if (_stream != null)
                    {
                        _stream.Dispose();
                    }
                }

                _stream = null;
                _targetStream = null;
                _entries = null;
                _directoryEntries = null;
                _archive = null;


                // Dispose unmanaged resources

                _disposed = true;
            }
        }

        ~ArchiveExtractCallback()
        {
            Dispose(false);
        }

        #endregion
    }
}