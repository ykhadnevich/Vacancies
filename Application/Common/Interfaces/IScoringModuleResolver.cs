using Application.Common.Scoring;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IScoringModuleResolver
{


    IScoringModule For(RoleFamily family);
}
