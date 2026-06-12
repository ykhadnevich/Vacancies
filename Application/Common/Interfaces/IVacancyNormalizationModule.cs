using Application.Common.Enums;
using Application.Common.VacancyNormalization;

namespace Application.Common.Interfaces;


public interface IVacancyNormalizationModule
{

    VacancyDomain Domain { get; }


    string Version { get; }


    VacancyNormalizationSlots GetSlots();
}
