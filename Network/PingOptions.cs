using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

internal struct PingOptions
{
    public short Ttl
    {
        get
        {
            return field;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 64;
    public bool DontFragment { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(2000);
    public PingOptions()
    {

    }
}
