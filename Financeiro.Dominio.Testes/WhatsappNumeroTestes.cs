using Financeiro.Dominio.Whatsapp;

namespace Financeiro.Dominio.Testes;

public sealed class WhatsappNumeroTestes
{
    [Theory]
    [InlineData("(11) 98765-4321", "+5511987654321")]
    [InlineData("11987654321", "+5511987654321")]
    [InlineData("+55 11 98765-4321", "+5511987654321")]
    [InlineData("11 3456-7890", "+551134567890")]
    public void Normalizar_AceitaFormatosBrasileiros(string entrada, string esperado)
    {
        Assert.Equal(esperado, NumeroWhatsapp.Normalizar(entrada));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("11387654321")]
    [InlineData("")]
    public void Normalizar_RejeitaInvalidos(string entrada)
    {
        Assert.Null(NumeroWhatsapp.Normalizar(entrada));
    }
}
