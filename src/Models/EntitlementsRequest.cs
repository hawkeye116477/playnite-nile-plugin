﻿namespace NileLibraryNS.Models
{
    public class EntitlementsRequest
    {
        public string Operation = "GetEntitlements";
        public string clientId = "Sonic";
        public int syncPoint = 0;
        public string nextToken;
        public int maxResults = 500;
        public string keyId;
        public string hardwareHash;
        public string productIdFilter = null;
        public bool disableStateFilter = true;
    }
}
