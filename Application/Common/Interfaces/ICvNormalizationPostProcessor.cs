namespace Application.Common.Interfaces;


public interface ICvNormalizationPostProcessor
{


    string Process(string rawJson, string cvRawText);
}
