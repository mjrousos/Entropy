using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using SOAPEndpointMiddleware;

namespace TestApp
{
    public class LoggingInspector : SOAPEndpointMiddleware.IDispatchMessageInspector, IParameterInspector
    {
        public object BeforeCall(string operationName, object[] inputs)
        {
            Console.WriteLine($"Received request to invoke {operationName} with {inputs.Length} parameters");
            return null;
        }

        public void AfterCall(string operationName, object[] outputs, object returnValue, object correlationState) { }

        public object AfterReceiveRequest(ref Message request)
        {
            Console.WriteLine();
            Console.WriteLine($"Received request with the following headers:");
            foreach (var header in request.Headers) { Console.WriteLine($" - {header.Name}"); }
            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState) { }
    }

    public class LogRequestsAttribute : Attribute, IOperationBehavior, IServiceBehavior
    {
        public void ApplyDispatchBehavior(ServiceDescription service) => service.MessageInspectors.Add(new LoggingInspector());
        public void ApplyDispatchBehavior(OperationDescription operation) => operation.ParameterInspectors.Add(new LoggingInspector());
    }
}
