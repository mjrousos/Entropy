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
