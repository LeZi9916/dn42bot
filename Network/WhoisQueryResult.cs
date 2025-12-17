using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace dn42Bot.Network;

internal readonly struct WhoisQueryResult<T>
{
    [MemberNotNullWhen(true, "Result")]
    public required bool IsSuccessfully { get; init; }
    public required T Result { get; init; }
}
