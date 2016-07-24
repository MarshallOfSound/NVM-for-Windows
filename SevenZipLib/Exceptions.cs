using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SevenZipLib
{
    public class SevenZipException : Exception
    {
        public SevenZipException()
        {
        }

        public SevenZipException(string message)
            : base(message)
        {
        }

        public SevenZipException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class PasswordRequiredException : SevenZipException
    {
        public string FileName { get; set; }

        public PasswordRequiredException(string message, string fileName)
            : base(message)
        {
            this.FileName = fileName;
        }

        public PasswordRequiredException(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            this.FileName = fileName;
        }
    }

    public class BadPasswordException : SevenZipException
    {
        public string FileName { get; set; }

        public BadPasswordException(string message)
            : base(message)
        {
        }

        public BadPasswordException(string message, string fileName)
            : base(message)
        {
            this.FileName = fileName;
        }

        public BadPasswordException(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            this.FileName = fileName;
        }
    }

    public class BadCrcException : SevenZipException
    {
        public string FileName { get; set; }

        public BadCrcException(string message)
            : base(message)
        {
        }
        
        public BadCrcException(string message, string fileName)
            : base(message)
        {
            this.FileName = fileName;
        }

        public BadCrcException(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            this.FileName = fileName;
        }
    }

    public class DataErrorException : SevenZipException
    {
        public string FileName { get; set; }

        public DataErrorException(string message)
            : base(message)
        {
        }
        
        public DataErrorException(string message, string fileName)
            : base(message)
        {
            this.FileName = fileName;
        }

        public DataErrorException(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            this.FileName = fileName;
        }
    }

    public class UnsupportedMethodException : SevenZipException
    {
        public string FileName { get; set; }

        public UnsupportedMethodException(string message)
            : base(message)
        {
        }
        
        public UnsupportedMethodException(string message, string fileName)
            : base(message)
        {
            this.FileName = fileName;
        }

        public UnsupportedMethodException(string message, string fileName, Exception innerException)
            : base(message, innerException)
        {
            this.FileName = fileName;
        }
    }

    internal class CancelOperationException : Exception
    {
    }
}
