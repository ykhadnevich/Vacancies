using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface ICvNormalizationModuleResolver
{


    ICvNormalizationModule For(CvDomain domain);
}
