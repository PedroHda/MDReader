using MDReader.Models;

namespace MDReader.Services;

public sealed class MarkdownFileReader
{
    private readonly MarkdownFileValidator _validator;

    public MarkdownFileReader(MarkdownFileValidator validator)
    {
        _validator = validator;
    }

    public MarkdownDocument Read(string filePath)
    {
        var validatedPath = _validator.ValidatePath(filePath);

        try
        {
            var content = File.ReadAllText(validatedPath);
            return new MarkdownDocument(validatedPath, content);
        }
        catch (FileNotFoundException ex)
        {
            throw new MarkdownFileReadException("O arquivo selecionado não existe mais.", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new MarkdownFileReadException("A pasta do arquivo selecionado não foi encontrada.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new MarkdownFileReadException("Não foi possível ler o arquivo por falta de permissão.", ex);
        }
        catch (IOException ex)
        {
            throw new MarkdownFileReadException("Ocorreu um erro de leitura ao abrir o arquivo.", ex);
        }
    }
}
