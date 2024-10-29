using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NileLibraryNS.Models
{
    public class DeviceRegistrationRequest
    {
        public class RegistrationData
        {
            public string app_name;
            public string app_version;
            public string device_model;
            public string device_name;
            public string device_serial;
            public string device_type;
            public string domain;
            public string os_version;
        }

        public class AuthData
        {
            public string access_token;
        }

        public class UserContextMap
        {
        }

        public RegistrationData registration_data = new RegistrationData();
        public AuthData auth_data = new AuthData();
        public UserContextMap user_context_map = new UserContextMap();
        public List<string> requested_extensions;
        public List<string> requested_token_type;
    }

    public class DeviceRegistrationResponse
    {
        public class Response
        {
            public class Success
            {
                public class Tokens
                {
                    public Mac_Dms mac_dms { get; set; }
                    public Bearer bearer { get; set; }
                }

                public class Mac_Dms
                {
                    public string device_private_key { get; set; }
                }

                public class Bearer
                {
                    public string access_token { get; set; }
                    public string refresh_token { get; set; }
                    public long expires_in { get; set; }
                }

                public class Extensions
                {
                    public Device_Info device_info { get; set; }
                    public Customer_Info customer_info { get; set; }
                }

                public class Device_Info
                {
                    public string device_name { get; set; }
                    public string device_serial_number { get; set; }
                    public string device_type { get; set; }
                }

                public class Customer_Info
                {
                    public string account_pool { get; set; }
                    public string user_id { get; set; }
                    public string home_region { get; set; }
                    public string name { get; set; }
                    public string given_name { get; set; }
                }

                public Tokens tokens { get; set; }
                public Extensions extensions { get; set; }

                public class Nile
                {
                    public long token_obtain_time { get; set; }
                }
                public string customer_id { get; set; }
                public Nile NILE { get; set; } = new Nile();
            }

            public Success success;
        }

        public Response response;
    }

    public class ProfileInfo
    {
        public string user_id;
    }

    public class TokenRefreshRequest
    {
        public string source_token_type;
        public string requested_token_type;
        public string source_token;
        public string app_name;
        public string app_version;
    }
}
