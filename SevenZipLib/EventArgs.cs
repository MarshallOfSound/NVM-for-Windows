using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SevenZipLib
{
    public class ProgressEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public ArchiveEntry CurrentEntry { get; internal set; }
        public int EntriesTotal { get; internal set; }
        public int EntriesProcessed { get; internal set; }

        internal ProgressEventArgs()
        {
        }
    }

    public class FileExtractedEventArgs : ProgressEventArgs
    {
        public string TargetFilePath { get; internal set; }

        internal FileExtractedEventArgs()
        {
        }
    }

    public class ExistingFileSkippedEventArgs : ProgressEventArgs
    {
        public string ExistingFilePath { get; internal set; }

        internal ExistingFileSkippedEventArgs()
        {
        }
    }

    public class FileExtractionFailedEventArgs : FileExtractedEventArgs
    {
        public bool AbortAndThrow { get; set; }
        public Exception Exception { get; internal set; }

        internal FileExtractionFailedEventArgs()
        {
        }
    }

    public class FileCheckedEventArgs : ProgressEventArgs
    {
        internal FileCheckedEventArgs()
        {
        }
    }

    public class FileCheckFailedEventArgs : FileCheckedEventArgs
    {
        public Exception Exception { get; internal set; }

        internal FileCheckFailedEventArgs()
        {
        }
    }

    public enum FileExistsAction
    {
        Throw,
        Overwrite,
        Skip
    }

    public class FileExistsEventArgs : EventArgs
    {
        public FileExistsAction Action { get; set; }
        public ArchiveEntry Entry { get; internal set; }
        public string TargetDirectory { get; internal set; }

        internal FileExistsEventArgs()
        {
        }
    }

    public class PasswordRequestedEventArgs : EventArgs
    {
        public string Password { get; set; }
        public ArchiveEntry Entry { get; internal set; }

        internal PasswordRequestedEventArgs()
        {
        }
    }
}
