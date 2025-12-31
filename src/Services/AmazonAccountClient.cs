using NileLibraryNS.Models;
using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using PlayniteExtensions.Common;
using System.Security.Principal;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;

namespace NileLibraryNS.Services
{
    public class AmazonAccountClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private NileLibrary library;
        private const string loginUrl = @"https://www.amazon.com/ap/signin?openid.ns=http://specs.openid.net/auth/2.0&openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select&openid.identity=http://specs.openid.net/auth/2.0/identifier_select&openid.mode=checkid_setup&openid.oa2.scope=device_auth_access&openid.ns.oa2=http://www.amazon.com/ap/ext/oauth/2&openid.oa2.response_type=code&openid.oa2.code_challenge_method=S256&openid.oa2.client_id=device:3733646238643238366332613932346432653737653161663637373636363435234132554d56484f58375550345637&language=en_US&marketPlaceId=ATVPDKIKX0DER&openid.return_to=https://www.amazon.com&openid.pape.max_auth_age=0&openid.assoc_handle=amzn_sonic_games_launcher&pageId=amzn_sonic_games_launcher&openid.oa2.code_challenge=";
        private readonly string tokensPath;
        private string userAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) @amzn/aga-electron-platform/1.0.0 Chrome/78.0.3904.130 Electron/7.1.9 Safari/537.36";
        public static readonly RetryHandler retryHandler = new RetryHandler(new HttpClientHandler());
        public static readonly HttpClient httpClient = new HttpClient(retryHandler);

        public AmazonAccountClient(NileLibrary library)
        {
            this.library = library;
            tokensPath = Nile.TokensPath;
        }

        public void LogOut()
        {
            using var webView = library.PlayniteApi.WebViews.CreateView(new WebViewSettings
            {
                WindowWidth = 580,
                WindowHeight = 700,
            });
            webView.DeleteDomainCookies(".amazon.com");
            FileSystem.DeleteFile(Nile.TokensPath);
            FileSystem.DeleteFile(Nile.EncryptedTokensPath);
        }

        public async Task Login()
        {
            var callbackUrl = string.Empty;
            var codeChallenge = GenerateCodeChallenge();
            using (var webView = library.PlayniteApi.WebViews.CreateView(new WebViewSettings
            {
                WindowWidth = 490,
                WindowHeight = 660,
                UserAgent = userAgent,
            }))
            {
                webView.LoadingChanged += (s, e) =>
                {
                    var url = webView.GetCurrentAddress();
                    if (url.Contains("openid.oa2.authorization_code"))
                    {
                        callbackUrl = url;
                        webView.Close();
                    }
                };

                webView.DeleteDomainCookies(".amazon.com");
                var lurl = loginUrl + EncodeBase64Url(GetSHA256HashByte(codeChallenge));
                webView.Navigate(lurl);
                webView.OpenDialog();
            }

            if (!callbackUrl.IsNullOrEmpty())
            {
                var rediUri = new Uri(callbackUrl);
                var fragments = HttpUtility.ParseQueryString(rediUri.Query);
                var token = fragments["openid.oa2.authorization_code"];
                await Authenticate(token, codeChallenge);
            }
        }

        private async Task Authenticate(string accessToken, string codeChallenge)
        {
            var reqData = new DeviceRegistrationRequest();
            reqData.auth_data.use_global_authentication = false;
            reqData.auth_data.authorization_code = accessToken;
            reqData.auth_data.code_verifier = codeChallenge;
            reqData.auth_data.code_algorithm = "SHA-256";
            reqData.auth_data.client_id = "3733646238643238366332613932346432653737653161663637373636363435234132554d56484f58375550345637";
            reqData.auth_data.client_domain = "DeviceLegacy";

            reqData.registration_data.app_name = "AGSLauncher for Windows";
            reqData.registration_data.app_version = "1.0.0";
            reqData.registration_data.device_model = "Windows";
            reqData.registration_data.device_serial = GetMachineGuid().ToString("N");
            reqData.registration_data.device_type = "A2UMVHOX7UP4V7";
            reqData.registration_data.domain = "Device";
            reqData.registration_data.os_version = Environment.OSVersion.Version.ToString(4);

            reqData.requested_extensions = new List<string> { "customer_info", "device_info" };
            reqData.requested_token_type = new List<string> { "bearer", "mac_dms" };

            var authPostContent = Serialization.ToJson(reqData, true);

            var request = new HttpRequestMessage(HttpMethod.Post, @"https://api.amazon.com/auth/register")
            {
                Content = new StringContent(authPostContent, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("User-Agent", "AGSLauncher/1.0.0");

            try
            {
                using var authResponse = await httpClient.SendAsync(request);
                authResponse.EnsureSuccessStatusCode();
                var authResponseContent = await authResponse.Content.ReadAsStringAsync();
                var authData = Serialization.FromJson<DeviceRegistrationResponse>(authResponseContent);
                if (authData.response?.success != null)
                {
                    bool useEncryptedTokens = true;
                    if (Nile.IsInstalled)
                    {
                        var result = await Cli.Wrap(Nile.ClientExecPath)
                                              .AddCommandToLog()
                                              .WithValidation(CommandResultValidation.None)
                                              .ExecuteBufferedAsync();
                        if (!result.StandardOutput.Contains("secret-user-data"))
                        {
                            useEncryptedTokens = false;
                        }
                    }
                    authData.response.success.NILE.token_obtain_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var finalResponse = Serialization.ToJson(authData.response.success);
                    if (!useEncryptedTokens)
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(tokensPath)))
                        {
                            FileSystem.CreateDirectory(Path.GetDirectoryName(tokensPath));
                        }
                        File.WriteAllText(tokensPath, finalResponse);
                    }
                    else
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(Nile.EncryptedTokensPath)))
                        {
                            FileSystem.CreateDirectory(Path.GetDirectoryName(Nile.EncryptedTokensPath));
                        }
                        Encryption.EncryptToFile(Nile.EncryptedTokensPath,
                                                 finalResponse,
                                                 Encoding.UTF8,
                                                 WindowsIdentity.GetCurrent().User.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to authenticate with Amazon");
            }
        }

        public async Task<List<Entitlement>> GetAccountEntitlements()
        {
            if (!await GetIsUserLoggedIn())
            {
                throw new Exception("User is not authenticated.");
            }

            var entitlements = new List<Entitlement>();
            var tokens = LoadTokens();
            string nextToken = null;
            var reqData = new EntitlementsRequest
            {
                // not sure what key this is but it's some key from Amazon.Fuel.Plugin.Entitlement.dll
                keyId = "d5dc8b8b-86c8-4fc4-ae93-18c0def5314d",
                hardwareHash = Guid.NewGuid().ToString("N")
            };

            do
            {
                reqData.nextToken = nextToken;
                var strCont = new StringContent(Serialization.ToJson(reqData, true), Encoding.UTF8, "application/json");
                strCont.Headers.TryAddWithoutValidation("Expect", "100-continue");
                strCont.Headers.TryAddWithoutValidation("Content-Encoding", "amz-1.0");

                using var request = new HttpRequestMessage(HttpMethod.Post, @"https://gaming.amazon.com/api/distribution/entitlements")
                {
                    Content = strCont
                };
                request.Headers.Add("User-Agent", "com.amazon.agslauncher.win/3.0.9495.3");
                request.Headers.Add("X-Amz-Target", "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
                request.Headers.Add("x-amzn-token", tokens.tokens.bearer.access_token);

                try
                {
                    using var entlsResponse = await httpClient.SendAsync(request);
                    entlsResponse.EnsureSuccessStatusCode();

                    var entlsResponseContent = await entlsResponse.Content.ReadAsStringAsync();
                    var entlsData = Serialization.FromJson<EntitlementsResponse>(entlsResponseContent);
                    nextToken = entlsData?.nextToken;
                    if (entlsData?.entitlements.HasItems() == true)
                    {
                        entitlements.AddRange(entlsData.entitlements);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to get account entitlements");
                }

            } while (!nextToken.IsNullOrEmpty());

            return entitlements;
        }

        public string GetUsername()
        {
            var tokens = LoadTokens();
            var username = "";
            if (tokens != null)
            {
                if (!tokens.extensions.customer_info.given_name.IsNullOrEmpty())
                {
                    username = tokens.extensions.customer_info.given_name;
                }
            }
            return username;
        }

        public DeviceRegistrationResponse.Response.Success LoadTokens()
        {
            if (File.Exists(tokensPath))
            {
                try
                {
                    return Serialization.FromJson<DeviceRegistrationResponse.Response.Success>(FileSystem.ReadFileAsStringSafe(tokensPath));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }
            else if (File.Exists(Nile.EncryptedTokensPath))
            {
                try
                {
                    return Serialization.FromJson<DeviceRegistrationResponse.Response.Success>(Encryption.DecryptFromFile(Nile.EncryptedTokensPath, Encoding.UTF8,
                                                WindowsIdentity.GetCurrent().User.Value));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }
            return null;
        }

        private async Task<DeviceRegistrationResponse.Response.Success> RefreshTokens()
        {
            var tokens = LoadTokens();

            var reqData = new TokenRefreshRequest
            {
                app_name = "AGSLauncher",
                app_version = "3.0.9495.3",
                source_token = tokens.tokens.bearer.refresh_token,
                requested_token_type = "access_token",
                source_token_type = "refresh_token"
            };

            var authPostContent = Serialization.ToJson(reqData, true);
            var strcont = new StringContent(authPostContent, Encoding.UTF8, "application/json");
            strcont.Headers.TryAddWithoutValidation("Expect", "100-continue");

            try
            {
                var authResponse = await httpClient.PostAsync(@"https://api.amazon.com/auth/token",
                                                          strcont);
                var authResponseContent = await authResponse.Content.ReadAsStringAsync();
                var authData = Serialization.FromJson<DeviceRegistrationResponse.Response.Success.Bearer>(authResponseContent);
                tokens.tokens.bearer.access_token = authData.access_token;
                tokens.NILE.token_obtain_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var jsonTokens = Serialization.ToJson(tokens);
                bool useEncryptedTokens = false;
                if (File.Exists(Nile.EncryptedTokensPath))
                {
                    useEncryptedTokens = true;
                }

                if (!useEncryptedTokens)
                {
                    File.WriteAllText(tokensPath, jsonTokens);
                }
                else
                {
                    Encryption.EncryptToFile(Nile.EncryptedTokensPath,
                                             jsonTokens,
                                             Encoding.UTF8,
                                             WindowsIdentity.GetCurrent().User.Value);
                }

            }
            catch (Exception ex)
            {
                logger.Error($"Failed to renew tokens: {ex}");
            }
            return tokens;

        }

        public async Task<bool> GetIsUserLoggedIn()
        {
            var tokens = LoadTokens();
            if (tokens == null)
            {
                return false;
            }

            var tokenLastUpdateTime = File.GetLastWriteTime(Nile.TokensPath);
            var tokenExpirySeconds = tokens.tokens.bearer.expires_in;
            DateTime tokenExpiryTime = tokenLastUpdateTime.AddSeconds(tokenExpirySeconds);

            if (DateTime.Now > tokenExpiryTime)
            {
                tokens = await RefreshTokens();
            }
            try
            {
                var infoRequest = new HttpRequestMessage(HttpMethod.Get, @"https://api.amazon.com/user/profile");
                infoRequest.Headers.Add("User-Agent", "AGSLauncher/1.0.0");
                infoRequest.Headers.Add("Authorization", "bearer " + tokens.tokens.bearer.access_token);
                infoRequest.Headers.Add("Accept", "application/json");
                using var infoResponse = await httpClient.SendAsync(infoRequest);
                var infoResponseContent = await infoResponse.Content.ReadAsStringAsync();
                var infoData = Serialization.FromJson<ProfileInfo>(infoResponseContent);
                return !infoData.user_id.IsNullOrEmpty();
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to check Amazon login status. Error: {ex}");
                return false;
            }
        }

        public static Guid GetMachineGuid()
        {
            RegistryKey root = null;
            if (Environment.Is64BitOperatingSystem)
            {
                root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            else
            {
                root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }

            try
            {
                using (var cryptography = root.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography"))
                {
                    return Guid.Parse((string)cryptography.GetValue("MachineGuid"));
                }
            }
            finally
            {
                root.Dispose();
            }
        }

        private string EncodeBase64Url(byte[] input)
        {
            return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static byte[] GetSHA256HashByte(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }

        private string GenerateCodeChallenge()
        {
            var randomStringChars = "ABCDEFGHIJKLMNOPQRSTYVWXZabcdefghijklmnopqrstyvwxz0123456789_";
            var randomSetLeng = randomStringChars.Length - 1;
            var random = new Random();
            var result = new StringBuilder(45);
            for (int i = 0; i < 45; i++)
            {
                result.Append(randomStringChars[random.Next(0, randomSetLeng)]);
            }

            return result.ToString();
        }
    }
}
