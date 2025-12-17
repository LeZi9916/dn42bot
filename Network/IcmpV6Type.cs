using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Network;

public enum IcmpV6Type : ushort
{
    // Type 0 (Reserved)
    Reserved = 0x0000,

    // Type 1 - Destination Unreachable
    DestinationUnreachable_NoRoute = 0x0100,
    DestinationUnreachable_AdminProhibited = 0x0101,
    DestinationUnreachable_BeyondScope = 0x0102,
    DestinationUnreachable_AddressUnreachable = 0x0103,
    DestinationUnreachable_PortUnreachable = 0x0104,
    DestinationUnreachable_SrcPolicyFail = 0x0105,
    DestinationUnreachable_RejectRoute = 0x0106,
    DestinationUnreachable_ErrorInSourceRouteHdr = 0x0107,
    DestinationUnreachable_HeadersTooLong = 0x0108,

    // Type 2 - Packet Too Big
    PacketTooBig = 0x0200,

    // Type 3 - Time Exceeded
    TimeExceeded_HopLimitExceeded = 0x0300,
    TimeExceeded_ReassemblyTimeout = 0x0301,

    // Type 4 - Parameter Problem
    ParamProblem_ErroneousHeader = 0x0400,
    ParamProblem_UnrecognizedNextHeader = 0x0401,
    ParamProblem_UnrecognizedOption = 0x0402,
    ParamProblem_IncompleteHeaderChain = 0x0403,
    ParamProblem_SRUpperLayerHeaderError = 0x0404,
    ParamProblem_UnrecognizedNextHdrByIntermediate = 0x0405,
    ParamProblem_ExtensionHeaderTooBig = 0x0406,
    ParamProblem_ExtHdrChainTooLong = 0x0407,
    ParamProblem_TooManyExtHeaders = 0x0408,
    ParamProblem_TooManyOptions = 0x0409,
    ParamProblem_OptionTooBig = 0x040A,

    // Informational (128+)

    // Echo
    EchoRequest = 0x8000,
    EchoReply = 0x8100,

    // Multicast Listener Discovery (MLD)
    MulticastListenerQuery = 0x8200,
    MulticastListenerReport = 0x8300,
    MulticastListenerDone = 0x8400,

    // Neighbor Discovery (NDP)
    RouterSolicitation = 0x8500,
    RouterAdvertisement = 0x8600,
    NeighborSolicitation = 0x8700,
    NeighborAdvertisement = 0x8800,
    RedirectMessage = 0x8900,

    // Additional ICMPv6 informational types
    RouterRenumbering = 0x8A00,
    NodeInfoQuery = 0x8B00,
    NodeInfoResponse = 0x8C00,
    InverseNeighborDiscoverySolicit = 0x8D00,
    InverseNeighborDiscoveryAdvert = 0x8E00,
    Version2MulticastListenerReport = 0x8F00,
    HomeAgentAddressDiscoveryRequest = 0x9000,
    HomeAgentAddressDiscoveryReply = 0x9100,
    MobilePrefixSolicitation = 0x9200,
    MobilePrefixAdvertisement = 0x9300,
    CertificationPathSolicitation = 0x9400,
    CertificationPathAdvertisement = 0x9500,
    ICMPv6MobilityMsgs = 0x9600, // e.g., experimental mobility (RFC 4065)
    MulticastRouterAdvertisement = 0x9700,
    MulticastRouterSolicitation = 0x9800,
    MulticastRouterTermination = 0x9900,
    FMIPv6Messages = 0x9A00,
    RPLControlMessage = 0x9B00,
    ILNPv6LocatorUpdate = 0x9C00,
    DuplicateAddressRequest = 0x9D00,
    DuplicateAddressConfirmation = 0x9E00,
    MPLControlMessage = 0x9F00,
    ExtendedEchoRequest = 0xA000,
    ExtendedEchoReply = 0xA100,

    // Experimental / Private
    PrivateExperiment100 = 0x6400,
    PrivateExperiment101 = 0x6500,
    PrivateExperiment200 = 0xC800,
    PrivateExperiment201 = 0xC900,

    // Reserved for expansion
    ReservedError127 = 0x7F00,
    ReservedInfo255 = 0xFF00,
}
