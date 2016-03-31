// Simplified, sample version of RemoteEndpointMessageProperty to use in .NET Core.
// When not building against .NET Core, redirect to the built-in property type.

#if NETCORE
using System.Net;

namespace System.ServiceModel.Channels
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
#else // NETCORE
using System.Runtime.CompilerServices;
[assembly: TypeForwardedTo(typeof(System.ServiceModel.Channels.RemoteEndpointMessageProperty))]
#endif // NETCORE