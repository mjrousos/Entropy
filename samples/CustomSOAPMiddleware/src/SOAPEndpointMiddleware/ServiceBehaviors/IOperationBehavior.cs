namespace SOAPEndpointMiddleware
{
    public interface IOperationBehavior
    {
        void ApplyDispatchBehavior(OperationDescription operation);
    }
}