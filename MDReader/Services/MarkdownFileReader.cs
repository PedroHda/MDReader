using MDReader.Core;
using MDReader.Models;

namespace MDReader.Services;

public sealed class MarkdownFileReader(MarkdownFileValidator validator)
{
    public MarkdownDocument Read(string filePath) => ReadAsync(filePath).GetAwaiter().GetResult();

    public Task<MarkdownDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            var validatedPath = validator.ValidatePath(filePath);
            try
            {
                await using var stream = new FileStream(validatedPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var content = await MarkdownContentReader.ReadAsync(stream, Path.GetFileName(validatedPath), cancellationToken);
                return new MarkdownDocument(validatedPath, content);
            }
            catch (FileNotFoundException ex) { throw new MarkdownFileReadException("O arquivo não existe mais. Selecione-o novamente.", ex); }
            catch (DirectoryNotFoundException ex) { throw new MarkdownFileReadException("A pasta do arquivo não foi encontrada.", ex); }
            catch (UnauthorizedAccessException ex) { throw new MarkdownFileReadException("Sem permissão para ler esse arquivo.", ex); }
            catch (System.Text.DecoderFallbackException ex) { throw new MarkdownFileReadException("Codificação inválida. Salve o documento como UTF-8.", ex); }
            catch (InvalidDataException ex) { throw new MarkdownFileReadException(ex.Message, ex); }
            catch (IOException ex) { throw new MarkdownFileReadException("Não foi possível ler o arquivo. Tente recarregar.", ex); }
        }, cancellationToken);
}
