namespace Application.Common.Interfaces;


public interface IVacancyNormalizationPostProcessor
{


    string Process(string rawJson, string vacancyRawText);
}
