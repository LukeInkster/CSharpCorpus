﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;

namespace StackExchange.Opserver.Data.CloudFlare
{
    public partial class CloudFlareAPI
    {
        private Cache<List<CloudFlareZone>> _zones;
        public Cache<List<CloudFlareZone>> Zones
        {
            get
            {
                return _zones ?? (_zones = new Cache<List<CloudFlareZone>>
                {
                    CacheForSeconds = 5*60,
                    UpdateCache = CloudFlareFetch(nameof(Zones), api => api.Get<List<CloudFlareZone>>("zones"))
                });
            }
        }

        private static readonly NameValueCollection _dnsRecordFetchParams = new NameValueCollection
        {
            {"per_page", "100"}
        };
        
        private Cache<List<CloudFlareDNSRecord>> _dnsRecords;
        public Cache<List<CloudFlareDNSRecord>> DNSRecords
        {
            get
            {
                return _dnsRecords ?? (_dnsRecords = new Cache<List<CloudFlareDNSRecord>>
                {
                    CacheForSeconds = 5*60,
                    UpdateCache = CloudFlareFetch(nameof(DNSRecords),
                        async api =>
                        {
                            var records = new List<CloudFlareDNSRecord>();
                            await Zones.PollAsync(); // wait on zones to load first...
                            var data = Zones.Data;
                            if (data == null) return records;
                            foreach (var z in data)
                            {
                                var zoneRecords = await api.Get<List<CloudFlareDNSRecord>>($"zones/{z.Id}/dns_records", _dnsRecordFetchParams).ConfigureAwait(false);
                                records.AddRange(zoneRecords);
                            }
                            return records;
                        })
                });
            }
        }

        public CloudFlareZone GetZoneFromHost(string host) => Zones.Data?.FirstOrDefault(z => host.EndsWith(z.Name));

        public CloudFlareZone GetZoneFromUrl(string url)
        {
            Uri uri;
            return !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri) ? null : GetZoneFromHost(uri.Host);
        }

        /// <summary>
        /// Get the IP Addresses for a given fully qualified host (star records not supported), even through CNAME chains
        /// </summary>
        /// <param name="host">The host to get an IP for</param>
        /// <returns>Root IP Addresses for this host</returns>
        public List<IPAddress> GetIPs(string host)
        {
            if (DNSRecords.Data == null) 
                return null;
            var records = DNSRecords.Data.Where(r => string.Equals(host, r.Name, StringComparison.InvariantCultureIgnoreCase) && (r.Type == DNSRecordType.A || r.Type == DNSRecordType.AAAA || r.Type == DNSRecordType.CNAME)).ToList();
            var cNameRecord = records.FirstOrDefault(r => r.Type == DNSRecordType.CNAME);
            return cNameRecord != null ? GetIPs(cNameRecord.Content) : records.Select(r => r.IPAddress).ToList();
        }
    }
}
