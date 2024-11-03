using System;
using System.Collections.Generic;

namespace NileLibraryNS.Models
{
    public class NileLibraryFile
    {
        public List<NileGames> games { get; set; } = new List<NileGames>();

        public class NileGames
        {
            public string __type { get; set; }
            public string entitlementDateFromEpoch { get; set; }
            public string id { get; set; }
            public float lastModifiedDate { get; set; }
            public Product product { get; set; }
            public string signature { get; set; }
            public string state { get; set; }
        }

        public class Product
        {
            public int asinVersion { get; set; }
            public string description { get; set; }
            public string domainId { get; set; }
            public string id { get; set; }
            public Productdetail productDetail { get; set; }
            public string productLine { get; set; }
            public string sku { get; set; }
            public string title { get; set; }
            public string vendorId { get; set; }
        }

        public class Productdetail
        {
            public Details details { get; set; }
            public string iconUrl { get; set; }
        }

        public class Details
        {
            public string backgroundUrl1 { get; set; }
            public string backgroundUrl2 { get; set; }
            public string developer { get; set; }
            public string esrbRating { get; set; }
            public string[] gameModes { get; set; }
            public string[] genres { get; set; }
            public string[] keywords { get; set; }
            public string logoUrl { get; set; }
            public string[] otherDevelopers { get; set; }
            public string pegiRating { get; set; }
            public string pgCrownImageUrl { get; set; }
            public string publisher { get; set; }
            public DateTime releaseDate { get; set; }
            public object[] screenshots { get; set; }
            public string shortDescription { get; set; }
            public string uskRating { get; set; }
            public Websites websites { get; set; }
        }

        public class Websites
        {
            public string TWITCH { get; set; }
            public string FACEBOOK { get; set; }
            public string OFFICIAL { get; set; }
            public string INSTAGRAM { get; set; }
            public string REDDIT { get; set; }
            public string STEAM { get; set; }
            public string TWITTER { get; set; }
            public string GOG { get; set; }
        }
    }
}
