using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Chatting;

public interface IController : IDisposable, IAsyncDisposable, IEquatable<IController>
{
    Guid RequestId { get; }
    DateTime CreateAt { get; }
    DateTime LastActive { get; }
    ControllerStatus State { get; }
    Task ExecuteAsync();
}
