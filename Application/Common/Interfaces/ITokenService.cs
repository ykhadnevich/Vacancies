using System;

namespace Application.Common.Interfaces;


public interface ITokenService
{


    string GenerateToken(Guid userId, string email);
}
