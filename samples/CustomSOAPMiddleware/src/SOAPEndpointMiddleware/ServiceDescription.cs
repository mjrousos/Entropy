// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;

namespace SOAPEndpointMiddleware
{
    public class ServiceDescription
    {
        public Type ServiceType { get; private set; }
        public IEnumerable<ContractDescription> Contracts { get; private set; }
        public IEnumerable<OperationDescription> Operations => Contracts.SelectMany(c => c.Operations);
        public IList<IDispatchMessageInspector> MessageInspectors { get; internal set; }

        public ServiceDescription(Type serviceType)
        {
            ServiceType = serviceType;

            var contracts = new List<ContractDescription>();

            // Iterate interfaces implemented by the service type looking for [ServiceContract] interfaces
            foreach (var contractType in ServiceType.GetInterfaces())
            {
                foreach (var serviceContractAttribute in contractType.GetTypeInfo().GetCustomAttributes<ServiceContractAttribute>())
                {
                    contracts.Add(new ContractDescription(this, contractType, serviceContractAttribute));
                }
            }

            Contracts = contracts;

            ApplyBehaviors();
        }

        internal void ApplyBehaviors()
        {
            MessageInspectors = new List<IDispatchMessageInspector>();

            // Find all IServiceBehavior attributes and apply them
            foreach (var behavior in ServiceType.GetTypeInfo().GetCustomAttributes().Select(attr => attr as IServiceBehavior))
            {
                behavior?.ApplyDispatchBehavior(this);
            }

            // Allow operations to apply method-level behaviors
            foreach (var operation in Operations)
            {
                operation.ApplyBehaviors();
            }
        }
    }

    public class ContractDescription
    {
        public ServiceDescription Service { get; private set; }
        public string Name { get; private set; }
        public string Namespace { get; private set; }
        public Type ContractType { get; private set; }
        public IEnumerable<OperationDescription> Operations { get; private set; }

        public ContractDescription(ServiceDescription service, Type contractType, ServiceContractAttribute attribute)
        {
            Service = service;
            ContractType = contractType;
            Namespace = attribute.Namespace ?? "http://tempuri.org/"; // Namespace defaults to http://tempuri.org
            Name = attribute.Name ?? ContractType.Name; // Name defaults to the type name

            var operations = new List<OperationDescription>();
            foreach (var operationMethodInfo in ContractType.GetTypeInfo().DeclaredMethods)
            {
                foreach (var operationContract in operationMethodInfo.GetCustomAttributes<OperationContractAttribute>())
                {
                    operations.Add(new OperationDescription(this, operationMethodInfo, operationContract));
                }
            }
            Operations = operations;
        }
    }

    public class OperationDescription
    {
        public ContractDescription Contract { get; private set; }
        public string SoapAction { get; private set; }
        public string ReplyAction { get; private set; }
        public string Name { get; private set; }
        public MethodInfo DispatchMethod { get; private set; }
        public bool IsOneWay { get; private set; }
        public IList<IParameterInspector> ParameterInspectors { get; private set; }

        public OperationDescription(ContractDescription contract, MethodInfo operationMethod, OperationContractAttribute contractAttribute)
        {
            Contract = contract;
            Name = contractAttribute.Name ?? operationMethod.Name;
            SoapAction = contractAttribute.Action ?? $"{contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name}";
            IsOneWay = contractAttribute.IsOneWay;
            ReplyAction = contractAttribute.ReplyAction;
            DispatchMethod = operationMethod;
        }

        internal void ApplyBehaviors()
        {
            ParameterInspectors = new List<IParameterInspector>();

            // Retrieve a mapping of contract interface methods to actual implementations in the service type
            var interfaceMap = Contract.Service.ServiceType.GetTypeInfo().GetRuntimeInterfaceMap(Contract.ContractType);
            MethodInfo serviceTypeMethod = null;

            // Locate the index in the map for this operation and get the corresponding implementation method
            for (int i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
            {
                if (interfaceMap.InterfaceMethods[i].ToString().Equals(DispatchMethod.ToString(), StringComparison.Ordinal))
                {
                    serviceTypeMethod = interfaceMap.TargetMethods[i];
                    break;
                }
            }

            if (serviceTypeMethod != null)
            {
                // Get IOperationBehaviors for the method
                var behaviors = serviceTypeMethod.GetCustomAttributes()
                    .Select(attr => attr as IOperationBehavior)
                    .Where(behavior => behavior != null);

                // Allow each to apply its behaviors
                foreach (var behavior in behaviors)
                {
                    behavior.ApplyDispatchBehavior(this);
                }
            }
        }
    }
}
