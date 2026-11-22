using System.Text.RegularExpressions;

namespace Financeiro.Dominio.Whatsapp;

/// <summary>
/// Normalização de celular brasileiro para E.164 (+55...).
/// </summary>
public static class NumeroWhatsapp
{
    /// <summary>
    /// Converte formatos comuns (com DDD, +55, máscara) em +55DDNNNNNNNNN, ou null se inválido.
    /// </summary>
    public static string? Normalizar(string entrada)
    {
        var digitos = Regex.Replace(entrada, @"\D", string.Empty);
        if (digitos.Length is 12 or 13)
        {
            if (!digitos.StartsWith("55", StringComparison.Ordinal))
            {
                return null;
            }

            digitos = digitos[2..];
        }

        if (digitos.Length is not (10 or 11))
        {
            return null;
        }

        if (digitos.Length == 11 && digitos[2] != '9')
        {
            return null;
        }

        return $"+55{digitos}";
    }
}
