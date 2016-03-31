using System.ServiceModel;

namespace TestApp
{
    // Sample contract implementation
    [LogRequests] // Service behavior
    public class CalculatorService : ICalculatorService
    {
        [LogRequests] public double Add(double x, double y) => x + y;
        [LogRequests] public double Divide(double x, double y) => x / y;
        [LogRequests] public double Multiply(double x, double y) => x * y;
        [LogRequests] public double Subtract(double x, double y) => x - y;
    }

    // Sample contract
    [ServiceContract]
    public interface ICalculatorService
    {
        [OperationContract] double Add(double x, double y);
        [OperationContract] double Subtract(double x, double y);
        [OperationContract] double Multiply(double x, double y);
        [OperationContract] double Divide(double x, double y);
    }
}
