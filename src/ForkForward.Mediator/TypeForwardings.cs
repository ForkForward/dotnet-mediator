using System.Runtime.CompilerServices;
using ForkForward.Mediator;

[assembly: TypeForwardedTo(typeof(IBaseRequest))]
[assembly: TypeForwardedTo(typeof(IRequest<>))]
[assembly: TypeForwardedTo(typeof(IRequest))]
[assembly: TypeForwardedTo(typeof(INotification))]
[assembly: TypeForwardedTo(typeof(Unit))]