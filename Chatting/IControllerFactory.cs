using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Chatting;

internal interface IControllerFactory
{
    abstract static IController FromRequest(UserRequest request, ChatRoom chatRoom);
}
