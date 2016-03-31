// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Based on https://github.com/dotnet/wcf/blob/master/src/System.Private.ServiceModel/src/System/ServiceModel/OperationContext.cs
// System.ServiceModel.OperationContext, although available in .NET Core, doesn't work because there are no usable constructors.
// This (much-simplified, server-only) version of OperationContext has a non-ServiceChannel constructor so that our 
// service host can create it. It cannot provide channel or session ID information but still allows .NET Core apps 
// to use outgoing message properties and header (which our dispatcher respects).

using System;
using System.ServiceModel.Channels;

namespace SOAPEndpointMiddleware
{
    public sealed class OperationContext
    {
        [ThreadStatic]
        private static Holder s_currentContext;

        private Message _request;
        private MessageProperties _outgoingMessageProperties;
        private MessageHeaders _outgoingMessageHeaders;
        private MessageVersion _outgoingMessageVersion;

        public OperationContext(Message request)
        {
            _request = request;
            _outgoingMessageVersion = request.Version;
        }

        public static OperationContext Current
        {
            get
            {
                return CurrentHolder.Context;
            }

            set
            {
                CurrentHolder.Context = value;
            }
        }

        internal static Holder CurrentHolder
        {
            get
            {
                Holder holder = s_currentContext;
                if (holder == null)
                {
                    holder = new Holder();
                    s_currentContext = holder;
                }
                return holder;
            }
        }

        public bool IsUserContext => false;

        internal bool HasOutgoingMessageHeaders
        {
            get { return (_outgoingMessageHeaders != null); }
        }

        public MessageHeaders OutgoingMessageHeaders
        {
            get
            {
                if (_outgoingMessageHeaders == null)
                    _outgoingMessageHeaders = new MessageHeaders(this.OutgoingMessageVersion);

                return _outgoingMessageHeaders;
            }
        }

        internal bool HasOutgoingMessageProperties
        {
            get { return (_outgoingMessageProperties != null); }
        }

        public MessageProperties OutgoingMessageProperties
        {
            get
            {
                if (_outgoingMessageProperties == null)
                    _outgoingMessageProperties = new MessageProperties();

                return _outgoingMessageProperties;
            }
        }

        internal MessageVersion OutgoingMessageVersion => _outgoingMessageVersion;

        public MessageHeaders IncomingMessageHeaders => _request?.Headers;

        public MessageProperties IncomingMessageProperties => _request?.Properties;

        public MessageVersion IncomingMessageVersion => _request?.Version;

        internal void Recycle()
        {
            _request = null;
            _outgoingMessageProperties = null;
            _outgoingMessageVersion = null;
            _outgoingMessageVersion = null;
        }

        internal class Holder
        {
            public OperationContext Context { get; set; }
        }
    }
}