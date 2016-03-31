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
            // Check whether the request has come to the path associated with the service endpoint
            if (httpContext.Request.Path.Equals(_endpointPath, StringComparison.Ordinal))
            {
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

                Message responseMessage = null;

                // If a response is expected, create it
                if (!operation.IsOneWay)
                {
                    // Create response message
                    var resultName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
                    var bodyWriter = new ServiceBodyWriter(operation.Contract.Namespace, operation.Name + "Response", resultName, responseObject);
                    responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, operation.ReplyAction, bodyWriter);

                    // Apply context outgoing headers and properties
                    AddOperationContextHeaders(responseMessage);
                }

                // Run message inspectors on the outgoing message
                // As per MSDN docs, they should run even if the message is null
                int messageInspectorIndex = 0;
                foreach (var messageInspector in operation.Contract.Service.MessageInspectors)
                {
                    messageInspector.BeforeSendReply(ref responseMessage, messageInspectorCorrelationStates[messageInspectorIndex++]);
                }

                if (responseMessage != null)
                {
                    // If a response was produced, write it to the HTTP context's response
                    httpContext.Response.ContentType = _messageEncoder.ContentType;
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

#if STRICT_PARAMETER_MATCHING
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var parameter = parameters[i];
                        var parameterName = parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameter.Name;
                        xmlReader.MoveToStartElement(parameterName, operation.Contract.Namespace);
                        if (xmlReader.IsStartElement(parameterName, operation.Contract.Namespace)) 
                        {
                            var serializer = new DataContractSerializer(parameter.ParameterType, parameterName, operation.Contract.Namespace);
                            arguments.Add(serializer.ReadObject(xmlReader, verifyObjectName: true));
                        }
                    }
#else // STRICT_PARAMETER_MATCHING
                // Prepopulate arguments with default values (in case not all are specified)
                foreach (ParameterInfo p in parameters)
                {
                    arguments.Add(p.HasDefaultValue ? p.DefaultValue : null);
                }

                // Iterate through arguments
                // Note that this doesn't assume parameters appear in a particular order.
                // .NET WCF serialization is consistent in its argument ordering, but 
                // some other SOAP serializers are not.
                xmlReader.MoveToElement();
                while (xmlReader.IsStartElement())
                {
                    var parameter = GetParameter(parameters, xmlReader.LocalName);
                    if (parameter != null)
                    {
                        var serializer = new DataContractSerializer(parameter.ParameterType);
                        // Note - This currently doesn't check namespace. If that turns out to be too loose, the parameter.Name and serviceNamespace
                        //        could be passed here.
                        arguments[parameter.Position] = serializer.ReadObject(xmlReader, false);
                    }
                    else
                    {
                        xmlReader.ReadOuterXml();
                    }
                }
#endif // STRICT_PARAMETER_MATCHING
            }

            return arguments.ToArray();
        }

        private void AddOperationContextHeaders(Message responseMessage)
        {
            var context = OperationContext.Current;
            if (context?.HasOutgoingMessageHeaders ?? false)
            {
                // If headers were specified in the operation context, apply them
                responseMessage.Headers.CopyHeadersFrom(context.OutgoingMessageHeaders);
            }
            if (context?.HasOutgoingMessageProperties ?? false)
            {
                // Similarly add/update properties based on data in the operation context
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

        // In the case of loose argument/parameter matching, this method will (generously) attempt to match
        // a given string to a parameter from a service's operation method.
        private ParameterInfo GetParameter(ParameterInfo[] parameters, string name)
        {
            // Check for [MessageParameter(Name=...)] attribute
            var ret = parameters.Where(p => p.GetCustomAttribute<MessageParameterAttribute>()?.Name.Equals(name, StringComparison.Ordinal) ?? false).FirstOrDefault();

            // Check parameter name
            if (ret == null)
            {
                ret = parameters.Where(p => p.Name.Equals(name, StringComparison.Ordinal)).FirstOrDefault();
            }

            // Check for [MessageParameter(Name=...)] attribute (ignoring case)
            if (ret == null)
            {
                ret = parameters.Where(p => p.GetCustomAttribute<MessageParameterAttribute>()?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false).FirstOrDefault();
            }

            // Check parameter name (ignoring case)
            if (ret == null)
            {
                ret = parameters.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            }

            return ret;
        }
    }
}
