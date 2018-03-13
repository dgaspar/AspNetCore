﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal
{
    public class SocketAwaitable : ICriticalNotifyCompletion
    {
        private static readonly Action _callbackCompleted = () => { };

        private readonly PipeScheduler _ioScheduler;

        private Action _callback;
        private int _bytesTransferred;
        private SocketError _error;

        public SocketAwaitable(PipeScheduler ioScheduler)
        {
            _ioScheduler = ioScheduler;
        }

        public SocketAwaitable GetAwaiter() => this;
        public bool IsCompleted => ReferenceEquals(_callback, _callbackCompleted);

        public int GetResult()
        {
            Debug.Assert(ReferenceEquals(_callback, _callbackCompleted));

            _callback = null;

            if (_error != SocketError.Success)
            {
                throw new SocketException((int)_error);
            }

            return _bytesTransferred;
        }

        public void OnCompleted(Action continuation)
        {
            if (ReferenceEquals(_callback, _callbackCompleted) ||
                ReferenceEquals(Interlocked.CompareExchange(ref _callback, continuation, null), _callbackCompleted))
            {
                continuation();
            }
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }

        public void Complete(int bytesTransferred, SocketError socketError)
        {
            _error = socketError;
            _bytesTransferred = bytesTransferred;
            var continuation = Interlocked.Exchange(ref _callback, _callbackCompleted);

            if (continuation != null)
            {
                _ioScheduler.Schedule(state => ((Action)state)(), continuation);
            }
        }
    }
}
