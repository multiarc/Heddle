using System;
using Templates.Helpers;

namespace Templates.Data {
    public struct DocumentCacheItem: IEquatable<DocumentCacheItem> {
        private readonly string _document;
        public TypeReference ModelType;
        public string RootPath;

        #region Implementation of IEquatable<DocumentCacheItem>

        public bool Equals (DocumentCacheItem other)
        {
            return other.RootPath == RootPath && other._document == _document && other.ModelType == ModelType;
        }

        #endregion

        public DocumentCacheItem (string document)
        {
            _document = document;
            ModelType = null;
            RootPath = null;
        }

        public override int GetHashCode ()
        {
            return _document != null ? _document.GetHashCode() : 0;
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (!(obj is DocumentCacheItem))
                return false;
            return Equals((DocumentCacheItem) obj);
        }
    }
}