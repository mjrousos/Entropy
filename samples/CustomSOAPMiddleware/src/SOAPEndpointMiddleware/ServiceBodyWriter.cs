// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;

namespace SOAPEndpointMiddleware
{
    public class ServiceBodyWriter : BodyWriter
    {
        private string _serviceNamespace;
        private string _envelopeName;
        private string _resultName;
        private object _result;

        public ServiceBodyWriter(string serviceNamespace, string envelopeName, string resultName, object result) : base(isBuffered: true)
        {
            _serviceNamespace = serviceNamespace;
            _envelopeName = envelopeName;
            _resultName = resultName;
            _result = result;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(_envelopeName, _serviceNamespace);
            var serializer = new DataContractSerializer(_result.GetType(), _resultName, _serviceNamespace);
            serializer.WriteObject(writer, _result);
            writer.WriteEndElement();
        }
    }
}
