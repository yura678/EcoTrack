using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Persistence;
using Application.Models.Jwt;
using Domain.Entities.User;
using Infrastructure.Identity.Dtos;
using Infrastructure.Identity.Manager;
using LanguageExt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity.Jwt;

public class JwtService : IJwtService
{
    private readonly IdentitySettings _siteSetting;
    private readonly AppUserManager _userManager;
    private IUserClaimsPrincipalFactory<User> _claimsPrincipal;

    private readonly IUnitOfWork _unitOfWork;
    //private readonly AppUserClaimsPrincipleFactory claimsPrincipleFactory;

    public JwtService(IOptions<IdentitySettings> siteSetting, AppUserManager userManager,
        IUserClaimsPrincipalFactory<User> claimsPrincipal, IUnitOfWork unitOfWork)
    {
        _siteSetting = siteSetting.Value;
        _userManager = userManager;
        _claimsPrincipal = claimsPrincipal;
        _unitOfWork = unitOfWork;
    }

    public async Task<AccessToken> GenerateAsync(User user, CancellationToken cancellationToken)
    {
        var secretKey = Encoding.UTF8.GetBytes(_siteSetting.SecretKey); // longer that 16 character
        var signingCredentials =
            new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature);

        var encryptionkey = Encoding.UTF8.GetBytes(_siteSetting.EncryptKey); //must be 16 character
        var encryptingCredentials = new EncryptingCredentials(new SymmetricSecurityKey(encryptionkey),
            SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);


        var claims = await _getClaimsAsync(user);

        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _siteSetting.Issuer,
            Audience = _siteSetting.Audience,
            IssuedAt = now,
            NotBefore = now.AddMinutes(_siteSetting.NotBeforeMinutes),
            Expires = now.AddMinutes(_siteSetting.ExpirationMinutes),
            SigningCredentials = signingCredentials,
            EncryptingCredentials = encryptingCredentials,
            Subject = new ClaimsIdentity(claims)
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var securityToken = tokenHandler.CreateJwtSecurityToken(descriptor);


        var refreshToken = await _unitOfWork.UserRefreshTokenRepository.CreateToken(user.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessToken(securityToken, refreshToken.ToString());
    }

    public Task<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false, //you might want to validate the audience and issuer depending on your use case
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_siteSetting.SecretKey)),
            ValidateLifetime = false,
            TokenDecryptionKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_siteSetting.EncryptKey)) 
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken securityToken;
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
        var jwtSecurityToken = securityToken as JwtSecurityToken;
        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
            throw new SecurityTokenException("Invalid token");

        return Task.FromResult(principal);
    }

    public async Task<AccessToken> GenerateByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
        var result = await this.GenerateAsync(user, cancellationToken);
        return result;
    }

    public async Task<Option<AccessToken>> RefreshToken(Guid refreshTokenId, CancellationToken cancellationToken)
    {
        var refreshTokenOption = await _unitOfWork.UserRefreshTokenRepository.GetTokenWithInvalidation(refreshTokenId);

        return await refreshTokenOption.MatchAsync<UserRefreshToken, Option<AccessToken>>(
            Some: async refreshToken =>
            {
                refreshToken.IsValid = false;
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var userOption = await _unitOfWork.UserRefreshTokenRepository.GetUserByRefreshToken(refreshTokenId);

                return await userOption.MatchAsync<User, Option<AccessToken>>(
                    Some: async unpackedUser =>
                    {
                        var result = await this.GenerateAsync(unpackedUser, cancellationToken);
                        return Option<AccessToken>.Some(result);
                    },
                    None: () => Option<AccessToken>.None
                );
            },
            None: () => Option<AccessToken>.None
        );
    }

    private async Task<IEnumerable<Claim>> _getClaimsAsync(User user)
    {
        var result = await _claimsPrincipal.CreateAsync(user);
        return result.Claims;
    }
}