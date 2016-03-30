namespace SOAPEndpointMiddleware
{
    public interface IServiceBehavior
    {
        void ApplyDispatchBehavior(ServiceDescription service);
    }
}