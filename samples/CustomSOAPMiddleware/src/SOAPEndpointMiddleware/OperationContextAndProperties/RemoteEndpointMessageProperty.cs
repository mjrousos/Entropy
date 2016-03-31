// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Simplified, sample version of RemoteEndpointMessageProperty to use in .NET Core.
// This property is automatically set for all incoming SOAP messages for use by services.

using System;
using System.Net;

namespace SOAPEndpointMiddleware
{
    public sealed class RemoteEndpointMessageProperty
    {
        public string Address { get; private set; }
        public int Port { get; private set; }
        public static string Name => "System.ServiceModel.Channels.RemoteEndpointMessageProperty";

        public RemoteEndpointMessageProperty(string address, int port)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentException("Value must be in range", "port");
            }

            Port = port;
            Address = address;
        }
    }
}