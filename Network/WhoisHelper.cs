using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Whois.NET;

namespace dn42Bot.Network;

internal class WhoisHelper
{
    const string WHOIS_SERVER_IPENDPOINT = "172.22.137.116";
    readonly static WhoisQueryOptions WHOIS_QUERY_OPTIONS = new()
    {
        Server = WHOIS_SERVER_IPENDPOINT,
    };
    public static async Task<WhoisQueryResult<uint>> QueryASNFromIPAddressAsync(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(address));
        var addressStr = address.ToString();
        var queryResult = await WhoisClient.QueryAsync(addressStr, WHOIS_QUERY_OPTIONS);
        if(queryResult is null)
        {
            return new()
            {
                IsSuccessfully = false,
                Result = 0
            };
        }
        var rawRsp = queryResult.Raw;
        var routeObjectStartAt = rawRsp.LastIndexOf("route:");
        if(routeObjectStartAt is -1)
        {
            routeObjectStartAt = rawRsp.LastIndexOf("route6:");
        }
        if(routeObjectStartAt is not -1)
        {
            var originStartAt = rawRsp.IndexOf("origin:", routeObjectStartAt);
            if(originStartAt is not -1)
            {
                originStartAt += "origin:".Length;
                var originEndAt = rawRsp.IndexOf('\n', originStartAt);
                if(originEndAt is -1)
                {
                    originEndAt = rawRsp.Length;
                }
                var originStr = rawRsp.AsSpan()[originStartAt..originEndAt].Trim();
                if(originStr.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
                {
                    originStr = originStr[2..];
                }
                if(uint.TryParse(originStr, out var asn))
                {
                    return new()
                    {
                        IsSuccessfully = true,
                        Result = asn
                    };
                }
            }
        }
        // route object not found
        return new()
        {
            IsSuccessfully = false,
            Result = 0
        };
    }
}
