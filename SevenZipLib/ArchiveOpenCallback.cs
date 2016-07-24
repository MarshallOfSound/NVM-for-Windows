using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SevenZipLib
{
    internal class ArchiveOpenCallback : IArchiveOpenCallback, IArchiveOpenVolumeCallback, ICryptoGetTextPassword, IArchiveOpenSetSubArchiveName, IDisposable
    {
        private bool _disposed;
        private bool _streamMode;
        private string _fileName;
        private string _password;
        private List<string> _volumes;
        private Dictionary<string, InStreamWrapper> _streams;
        private Exception _exception;
        private bool _isEncrypted;
        private long _packedSize;
        private long _currentVolumeSize;
        private bool _subArchiveMode;
        private string _subArchiveName;

        public ArchiveOpenCallback(bool streamMode, string fileName, Stream stream, string password)
        {
            Init(streamMode, fileName, stream, password);
        }

        private void Init(bool streamMode, string fileName, Stream stream, string password)
        {
            Debug.Assert(stream != null);
            Debug.Assert(streamMode || fileName != null);

            _streamMode = streamMode;
            _fileName = fileName;
            _password = password;
            _volumes = new List<string>();
            _streams = new Dictionary<string, InStreamWrapper>();
            _exception = null;
            _isEncrypted = false;
            _packedSize = stream.Length;
            _currentVolumeSize = stream.Length;
            _subArchiveMode = false;
            _subArchiveName = null;
            
            _volumes.Add(fileName);
        }

        public void Reset(bool streamMode, string fileName, Stream stream, string password)
        {
            Dispose(true);
            _disposed = false;
            Init(streamMode, fileName, stream, password);
        }

        #region Properties

        public bool HasException
        {
            get { return _exception != null; }
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        public ReadOnlyCollection<string> Volumes
        {
            get { return _volumes.AsReadOnly(); }
        }

        public string Password
        {
            get { return _password; }
        }

        public bool IsEncrypted
        {
            get { return _isEncrypted; }
        }

        public long PackedSize
        {
            get { return _packedSize; }
        }

        #endregion

        #region IArchiveOpenCallback Members

        public void SetTotal(IntPtr files, IntPtr bytes)
        {
        }

        public void SetCompleted(IntPtr files, IntPtr bytes)
        {
        }

        #endregion

        #region IArchiveOpenVolumeCallback Members

        public int GetProperty(ItemPropId propID, ref PropVariant value)
        {
            value.Clear();

            if (_subArchiveMode)
            {
                if (propID == ItemPropId.Name)
                {
                    value.SetBString(_subArchiveName);
                }
            }
            else
            {
                switch (propID)
                {
                    case ItemPropId.Name:
                        if (!_streamMode)
                        {
                            value.SetBString(Path.GetFileName(_fileName));
                        }
                        break;
                    case ItemPropId.IsDirectory:
                        value.SetBool(_streamMode ? false : (byte)(new FileInfo(_fileName).Attributes & FileAttributes.Directory) != 0);
                        break;
                    case ItemPropId.Size:
                        value.SetULong((ulong)_currentVolumeSize);
                        break;
                    case ItemPropId.Attributes:
                        value.SetUInt(_streamMode ? (uint)0 : (uint)new FileInfo(_fileName).Attributes);
                        break;
                    case ItemPropId.CreationTime:
                        if (!_streamMode)
                        {
                            value.SetDateTime(new FileInfo(_fileName).CreationTime);
                        }
                        break;
                    case ItemPropId.LastAccessTime:
                        if (!_streamMode)
                        {
                            value.SetDateTime(new FileInfo(_fileName).LastAccessTime);
                        }
                        break;
                    case ItemPropId.LastWriteTime:
                        if (!_streamMode)
                        {
                            value.SetDateTime(new FileInfo(_fileName).LastWriteTime);
                        }
                        break;
                }
            }
            return HRESULT.S_OK;
        }

        public int GetStream(string name, out IInStream inStream)
        {
            if (_subArchiveMode)
            {
                inStream = null;
                return HRESULT.S_FALSE;
            }

            if (_streams.ContainsKey(name))
            {
                inStream = _streams[name];
            }
            else
            {
                string path = Path.Combine(Path.GetDirectoryName(_fileName), name);
                try
                {
                    InStreamWrapper stream = new InStreamWrapper(File.OpenRead(path));
                    _volumes.Add(path);
                    _streams.Add(path, stream);
                    _currentVolumeSize = stream.BaseStream.Length;
                    _packedSize += _currentVolumeSize;
                    inStream = stream;
                }
                catch (FileNotFoundException)
                {
                    inStream = null;
                    return HRESULT.S_FALSE;
                }
                catch (Exception e)
                {
                    if (_exception == null)
                    {
                        _exception = new SevenZipException("Unable to open the volume: " + path, e);
                    }
                    inStream = null;
                    return HRESULT.E_INVALIDARG;
                }
            }
            return HRESULT.S_OK;
        }

        #endregion

        #region ICryptoGetTextPassword Members

        public int CryptoGetTextPassword(out string password)
        {
            int result = HRESULT.S_OK;
            _isEncrypted = true;

            if (!string.IsNullOrEmpty(_password))
            {
                password = _password;
            }
            else
            {
                password = string.Empty;
                result = HRESULT.E_ABORT;
                _exception = new PasswordRequiredException(
                    "This archive is encrypted, and requires a password to be decrypted.",
                    _subArchiveMode ? _subArchiveName : _fileName,
                    Marshal.GetExceptionForHR(result));
            }

            return result;
        }

        #endregion

        #region IArchiveOpenSetSubArchiveName Members

        public void SetSubArchiveName(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "A sub archive name should be set");
            _subArchiveMode = true;
            _subArchiveName = name;
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
                if (disposing)
                {
                    // Dispose managed resources
                    foreach (var pair in _streams)
                    {
                        pair.Value.Dispose();
                    }
                    _streams.Clear();
                    _volumes.Clear();
                }

                _streams = null;
                _volumes = null;

                // Dispose unmanaged resources

                _disposed = true;
            }
        }

        ~ArchiveOpenCallback()
        {
            Dispose(false);
        }

        #endregion
    }

}
