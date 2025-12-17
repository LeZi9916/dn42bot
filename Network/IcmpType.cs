using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

internal enum IcmpType: ushort
{
    // Type 0, Code 0
    EchoReply = 0x0000,
    // Type 8, Code 0
    EchoRequest = 0x0800,

    // Destination Unreachable (Type 3)
    DestinationUnreachable_NetUnreachable = 0x0300,
    DestinationUnreachable_HostUnreachable = 0x0301,
    DestinationUnreachable_ProtocolUnreachable = 0x0302,
    DestinationUnreachable_PortUnreachable = 0x0303,
    DestinationUnreachable_FragmentationNeeded = 0x0304,
    DestinationUnreachable_SourceRouteFailed = 0x0305,
    DestinationUnreachable_DestNetworkUnknown = 0x0306,
    DestinationUnreachable_DestHostUnknown = 0x0307,
    DestinationUnreachable_SourceHostIsolated = 0x0308,
    DestinationUnreachable_CommWithNetworkProhibited = 0x0309,
    DestinationUnreachable_CommWithHostProhibited = 0x030A,
    DestinationUnreachable_DestNetUnreachForTOS = 0x030B,
    DestinationUnreachable_DestHostUnreachForTOS = 0x030C,
    DestinationUnreachable_CommAdministrativelyProhibited = 0x030D,
    DestinationUnreachable_HostPrecedenceViolation = 0x030E,
    DestinationUnreachable_PrecedenceCutoffInEffect = 0x030F,

    // Source Quench (Deprecated)
    SourceQuench = 0x0400, // Type 4, Code 0

    // Redirect (Type 5)
    Redirect_Network = 0x0500,
    Redirect_Host = 0x0501,
    Redirect_TOSAndNetwork = 0x0502,
    Redirect_TOSAndHost = 0x0503,

    // Router Advertisement / Solicitation
    RouterAdvertisement = 0x0900, // Type 9, Code 0
    RouterSolicitation = 0x0A00, // Type 10, Code 0

    // Time Exceeded (Type 11)
    TimeExceeded_TTLExpired = 0x0B00,
    TimeExceeded_ReassemblyTimeout = 0x0B01,

    // Parameter Problem (Type 12)
    ParameterProblem_PointerIndicatesError = 0x0C00,
    ParameterProblem_MissingRequiredOption = 0x0C01,
    ParameterProblem_BadLength = 0x0C02,

    // Timestamp (Deprecated)
    TimestampRequest = 0x0D00,
    TimestampReply = 0x0E00,

    // Information (Deprecated)
    InformationRequest = 0x0F00,
    InformationReply = 0x1000,

    // Address Mask
    AddressMaskRequest = 0x1100,
    AddressMaskReply = 0x1200,
}
