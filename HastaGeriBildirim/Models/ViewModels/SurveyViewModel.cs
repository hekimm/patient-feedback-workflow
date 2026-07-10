namespace HastaGeriBildirim.Models.ViewModels;

public class SurveyStartViewModel
{
    public string Token { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Lang { get; set; } = "tr";
}

public class SurveyConsentViewModel
{
    public string Token { get; set; } = string.Empty;
    public string ConsentText { get; set; } = string.Empty;
    public bool IsConsentGiven { get; set; }
    public bool IsAnonymous { get; set; }
    public string Lang { get; set; } = "tr";
}

public class SurveyQuestionViewModel
{
    public string Token { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public List<QuestionOption> Options { get; set; } = new();
    public int CurrentQuestionNumber { get; set; }
    public int TotalQuestions { get; set; }
    public string Lang { get; set; } = "tr";
}

public class QuestionOption
{
    public int OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
}

public class SurveyAnswerRequest
{
    public string Token { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public bool IsSkipped { get; set; }
}

public static class SurveyTexts
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["tr"] = new()
        {
            ["welcome_title"] = "Hasta Memnuniyet Anketi",
            ["welcome_text"] = "Aldığınız hizmeti değerlendirmeniz yaklaşık 1 dakika sürer.",
            ["start_button"] = "Ankete Başla",
            ["consent_title"] = "KVKK Aydınlatma Metni",
            ["consent_checkbox"] = "Aydınlatma metnini okudum, kişisel verilerimin işlenmesine açık rıza veriyorum.",
            ["anonymous_checkbox"] = "Kimliğimi paylaşmadan anonim yanıtlamak istiyorum.",
            ["continue_button"] = "Devam Et",
            ["skip_button"] = "Atla",
            ["question_label"] = "Soru",
            ["free_text_placeholder"] = "Görüşlerinizi buraya yazabilirsiniz...",
            ["thanks_title"] = "Teşekkür Ederiz!",
            ["thanks_text"] = "Geri bildiriminiz hizmet kalitemizi artırmak için değerlendirilecektir.",
            ["nps_low"] = "Kesinlikle Tavsiye Etmem",
            ["nps_high"] = "Kesinlikle Tavsiye Ederim",
            ["ces_low"] = "Çok Zor",
            ["ces_high"] = "Çok Kolay"
        },
        ["en"] = new()
        {
            ["welcome_title"] = "Patient Satisfaction Survey",
            ["welcome_text"] = "Evaluating our service takes about 1 minute.",
            ["start_button"] = "Start Survey",
            ["consent_title"] = "Privacy Notice (KVKK)",
            ["consent_checkbox"] = "I have read the privacy notice and give my explicit consent.",
            ["anonymous_checkbox"] = "I want to answer anonymously.",
            ["continue_button"] = "Continue",
            ["skip_button"] = "Skip",
            ["question_label"] = "Question",
            ["free_text_placeholder"] = "You can write your comments here...",
            ["thanks_title"] = "Thank You!",
            ["thanks_text"] = "Your feedback will help us improve our service quality.",
            ["nps_low"] = "Not likely at all",
            ["nps_high"] = "Extremely likely",
            ["ces_low"] = "Very difficult",
            ["ces_high"] = "Very easy"
        },
        ["ar"] = new()
        {
            ["welcome_title"] = "استبيان رضا المرضى",
            ["welcome_text"] = "يستغرق تقييم الخدمة حوالي دقيقة واحدة.",
            ["start_button"] = "ابدأ الاستبيان",
            ["consent_title"] = "نص الإفصاح عن البيانات الشخصية",
            ["consent_checkbox"] = "قرأت نص الإفصاح وأوافق صراحة على معالجة بياناتي الشخصية.",
            ["anonymous_checkbox"] = "أرغب في الإجابة دون الكشف عن هويتي.",
            ["continue_button"] = "متابعة",
            ["skip_button"] = "تخطي",
            ["question_label"] = "سؤال",
            ["free_text_placeholder"] = "يمكنك كتابة ملاحظاتك هنا...",
            ["thanks_title"] = "شكرا لكم!",
            ["thanks_text"] = "سيتم تقييم ملاحظاتكم لتحسين جودة خدماتنا.",
            ["nps_low"] = "لن أوصي بالتأكيد",
            ["nps_high"] = "سأوصي بالتأكيد",
            ["ces_low"] = "صعب جدا",
            ["ces_high"] = "سهل جدا"
        }
    };

    public static string Get(string lang, string key)
    {
        if (Texts.TryGetValue(lang, out var langTexts) && langTexts.TryGetValue(key, out var text))
            return text;

        return Texts["tr"].TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string NormalizeLang(string? lang)
    {
        return lang is "en" or "ar" ? lang : "tr";
    }
}
