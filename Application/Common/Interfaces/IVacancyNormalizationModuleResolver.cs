using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface IVacancyNormalizationModuleResolver
{


    IVacancyNormalizationModule For(VacancyDomain domain);
}
