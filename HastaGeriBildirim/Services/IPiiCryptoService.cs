namespace HastaGeriBildirim.Services;

public interface IPiiCryptoService
{
    string? Encrypt(string? plainText);
    string? Decrypt(string? cipherText);
    string? HashForLookup(string? value);
}

