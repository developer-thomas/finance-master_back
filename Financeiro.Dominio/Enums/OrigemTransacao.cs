namespace Financeiro.Dominio.Enums;

public static class OrigemTransacao
{
    public const string AppManual = "APP_MANUAL";
    public const string AppAi = "APP_AI";
    public const string WhatsappText = "WHATSAPP_TEXT";
    public const string WhatsappImage = "WHATSAPP_IMAGE";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>
    {
        AppManual,
        AppAi,
        WhatsappText,
        WhatsappImage,
    };
}
