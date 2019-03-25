namespace PublicServiceRegistry.Api.Backoffice.Security
{
    public class OpenIdConnectConfiguration
    {
        public static string Section = "OIDCAuth";

        public string Authority { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string CallbackPath { get; set; }

        public string TokenEndPoint { get; set; }

        public string JwtCookieName { get; set; }

        public int JwtCookieDurationInMinutes { get; set; }

        public string JwtCookieDomain { get; set; }

        public bool JwtCookieSecure { get; set; }

        public string JwtSharedSigningKey { get; set; }

        public string JwtIssuer { get; set; }

        public string JwtAudience { get; set; }

        public string Developers { get; set; }

        public string ReturnUrlGuard { get; set; }

        public string SignOutReturnUrl { get; set; }

        public string AuthorizationRedirectUri { get; set; }
    }
}
