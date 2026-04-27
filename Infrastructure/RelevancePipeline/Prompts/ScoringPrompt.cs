namespace Infrastructure.RelevancePipeline.Prompts;

public static class ScoringPrompt
{
    public static string Build(string userProfileText, string title, string company, string description)
    {
        return "Ти — рекрутер. Оціни наскільки вакансія підходить кандидату.\n\n" +
               "Кандидат: " + userProfileText + "\n\n" +
               "Вакансія:\n" +
               "Назва: " + title + "\n" +
               "Компанія: " + company + "\n" +
               "Опис: " + description + "\n\n" +
               "Відповідай ТІЛЬКИ валідним JSON без будь-якого тексту навколо:\n" +
               "{\"score\": 85, \"reason\": \"одне речення чому\"}\n" +
               "score — ціле число від 0 до 100.";
    }
}