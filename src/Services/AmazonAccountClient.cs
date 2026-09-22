using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using CommonPlugin;
using Microsoft.Win32;
using NileLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;

namespace NileLibraryNS.Services
{
    public class AmazonAccountClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private NileLibrary library;
        private const string loginUrl = @"https://www.amazon.com/ap/signin";
        private readonly string userInfoPath;

        private string LoginUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) @amzn/aga-electron-platform/1.0.0 Chrome/78.0.3904.130 Electron/7.1.9 Safari/537.36";

        public static readonly RetryHandler retryHandler = new RetryHandler(new HttpClientHandler());
        public static readonly HttpClient httpClient = new HttpClient(retryHandler);
        private const string LauncherUserAgent = "com.amazon.agslauncher.win/3.0.9782.3";

        public AmazonAccountClient(NileLibrary library)
        {
            this.library = library;
            userInfoPath = Nile.UserInfoPath;
        }

        public void LogOut()
        {
            using var webView = library.PlayniteApi.WebViews.CreateView(new WebViewSettings
            {
                WindowWidth = 580,
                WindowHeight = 700,
            });
            webView.DeleteDomainCookies(".amazon.com");
            var nileUserInfo = GetNileUserInfo();
            if (!nileUserInfo.user_id.IsNullOrEmpty())
            {
                var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(nileUserInfo.user_id)}.enc");
                FileSystem.DeleteFile(tokensPath);
                FileSystem.DeleteFile(Nile.UserInfoPath);
            }

            FileSystem.DeleteFile(Nile.OldEncryptedTokensPath);
        }

        public async Task Login()
        {
            var callbackUrl = string.Empty;
            var codeChallenge = GenerateCodeChallenge();
            var deviceSerial = GetMachineGuid().ToString("N");
            var clientId = BitConverter.ToString(Encoding.ASCII.GetBytes($"{deviceSerial}#A2UMVHOX7UP4V7"))
                                       .ToLowerInvariant()
                                       .Replace("-", "");
            using (var webView = library.PlayniteApi.WebViews.CreateView(new WebViewSettings
                   {
                       WindowWidth = 490,
                       WindowHeight = 660,
                       UserAgent = LoginUserAgent,
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
                var query = HttpUtility.ParseQueryString("");
                query["openid.ns"] = "http://specs.openid.net/auth/2.0";
                var openidIdentity = "http://specs.openid.net/auth/2.0/identifier_select";
                query["openid.claimed_id"] = openidIdentity;
                query["openid.identity"] = openidIdentity;
                query["openid.mode"] = "checkid_setup";
                query["openid.oa2.scope"] = "device_auth_access";
                query["openid.ns.oa2"] = "http://www.amazon.com/ap/ext/oauth/2";
                query["openid.oa2.response_type"] = "code";
                query["openid.oa2.code_challenge_method"] = "S256";
                query["openid.oa2.client_id"] = $"device:{clientId}";
                query["openid.oa2.code_challenge"] = EncodeBase64Url(codeChallenge.GetSHA256HashByte());
                query["language"] = "en_US";
                query["marketPlaceId"] = "ATVPDKIKX0DER";
                query["openid.return_to"] = "https://www.amazon.com";
                query["openid.pape.max_auth_age"] = "0";
                query["openid.ns.pape"] = "http://specs.openid.net/extensions/pape/1.0";
                var openidAssocHandle = "amzn_sonic_games_launcher";
                query["openid.assoc_handle"] = openidAssocHandle;
                query["pageId"] = openidAssocHandle;
                var lurl = $"{loginUrl}?{query}";
                webView.Navigate(lurl);
                webView.OpenDialog();
            }

            if (!callbackUrl.IsNullOrEmpty())
            {
                var rediUri = new Uri(callbackUrl);
                var fragments = HttpUtility.ParseQueryString(rediUri.Query);
                var token = fragments["openid.oa2.authorization_code"];
                await Authenticate(token, codeChallenge, clientId);
            }
        }

        private async Task Authenticate(string accessToken, string codeChallenge, string clientId)
        {
            var reqData = new DeviceRegistrationRequest
            {
                auth_data =
                {
                    authorization_code = accessToken,
                    client_domain = "DeviceLegacy",
                    client_id = clientId,
                    code_algorithm = "SHA-256",
                    code_verifier = codeChallenge,
                    use_global_authentication = false,
                },
                registration_data =
                {
                    app_name = "AGSLauncher for Windows",
                    app_version = "1.0.0",
                    device_model = "Windows",
                    device_serial = GetMachineGuid().ToString("N"),
                    device_type = "A2UMVHOX7UP4V7",
                    domain = "Device",
                    os_version = Environment.OSVersion.Version.ToString(4)
                },
                requested_extensions = new List<string> { "customer_info", "device_info" },
                requested_token_type = new List<string> { "bearer", "mac_dms" }
            };

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
                    authData.response.success.NILE.token_obtain_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var finalResponse = Serialization.ToJson(authData.response.success);
                    var userId = authData.response.success.extensions.customer_info.user_id;
                    var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(userId)}.enc");
                    Helpers.EncryptToNileFile(tokensPath, finalResponse, userId);
                    var nileUserInfo = new NileUserInfo
                    {
                        name = authData.response.success.extensions.customer_info.name,
                        user_id = userId
                    };
                    if (!Directory.Exists(Path.GetDirectoryName(userInfoPath)))
                    {
                        FileSystem.CreateDirectory(Path.GetDirectoryName(userInfoPath));
                    }

                    FileSystem.WriteStringToFileSafe(userInfoPath, Serialization.ToJson(nileUserInfo));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to authenticate with Amazon");
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

                using var request = new HttpRequestMessage(HttpMethod.Post, @"https://gaming.amazon.com/api/distribution/entitlements");
                request.Content = strCont;
                request.Headers.Add("User-Agent", LauncherUserAgent);
                request.Headers.Add("X-Amz-Target",
                    "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
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
                    logger.Error(ex, "Failed to get account entitlements");
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

        private NileUserInfo GetNileUserInfo()
        {
            var userInfoJson = new NileUserInfo();
            if (File.Exists(userInfoPath))
            {
                var userInfoContent = FileSystem.ReadFileAsStringSafe(userInfoPath);
                if (!userInfoContent.IsNullOrEmpty() && Serialization.TryFromJson(userInfoContent, out NileUserInfo newUserInfoJson))
                {
                    userInfoJson = newUserInfoJson;
                }
            }

            return userInfoJson;
        }

        private DeviceRegistrationResponse.Response.Success LoadTokens()
        {
            if (File.Exists(userInfoPath))
            {
                try
                {
                    var userInfoJson = GetNileUserInfo();
                    if (!userInfoJson.user_id.IsNullOrEmpty())
                    {
                        var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(userInfoJson.user_id)}.enc");
                        byte[] encryptionKey = userInfoJson.user_id.GetSHA256HashByte();
                        using var stream = new FileStream(tokensPath, FileMode.Open);
                        byte[] encryptedIv = new byte[16];
                        stream.Read(encryptedIv, 0, 16);

                        byte[] iv;

                        using Aes ivCipher = Aes.Create();
                        ivCipher.Key = encryptionKey;
                        ivCipher.Mode = CipherMode.ECB;
                        ivCipher.Padding = PaddingMode.None;

                        using ICryptoTransform decryptor = ivCipher.CreateDecryptor();
                        iv = decryptor.TransformFinalBlock(encryptedIv, 0, encryptedIv.Length);

                        byte[] encryptedData = new byte[stream.Length - 16];
                        stream.Read(encryptedData, 0, encryptedData.Length);

                        using Aes cipher = Aes.Create();
                        cipher.Key = encryptionKey;
                        cipher.Mode = CipherMode.CBC;
                        cipher.IV = iv;
                        cipher.Padding = PaddingMode.PKCS7;

                        using ICryptoTransform finalDecryptor = cipher.CreateDecryptor();
                        byte[] decryptedBytes = finalDecryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                        var decryptedData = Encoding.UTF8.GetString(decryptedBytes);
                        if (!decryptedData.IsNullOrEmpty())
                        {
                            return Serialization.FromJson<DeviceRegistrationResponse.Response.Success>(decryptedData);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }

            // Migrate old tokens
            if (File.Exists(Nile.OldEncryptedTokensPath))
            {
                try
                {
                    var oldTokens = Serialization.FromJson<DeviceRegistrationResponse.Response.Success>(Encryption.DecryptFromFile(
                        Nile.OldEncryptedTokensPath, Encoding.UTF8, WindowsIdentity.GetCurrent().User?.Value));
                    var nileUserInfo = new NileUserInfo
                    {
                        name = oldTokens.extensions.customer_info.given_name,
                        user_id = oldTokens.extensions.customer_info.user_id,
                    };
                    if (!Directory.Exists(Path.GetDirectoryName(userInfoPath)))
                    {
                        FileSystem.CreateDirectory(Path.GetDirectoryName(userInfoPath));
                    }

                    FileSystem.WriteStringToFileSafe(userInfoPath, Serialization.ToJson(nileUserInfo));
                    var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(nileUserInfo.user_id)}.enc");
                    Helpers.EncryptToNileFile(tokensPath, Serialization.ToJson(oldTokens), nileUserInfo.user_id);
                    FileSystem.DeleteFile(Nile.OldEncryptedTokensPath);
                    return oldTokens;
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }

            return null;
        }

        public async Task<DeviceRegistrationResponse.Response.Success> RefreshTokens()
        {
            var tokens = LoadTokens();
            if (tokens != null)
            {
                var tokenLastUpdateTime = new DateTime();
                var userInfoJson = GetNileUserInfo();
                if (File.Exists(userInfoPath) && !userInfoJson.user_id.IsNullOrEmpty())
                {
                    var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(userInfoJson.user_id)}.enc");
                    tokenLastUpdateTime = File.GetLastWriteTime(tokensPath);
                }
                else if (File.Exists(Nile.OldEncryptedTokensPath))
                {
                    tokenLastUpdateTime = File.GetLastWriteTime(Nile.OldEncryptedTokensPath);
                }

                var tokenExpirySeconds = tokens.tokens.bearer.expires_in;
                DateTime tokenExpiryTime = tokenLastUpdateTime.AddSeconds(tokenExpirySeconds);
                if (DateTime.Now > tokenExpiryTime)
                {
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

                        var userId = userInfoJson.user_id;
                        var tokensPath = Path.Combine(Nile.ConfigPath, $"{Helpers.GetMD5(userId)}.enc");
                        Helpers.EncryptToNileFile(tokensPath, jsonTokens, userId);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to renew tokens: {ex}");
                    }
                }
            }

            return tokens;
        }


        public async Task<bool> GetIsUserLoggedIn()
        {
            var tokens = await RefreshTokens();
            if (tokens == null)
            {
                return false;
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
                using var cryptography = root.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (cryptography != null)
                {
                    return Guid.Parse((string)cryptography.GetValue("MachineGuid"));
                }
            }
            finally
            {
                root.Dispose();
            }

            return Guid.Empty;
        }

        private string EncodeBase64Url(byte[] input)
        {
            return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
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