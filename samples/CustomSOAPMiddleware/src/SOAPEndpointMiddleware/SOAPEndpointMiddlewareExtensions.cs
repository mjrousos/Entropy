// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ServiceModel.Channels;

namespace Microsoft.AspNet.Builder
{
    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class SOAPEndpointMiddlewareExtensions
    {
        public static IApplicationBuilder UseSOAPEndpoint<T>(this IApplicationBuilder builder, string path, MessageEncoder encoder)
        {
            return builder.UseMiddleware<SOAPEndpointMiddleware.SOAPEndpointMiddleware>(typeof(T), path, encoder);
        }

        public static IApplicationBuilder UseSOAPEndpoint<T>(this IApplicationBuilder builder, string path, Binding binding)
        {
            var encoder = binding.CreateBindingElements().Find<MessageEncodingBindingElement>()?.CreateMessageEncoderFactory().Encoder;
            return builder.UseMiddleware<SOAPEndpointMiddleware.SOAPEndpointMiddleware>(typeof(T), path, encoder);
        }
    }
}
