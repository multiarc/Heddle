#if DNXCORE50 || DNX451
using Microsoft.Framework.Runtime;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates
{
    public class TtlGlobalCache : ITtlGlobalCache
    {
        private static readonly string PipeName = "TtlGlobalCachePipe";
        private static readonly string PipeServerName = "localhost";
        private readonly Dictionary<IdPair, ITtlTemplate> _cache = new Dictionary<IdPair, ITtlTemplate>();
        private readonly object _lockObject = new object();
        private readonly PipeStream _server;
        private readonly PipeStream _client;

        private struct IdPair : IEquatable<IdPair>
        {
            private readonly int _idMaster;
            private readonly int _idTemplate;

            public IdPair(int idMaster, int idTemplate)
            {
                _idTemplate = idTemplate;
                _idMaster = idMaster;
            }

            public bool Equals(IdPair other)
            {
                return other._idMaster == _idMaster && other._idTemplate == _idTemplate;
            }

            public override int GetHashCode()
            {
                return _idTemplate.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (!(obj is IdPair))
                    return false;
                return Equals((IdPair) obj);
            }

            public static bool operator ==(IdPair one, IdPair another)
            {
                return one.Equals(another);
            }

            public static bool operator !=(IdPair one, IdPair another)
            {
                return !one.Equals(another);
            }
        }

        public TtlGlobalCache()
        {
            try
            {
                _server = new NamedPipeServerStream(PipeName, PipeDirection.InOut)
                {
                    ReadMode = PipeTransmissionMode.Message
                };
                var serverThread = new Thread(ListenServerThread) {IsBackground = true};
                serverThread.Start();
            }
            catch (IOException)
            {
                _server = null;
            }
            if (_server == null)
            {
                var clientThread = new Thread(ListenClientThread) {IsBackground = true};
                _client = new NamedPipeClientStream(PipeServerName, PipeName, PipeDirection.InOut, PipeOptions.Asynchronous)
                {
                    ReadMode = PipeTransmissionMode.Message
                };
                clientThread.Start();
            }
        }

        private async void ListenServerThread()
        {
            while (!_disposed)
            {
                try
                {
                    using (var reader = new StreamReader(_server))
                    {
                        var message = await reader.ReadLineAsync();
                        var cacheRemove = message?.Split(',');
                        if (cacheRemove != null && cacheRemove.Length > 1)
                        {
                            RemoveFromCacheNoBroadCast(int.Parse(cacheRemove[0]), int.Parse(cacheRemove[1]));
                            using (var writer = new StreamWriter(_server))
                            {
                                await writer.WriteLineAsync(message);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private async void ListenClientThread()
        {
            while (!_disposed)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(_client))
                    {
                        var message = await reader.ReadLineAsync();
                        var cacheRemove = message?.Split(',');
                        if (cacheRemove != null && cacheRemove.Length > 1)
                        {
                            RemoveFromCacheNoBroadCast(int.Parse(cacheRemove[0]), int.Parse(cacheRemove[1]));
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void RemoveFromCacheNoBroadCast(int idMaster, int idContent)
        {
            var searchValue = new IdPair(idMaster, idContent);
            lock (_lockObject)
            {
                if (_cache.ContainsKey(searchValue))
                {
                    var ttlTemplate = _cache[searchValue];
                    _cache.Remove(searchValue);
                    ttlTemplate.Dispose();
                }
            }
        }

        public async Task RemoveFromCache(int idMaster, int idContent)
        {
            RemoveFromCacheNoBroadCast(idMaster, idContent);
            if (_server != null)
            {
                using (var writer = new StreamWriter(_server))
                {
                    await writer.WriteLineAsync($"{idMaster},{idContent}");
                }
            }
            else if (_client != null)
            {
                using (var writer = new StreamWriter(_client))
                {
                    await writer.WriteLineAsync($"{idMaster},{idContent}");
                }
            }
        }

        public ITtlTemplate GetOrCreateTemplate(string masterTemplate, string template, DateTime dateUpdated,
            DateTime masterDateUpdated, int idMaster, int idContent)
        {
            ITtlTemplate result;
            var searchValue = new IdPair(idMaster, template.IsNullOrEmpty() ? 0 : idContent);
            lock (_lockObject)
            {
                if (_cache.TryGetValue(searchValue, out result))
                {
                    if (result.DateCreated != dateUpdated || result.MasterDateCreated != masterDateUpdated)
                    {
                        if (!result.Recompile(masterTemplate + template).Success)
                        {
                            //Update dates so old template using in runtime next request ok
                            result.MasterDateCreated = masterDateUpdated;
                            result.DateCreated = dateUpdated;
                            throw new TemplateCompileException(result.CompileResult.Errors);
                        }
                        result.MasterDateCreated = masterDateUpdated;
                        result.DateCreated = dateUpdated;
                    }
                    return result;
                }
            }
            result = new TtlTemplate(masterTemplate + template);
            if (!result.CompileResult.Success)
                throw new TemplateCompileException(result.CompileResult.Errors);
            result.MasterDateCreated = masterDateUpdated;
            result.DateCreated = dateUpdated;
            lock (_lockObject)
            {
                _cache.Add(searchValue, result);
            }
            return result;
        }

        #region IDisposable
        private volatile bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
                _disposed = true;
                lock (_lockObject)
                {
                    _cache.Clear();
                }
                _server?.Dispose();
                _client?.Dispose();
            }
        }

        ~TtlGlobalCache()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}