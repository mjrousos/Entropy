Custom SOAP Middleware Sample
=============================

This sample demonstrates how developers can create custom middleware to extend how ASP.NET Core handles incoming HTTP requests. More information on custom middleware can be found in [the ASP.NET Core docs](https://docs.asp.net/en/latest/fundamentals/middleware.html).

This particular sample demonstrates custom middleware by handling SOAP requests. The middleware deserializes the SOAP body, finds an appropriate service operation (via reflection), dispatches the call, a serializes the returned object into a SOAP response.

### A Disclaimer ###
Hopefully this sample provides a useful demonstration of creating custom middleware for ASP.NET Core in a real-world scenario. Some users might also find the SOAP handling itself useful for processing requests from old clients that previously communicated with a WCF endpoint. Be aware, though, that **this sample does not provide general WCF host support for ASP.NET Core**. Among other things, it has no support for message security, WSDL generation, duplex channels, non-HTTP transports, etc. The recommended way of providing web services with ASP.NET Core is via RESTful web API solutions. The ASP.NET [MVC](https://github.com/aspnet/Mvc) framework provides a powerful and flexible model for routing and handling web requests with controllers and actions.
    