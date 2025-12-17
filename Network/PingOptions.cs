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
            return _ttl;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ttl = value;
        }
    }
    public bool DontFragment { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(2000);

    short _ttl = 64;
    public PingOptions()
    {

    }
}
