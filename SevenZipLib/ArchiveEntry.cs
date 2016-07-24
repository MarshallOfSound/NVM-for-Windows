using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace SevenZipLib
{
    [Serializable]
    public class ArchiveEntry : IEquatable<ArchiveEntry>, IEqualityComparer<ArchiveEntry>
    {
        [NonSerialized]
        internal WeakReference _archiveReference;
        internal uint Index { get; set; }

        public string FileName { get; internal set; }
        public DateTime? LastWriteTime { get; internal set; }
        public DateTime? CreationTime { get; internal set; }
        public DateTime? LastAccessTime { get; internal set; }
        public ulong Size { get; internal set; }
        public uint Crc { get; internal set; }
        public FileAttributes? Attributes { get; internal set; }
        public bool IsDirectory { get; internal set; }
        public bool IsEncrypted { get; internal set; }
        public string Comment { get; internal set; }
        public bool IsUntitled { get; internal set; }

        internal ArchiveEntry(SevenZipArchive archive, string fileName, uint index)
        {
            Debug.Assert(fileName != null, "A file name should be specified.");
            this._archiveReference = new WeakReference(archive);
            this.FileName = fileName;
            this.Index = index;
        }

        public SevenZipArchive Archive
        {
            get { return (SevenZipArchive)_archiveReference.Target; }
        }

        #region Public Methods

        public void Extract(Stream stream)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, stream);
        }

        public void Extract(Stream stream, ExtractOptions options)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, stream, options);
        }

        public void Extract(Stream stream, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, stream, options, password);
        }

        public void Extract(string directory)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, directory);
        }

        public void Extract(string directory, ExtractOptions options)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, directory, options);
        }

        public void Extract(string directory, ExtractOptions options, string password)
        {
            EnsureNotDisposed();
            Archive.ExtractFile(FileName, directory, options, password);
        }

        public bool Check()
        {
            EnsureNotDisposed();
            return Archive.CheckFile(FileName);
        }

        public bool Check(string password)
        {
            EnsureNotDisposed();
            return Archive.CheckFile(FileName, password);
        }

        public override string ToString()
        {
            return FileName;
        }

        #endregion

        #region Private Methods

        private void EnsureNotDisposed()
        {
            if (_archiveReference == null || !_archiveReference.IsAlive)
            {
                throw new ObjectDisposedException("SevenZipArchive");
            }
        }

        #endregion

        #region IEquatable<ArchiveEntry> Members

        public bool Equals(ArchiveEntry other)
        {
            return other != null && this.Index == other.Index && this.Archive == other.Archive;
        }

        #endregion

        #region IEqualityComparer<ArchiveEntry> Members

        public bool Equals(ArchiveEntry x, ArchiveEntry y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(ArchiveEntry obj)
        {
            int hash;

            SevenZipArchive archive = Archive;
            if (archive != null)
            {
                hash = Index.GetHashCode() ^ archive.GetHashCode();
            }
            else if (FileName != null)
            {
                hash = FileName.GetHashCode();
            }
            else
            {
                Debug.Fail("FileName should be set.");
                hash = Index.GetHashCode();
            }

            return hash;
        }

        #endregion
    }

}
