// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace SOAPEndpointMiddleware
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class SOAPEndpointMiddleware
    {
        // The middleware delegate to call after this one finishes processing
        private readonly RequestDelegate _next;
        private readonly string _endpointPath;
        private readonly MessageEncoder _messageEncoder;

        private readonly ServiceDescription _service;

        public SOAPEndpointMiddleware(RequestDelegate next, Type serviceType, string path, MessageEncoder encoder)
        {
            _next = next;
            _endpointPath = path;
            _messageEncoder = encoder;
            _service = new ServiceDescription(serviceType);
        }

        public async Task Invoke(HttpContext httpContext, IServiceProvider serviceProvider)
        {
            if (httpContext.Request.Path.Equals(_endpointPath, StringComparison.Ordinal))
            {
                Message responseMessage;

                // Read request message
                var requestMessage = _messageEncoder.ReadMessage(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);
                var soapAction = httpContext.Request.Headers["SOAPAction"].ToString().Trim('\"');
                if (!string.IsNullOrEmpty(soapAction))
                {
                    requestMessage.Headers.Action = soapAction;
                }

                // Setup operation context and populate the remote endpoint property which some services may use
                OperationContext.Current = new OperationContext(requestMessage);
                // Check for null values in RemoteIpAddress since this isn't working in RC1. Will be fixed for RC2.
                var remoteEndpoint = new RemoteEndpointMessageProperty(httpContext.Connection?.RemoteIpAddress?.ToString() ?? "::1", httpContext.Connection.RemotePort);
                OperationContext.Current.IncomingMessageProperties.Add(RemoteEndpointMessageProperty.Name, remoteEndpoint);

                // Find the requested action/operation
                var operation = _service.Operations.Where(o => o.SoapAction.Equals(requestMessage.Headers.Action, StringComparison.Ordinal)).FirstOrDefault();
                if (operation == null)
                {
                    throw new InvalidOperationException($"No operation found for specified action: {requestMessage.Headers.Action}");
                }

                // Get service type
                var serviceInstance = serviceProvider.GetService(_service.ServiceType);

                // Get operation arguments from message
                var arguments = GetRequestArguments(requestMessage, operation);

                // Run message inspectors on the incoming message
                List<object> messageInspectorCorrelationStates = new List<object>();
                foreach (var messageInspector in operation.Contract.Service.MessageInspectors)
                {
                    messageInspectorCorrelationStates.Add(messageInspector.AfterReceiveRequest(ref requestMessage));
                }

                // Run parameter inspectors on the incoming message
                List<object> parameterInspectorCorrelationStates = new List<object>();
                foreach (var parameterInspector in operation.ParameterInspectors)
                {
                    parameterInspectorCorrelationStates.Add(parameterInspector.BeforeCall(operation.Name, arguments));
                }

                // Invoke Operation method
                var responseObject = operation.DispatchMethod.Invoke(serviceInstance, arguments.ToArray());

                // Run parameter inspectors on the dispatch results
                int paramInspectorIndex = 0;
                foreach (var parameterInspector in operation.ParameterInspectors)
                {
                    parameterInspector.AfterCall(operation.Name, arguments, responseObject, parameterInspectorCorrelationStates[paramInspectorIndex++]);
                }

                if (operation.IsOneWay)
                {
                    responseMessage = null;
                }
                else
                {
                    // Create response message
                    var resultName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
                    var bodyWriter = new ServiceBodyWriter(operation.Contract.Namespace, operation.Name + "Response", resultName, responseObject);
                    responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, operation.ReplyAction, bodyWriter);

                    // Apply context outgoing headers and properties
                    AddOperationContextHeaders(responseMessage);

                    // Run message inspectors on the outgoing message
                    int messageInspectorIndex = 0;
                    foreach (var messageInspector in operation.Contract.Service.MessageInspectors)
                    {
                        messageInspector.BeforeSendReply(ref responseMessage, messageInspectorCorrelationStates[messageInspectorIndex++]);
                    }

                    httpContext.Response.ContentType = httpContext.Request.ContentType; // _messageEncoder.ContentType;
                    httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

                    _messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);
                }

                // Cleanup
                OperationContext.Current.Recycle();
                requestMessage.Close();
            }
            else
            {
                // If the path doesn't match, allow other pipeline middleware to process the request
                await _next(httpContext);
            }
        }

        private object[] GetRequestArguments(Message requestMessage, OperationDescription operation)
        {
            var parameters = operation.DispatchMethod.GetParameters();
            var arguments = new List<object>();

            // Deserialize request wrapper and object
            using (var xmlReader = requestMessage.GetReaderAtBodyContents())
            {
                // Find the element for the operation's data
                xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameterName = parameters[i].GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameters[i].Name;
                    xmlReader.MoveToStartElement(parameterName, operation.Contract.Namespace);
                    if (xmlReader.IsStartElement(parameterName, operation.Contract.Namespace))
                    {
                        var serializer = new DataContractSerializer(parameters[i].ParameterType, parameterName, operation.Contract.Namespace);
                        arguments.Add(serializer.ReadObject(xmlReader, verifyObjectName: true));
                    }
                }
            }

            return arguments.ToArray();
        }

        private void AddOperationContextHeaders(Message responseMessage)
        {
            var context = OperationContext.Current;
            if (context?.HasOutgoingMessageHeaders ?? false)
            {
                responseMessage.Headers.CopyHeadersFrom(context.OutgoingMessageHeaders);
            }
            if (context?.HasOutgoingMessageProperties ?? false)
            {
                foreach (var property in context.OutgoingMessageProperties)
                {
                    if (responseMessage.Properties.ContainsKey(property.Key))
                    {
                        responseMessage.Properties[property.Key] = property.Value;
                    }
                    else
                    {
                        responseMessage.Properties.Add(property.Key, property.Value);
                    }
                }
                responseMessage.Properties.Via = context.OutgoingMessageProperties.Via;
                responseMessage.Properties.AllowOutputBatching = context.OutgoingMessageProperties.AllowOutputBatching;
                responseMessage.Properties.Encoder = context.OutgoingMessageProperties.Encoder;
            }
        }
    }
}
