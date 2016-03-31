// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ServiceModel;

namespace TestApp
{
    // Sample contract implementation
    [LogRequests] // Service behavior
    public class CalculatorService : ICalculatorService
    {
        [LogRequests]
        public double Add(double x, double y) => x + y;
        [LogRequests]
        public double Divide(double x, double y) => x / y;
        [LogRequests]
        public double Multiply(double x, double y) => x * y;
        [LogRequests]
        public double Subtract(double x, double y) => x - y;
    }

    // Sample contract
    [ServiceContract]
    public interface ICalculatorService
    {
        [OperationContract]
        double Add(double x, double y);
        [OperationContract]
        double Subtract(double x, double y);
        [OperationContract]
        double Multiply(double x, double y);
        [OperationContract]
        double Divide(double x, double y);
    }
}
