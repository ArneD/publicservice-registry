namespace PublicServiceRegistry.Api.Backoffice.Security
{
    using System;
    using System.Security.Claims;
    using System.Text;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;

    public class PublicServiceRegistryTokenValidationParameters : TokenValidationParameters
    {
        public PublicServiceRegistryTokenValidationParameters(IOptions<OpenIdConnectConfiguration> authOptions)
            : this(authOptions.Value) { }

        public PublicServiceRegistryTokenValidationParameters(OpenIdConnectConfiguration auth)
        {
            var secretKey = auth.JwtSharedSigningKey;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // The signing key must match!
            ValidateIssuerSigningKey = true;
            IssuerSigningKey = signingKey;

            // Validate the JWT Issuer (iss) claim
            ValidateIssuer = true;
            ValidIssuer = auth.JwtIssuer;

            // Validate the JWT Audience (aud) claim
            ValidateAudience = true;
            ValidAudience = auth.JwtAudience;

            // Validate the token expiry
            ValidateLifetime = true;

            // If you want to allow a certain amount of clock drift, set that here:
            ClockSkew = new TimeSpan(0, 5, 0);

            RoleClaimType = ClaimTypes.Role;
        }
    }
}
