using System.ServiceModel.Channels;

namespace SOAPEndpointMiddleware
{
    public interface IDispatchMessageInspector
    {
        object AfterReceiveRequest(ref Message request);
        void BeforeSendReply(ref Message reply, object correlationState);
    }
}