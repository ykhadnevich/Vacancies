using Application.Common.CvNormalization;
using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface ICvNormalizationModule
{

    CvDomain Domain { get; }


    string Version { get; }


    CvNormalizationSlots GetSlots();
}
